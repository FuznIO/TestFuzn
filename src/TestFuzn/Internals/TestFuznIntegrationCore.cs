using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Reports;
using Fuzn.TestFuzn.Internals.Results.Feature;
using System.Reflection;

namespace Fuzn.TestFuzn.Internals;

internal static class TestFuznIntegrationCore
{
    private static IStartup _startupInstance;

    public static async Task InitGlobal(ITestFrameworkAdapter testFramework, Dictionary<string, string> args = null)
    {
        GlobalState.TestRunStartTime = DateTime.UtcNow;
        GlobalState.TestRunId = $"{DateTime.Now:yyyy-MM-dd_HH-mm}__{Guid.NewGuid().ToString("N").Substring(0, 6)}";

        GlobalState.EnvironmentName = ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(args, "environment", "TESTFUZN_ENVIRONMENT");

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
        GlobalState.TestsOutputDirectory = Path.Combine(testFramework.TestResultsDirectory, $"TestFuzn_{GlobalState.TestRunId}");
        Directory.CreateDirectory(GlobalState.TestsOutputDirectory);
        GlobalState.Logger = Internals.Logging.LoggerFactory.CreateLogger();
        GlobalState.Logger.LogInformation("Logging initialized");

        // Scan all loaded assemblies for a type that implements IStartup
        var startupType = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => typeof(IStartup).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        if (startupType == null)
            throw new InvalidOperationException("No class implementing IStartup was found in the loaded assemblies.");

        _startupInstance = Activator.CreateInstance(startupType) as IStartup;
        if (_startupInstance == null)
            throw new InvalidOperationException($"Failed to create an instance of {startupType.FullName}.");

        var configuration = _startupInstance.Configuration();
        if (configuration == null)
        {
            configuration = new TestFuznConfiguration();
        }
        if (configuration.TestSuite == null)
            configuration.TestSuite = new();

        if (string.IsNullOrEmpty(configuration.TestSuite.Name))
            configuration.TestSuite.Name = Assembly.GetExecutingAssembly().GetName().Name;

        GlobalState.Configuration = configuration;

        if (_startupInstance is ISetupRun initGlobalInstance)
        {
            var context = ContextFactory.CreateContext(testFramework, "InitGlobal");
            await initGlobalInstance.BeforeRun(context);
        }

        foreach (var plugin in GlobalState.Configuration.SinkPlugins)
            await plugin.InitGlobal();

        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
            await plugin.InitGlobal();

        GlobalState.IsInitializeGlobalExecuted = true;
    }

    public static async Task CleanupGlobal(ITestFrameworkAdapter testFramework)
    {
        if (_startupInstance == null)
            throw new InvalidOperationException("TestFuznIntegration has not been initialized. Please call TestFuznIntegration.InitGlobal() before running tests.");

        if (_startupInstance is ITeardownRun cleanupGlobalInstance)
        {
            await cleanupGlobalInstance.TeardownRun(ContextFactory.CreateContext(testFramework, "CleanupGlobal"));
        }

        GlobalState.TestRunEndTime = DateTime.UtcNow;

        if (!GlobalState.IsInitializeGlobalExecuted)
            return;
        
        await new ReportManager().WriteFeatureReports(new FeatureResultManager());

        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
            await plugin.CleanupGlobal();

        foreach (var plugin in GlobalState.Configuration.SinkPlugins)
            await plugin.CleanupGlobal();
    }
}
