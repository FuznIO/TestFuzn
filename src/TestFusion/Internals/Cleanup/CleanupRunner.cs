using TestFusion.Internals.State;
using TestFusion.Plugins.TestFrameworkProviders;

namespace TestFusion.Internals.Cleanup;

internal class CleanupRunner
{
    private readonly ITestFrameworkProvider _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;

    public CleanupRunner(ITestFrameworkProvider testFramework, SharedExecutionState sharedExecutionState)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
    }

    internal async Task Cleanup()
    {
        await ExecuteCleanupAfterScenarioTest(_testFramework);

        var cleanupPerScenarioTasks = new List<Task>();

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            if (scenario.CleanupAfterScenario != null)
                cleanupPerScenarioTasks.Add(ExecuteScenarioCleanup(_testFramework, scenario));
        }

        await Task.WhenAll(cleanupPerScenarioTasks);
    }

    public async Task ExecuteCleanupAfterScenarioTest(ITestFrameworkProvider testFramework)
    {
        var context = ContextFactory.CreateContext(testFramework, "AfterEachScenarioTest");
        await _sharedExecutionState.FeatureTestClassInstance.CleanupScenarioTest(context);
    }

    private async Task ExecuteScenarioCleanup(ITestFrameworkProvider testFramework, Scenario scenario)
    {
        var context = ContextFactory.CreateContext(testFramework, "CleanupAfterScenario");
        await scenario.CleanupAfterScenario(context);
    }
}
