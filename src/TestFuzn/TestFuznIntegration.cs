using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Results.Feature;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Reports;

namespace Fuzn.TestFuzn;

public static class TestFuznIntegration
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

        GlobalState.TestsOutputDirectory = Path.Combine(testFramework.TestResultsDirectory, $"TestFuzn_{GlobalState.TestRunId}");
        Directory.CreateDirectory(GlobalState.TestsOutputDirectory);

        GlobalState.Logger = Internals.Logging.LoggerFactory.CreateLogger();
        GlobalState.Logger.LogInformation("Logging initialized");

        _startupInstance = Activator.CreateInstance(startupType) as IStartup;

        var configuration = _startupInstance.Configuration();
        if (configuration == null)
        {
            configuration = new TestFuznConfiguration();
            configuration.TestSuite.Name = "Default";
        }
        GlobalState.Configuration = configuration;

        var context = ContextFactory.CreateContext(testFramework, "InitGlobal");
        await _startupInstance.InitGlobal(context);

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

        await _startupInstance.CleanupGlobal(ContextFactory.CreateContext(testFramework, "CleanupGlobal"));

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
