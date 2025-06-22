using TestFusion.Internals.State;
using TestFusion.Contracts.Adapters;

namespace TestFusion.Internals.Cleanup;

internal class CleanupManager
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;

    public CleanupManager(ITestFrameworkAdapter testFramework, SharedExecutionState sharedExecutionState)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
    }

    public async Task Run()
    {
        var cleanupPerScenarioTasks = new List<Task>();

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            if (scenario.CleanupAfterScenarioAction != null)
                cleanupPerScenarioTasks.Add(ExecuteCleanupAfterScenario(_testFramework, scenario));
        }

        await Task.WhenAll(cleanupPerScenarioTasks);

        await IFeatureTestCleanup();
    }

    private async Task ExecuteCleanupAfterScenario(ITestFrameworkAdapter testFramework, Scenario scenario)
    {
        var context = ContextFactory.CreateContext(testFramework, "CleanupAfterScenario");
        await scenario.CleanupAfterScenarioAction(context);
    }

    private async Task IFeatureTestCleanup()
    {
        var context = ContextFactory.CreateContext(_testFramework, "CleanupScenarioTest");
        await _sharedExecutionState.IFeatureTestClassInstance.CleanupScenarioTest(context);
    }
}
