using TestFusion.Internals.State;
using TestFusion.Internals;
using TestFusion.Internals.Results.Feature;
using TestFusion.Contracts.Adapters;
using TestFusion.Internals.Reports;

namespace TestFusion;

public static class TestFusionIntegration
{
    private static IStartup _startupInstance;

    public static async Task InitGlobal(ITestFrameworkAdapter testFramework)
    {
        GlobalState.Init();

        // Scan all loaded assemblies for a type that implements IStartup
        var startupType = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => typeof(IStartup).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        if (startupType == null)
            throw new InvalidOperationException("No class implementing IStartup was found in the loaded assemblies.");

        _startupInstance = Activator.CreateInstance(startupType) as IStartup;

        var configuration = _startupInstance.Configuration();
        if (configuration == null)
        {
            configuration = new TestFusionConfiguration();
            configuration.TestSuiteName = "Default";
        }
        GlobalState.Configuration = configuration;

        var context = ContextFactory.CreateContext(testFramework, "InitGlobal");
        await _startupInstance.InitGlobal(context);

        GlobalState.TestsOutputDirectory = Path.Combine(testFramework.TestResultsDirectory, $"TestFusion_{GlobalState.TestRunId}");
        Directory.CreateDirectory(GlobalState.TestsOutputDirectory);

        GlobalState.Logger = LoggerFactory.CreateLogger();

        GlobalState.IsInitializeGlobalExecuted = true;

        foreach (var plugin in GlobalState.Configuration.SinkPlugins)
        {
            await plugin.InitGlobal();
        }

        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
        {
            await plugin.InitGlobal();
        }
    }

    public static async Task CleanupGlobal(ITestFrameworkAdapter testFramework)
    {
        if (_startupInstance == null)
            throw new InvalidOperationException("TestFusionIntegration has not been initialized. Please call TestFusionIntegration.InitializeGlobal() before running tests.");

        await _startupInstance.CleanupGlobal(ContextFactory.CreateContext(testFramework, "CleanupGlobal"));

        await new ReportManager().WriteFeatureReports(new FeatureResultManager());

        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
            await plugin.CleanupGlobal();

        foreach (var plugin in GlobalState.Configuration.SinkPlugins)
            await plugin.CleanupGlobal();
    }
}
