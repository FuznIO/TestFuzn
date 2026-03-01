using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Cleanup;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Consumers;
using Fuzn.TestFuzn.Internals.Execution.Producers;
using Fuzn.TestFuzn.Internals.Init;
using Fuzn.TestFuzn.Internals.InputData;
using Fuzn.TestFuzn.Internals.Logger;
using Fuzn.TestFuzn.Internals.Reports.EmbeddedResources;
using Fuzn.TestFuzn.Internals.Reports.Load;
using Fuzn.TestFuzn.Internals.Reports.Standard;
using Fuzn.TestFuzn.Internals.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Internals;

internal static class TestFuznIntegrationCore
{
    public static async Task Init(ITestFrameworkAdapter testFramework, Dictionary<string, string> args = null)
    {
        var session = TestSession.Current;

        session.TestRunStartTime = DateTime.UtcNow;
        session.TestRunId = $"{DateTime.Now:yyyy-MM-dd_HH-mm}__{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        
        session.TargetEnvironment = 
            ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(args, "target-environment", "TESTFUZN_TARGET_ENVIRONMENT");

        session.ExecutionEnvironment = 
            ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(args, "execution-environment", "TESTFUZN_EXECUTION_ENVIRONMENT");

        var tagsInclude = ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(args, "tags-filter-include", "TESTFUZN_TAGS_FILTER_INCLUDE");
        if (!string.IsNullOrEmpty(tagsInclude))
        {
            session.TagsFilterInclude.AddRange(tagsInclude.Split(',').Select(t => t.Trim()));
        }

        var tagsExclude = ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(args, "tags-filter-exclude", "TESTFUZN_TAGS_FILTER_EXCLUDE");
        if (!string.IsNullOrEmpty(tagsExclude))
        {
            session.TagsFilterExclude.AddRange(tagsExclude.Split(',').Select(t => t.Trim()));
        }

        session.NodeName = Environment.MachineName;

        // Scan all loaded assemblies for a type that implements IStartup
        var startupType = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => typeof(IStartup).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        if (startupType == null)
            throw new InvalidOperationException("No class implementing IStartup was found in the loaded assemblies.");

        var testAssemblyName = startupType.Assembly.GetName().Name;
        session.TestsOutputDirectory = Path.Combine(testFramework.TestResultsDirectory, "TestFuznResults", testAssemblyName, $"{session.TestRunId}");
        Directory.CreateDirectory(session.TestsOutputDirectory);

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile("Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Styles.testfuzn.css",
                                        Path.Combine(session.TestsOutputDirectory, "assets/styles/testfuzn.css"));

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile("Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Scripts.chart.js",
                                        Path.Combine(session.TestsOutputDirectory, "assets/scripts/chart.js"));

        session.Logger = Internals.Logging.LoggerFactory.CreateLogger(session.TestsOutputDirectory);
        session.Logger.LogInformation("Logging initialized");

        session.StartupInstance = Activator.CreateInstance(startupType) as IStartup;
        if (session.StartupInstance == null)
            throw new InvalidOperationException($"Failed to create an instance of {startupType.FullName}.");

        var configuration = new TestFuznConfiguration();
        configuration.Suite = new SuiteInfo();
        configuration.Suite.Name = testAssemblyName;
        session.StartupInstance.Configure(configuration);            

        AddServices(configuration);

        // Build the service provider after all plugins have registered their services
        configuration.BuildServiceProvider();

        session.Configuration = configuration;

        if (session.StartupInstance is IBeforeSuite initGlobalInstance)
        {
            var context = ContextFactory.CreateContext(testFramework, "Init");
            await initGlobalInstance.BeforeSuite(context);
        }

        foreach (var plugin in session.Configuration.SinkPlugins)
            await plugin.InitSuite();

        foreach (var plugin in session.Configuration.ContextPlugins)
            await plugin.InitSuite();

        session.IsInitializeGlobalExecuted = true;
    }

    private static void AddServices(TestFuznConfiguration configuration)
    {
        var services = configuration.Services;

        services.AddScoped<TestRunner>();
        services.AddScoped<TestExecutionState>();
        services.AddScoped<ConsoleWriter>();
        services.AddScoped<ConsoleManager>();
        services.AddScoped<InitManager>();
        services.AddScoped<InputDataFeeder>();
        services.AddScoped<ProducerManager>();
        services.AddScoped<ExecuteScenarioMessageHandler>();
        services.AddScoped<ConsumerManager>();
        services.AddScoped<ExecutionManager>();
        services.AddScoped<CleanupManager>();
        services.AddScoped<LoadReportManager>();
    }

    public static async Task Cleanup(ITestFrameworkAdapter testFramework)
    {
        var session = TestSession.Current;

        if (session.StartupInstance == null)
            throw new InvalidOperationException("TestFuznIntegration has not been initialized. Please call TestFuznIntegration.InitSuite() before running tests.");

        if (session.StartupInstance is IAfterSuite cleanupGlobalInstance)
        {
            await cleanupGlobalInstance.AfterSuite(ContextFactory.CreateContext(testFramework, "Cleanup"));
        }

        session.TestRunEndTime = DateTime.UtcNow;

        if (!session.IsInitializeGlobalExecuted)
            return;
        
        await new StandardReportManager().WriteStandardReports(session.ResultManager);

        foreach (var plugin in session.Configuration.ContextPlugins)
            await plugin.CleanupSuite();

        foreach (var plugin in session.Configuration.SinkPlugins)
            await plugin.CleanupSuite();
    }
}
