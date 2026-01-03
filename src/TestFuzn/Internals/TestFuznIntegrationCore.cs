using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Reports;
using Fuzn.TestFuzn.Internals.Results.Standard;
using System.Reflection;

namespace Fuzn.TestFuzn.Internals;

internal static class TestFuznIntegrationCore
{
    private static IStartup _startupInstance;

    public static async Task Init(ITestFrameworkAdapter testFramework, Dictionary<string, string> args = null)
    {
        GlobalState.TestRunStartTime = DateTime.UtcNow;
        GlobalState.TestRunId = $"{DateTime.Now:yyyy-MM-dd_HH-mm}__{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        
        GlobalState.TargetEnvironment = 
            ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(args, "target-environment", "TESTFUZN_TARGET_ENVIRONMENT");

        GlobalState.ExecutionEnvironment = 
            ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(args, "execution-environment", "TESTFUZN_EXECUTION_ENVIRONMENT");

        var tagsInclude = ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(args, "tags-filter-include", "TESTFUZN_TAGS_FILTER_INCLUDE");
        if (!string.IsNullOrEmpty(tagsInclude))
        {
            GlobalState.TagsFilterInclude.AddRange(tagsInclude.Split(',').Select(t => t.Trim()));
        }

        var tagsExclude = ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(args, "tags-filter-exclude", "TESTFUZN_TAGS_FILTER_EXCLUDE");
        if (!string.IsNullOrEmpty(tagsExclude))
        {
            GlobalState.TagsFilterExclude.AddRange(tagsExclude.Split(',').Select(t => t.Trim()));
        }

        GlobalState.NodeName = Environment.MachineName;
        GlobalState.Logger = Internals.Logging.LoggerFactory.CreateLogger();
        GlobalState.Logger.LogInformation("Logging initialized");

        // Scan all loaded assemblies for a type that implements IStartup
        var startupType = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => typeof(IStartup).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        if (startupType == null)
            throw new InvalidOperationException("No class implementing IStartup was found in the loaded assemblies.");

        var testAssemblyName = startupType.Assembly.GetName().Name;
        GlobalState.TestsOutputDirectory = Path.Combine(testFramework.TestResultsDirectory, "TestFuznResults", testAssemblyName, $"{GlobalState.TestRunId}");
        Directory.CreateDirectory(GlobalState.TestsOutputDirectory);

        _startupInstance = Activator.CreateInstance(startupType) as IStartup;
        if (_startupInstance == null)
            throw new InvalidOperationException($"Failed to create an instance of {startupType.FullName}.");

        var configuration = new TestFuznConfiguration();
        configuration.Suite = new SuiteInfo();
        configuration.Suite.Name = Assembly.GetExecutingAssembly().GetName().Name;
        _startupInstance.Configure(configuration);            

        GlobalState.Configuration = configuration;

        if (_startupInstance is IBeforeSuite initGlobalInstance)
        {
            var context = ContextFactory.CreateContext(testFramework, "Init");
            await initGlobalInstance.BeforeSuite(context);
        }

        foreach (var plugin in GlobalState.Configuration.SinkPlugins)
            await plugin.InitSuite();

        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
            await plugin.InitSuite();

        GlobalState.IsInitializeGlobalExecuted = true;
    }

    public static async Task Cleanup(ITestFrameworkAdapter testFramework)
    {
        if (_startupInstance == null)
            throw new InvalidOperationException("TestFuznIntegration has not been initialized. Please call TestFuznIntegration.InitSuite() before running tests.");

        if (_startupInstance is IAfterSuite cleanupGlobalInstance)
        {
            await cleanupGlobalInstance.AfterSuite(ContextFactory.CreateContext(testFramework, "Cleanup"));
        }

        GlobalState.TestRunEndTime = DateTime.UtcNow;

        if (!GlobalState.IsInitializeGlobalExecuted)
            return;
        
        await new ReportManager().WriteStandardReports(new StandardResultManager());

        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
            await plugin.CleanupSuite();

        foreach (var plugin in GlobalState.Configuration.SinkPlugins)
            await plugin.CleanupSuite();
    }
}
