using FuznLabs.TestFuzn.Internals.State;
using FuznLabs.TestFuzn.Contracts.Adapters;
using FuznLabs.TestFuzn.Internals.Execution;

namespace FuznLabs.TestFuzn.Internals.Cleanup;

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
        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            _sharedExecutionState.ResultState.FeatureCollectors[scenario.Name].MarkPhaseAsStarted(FeatureTestPhase.Cleanup);
            _sharedExecutionState.ResultState.LoadCollectors[scenario.Name].MarkPhaseAsStarted(LoadTestPhase.Cleanup);
        }

        var cleanupPerScenarioTasks = new List<Task>();

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            if (scenario.CleanupAfterScenarioAction != null)
                cleanupPerScenarioTasks.Add(ExecuteCleanupAfterScenario(_testFramework, scenario));
        }

        await Task.WhenAll(cleanupPerScenarioTasks);

        await IFeatureTestCleanup();

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            _sharedExecutionState.ResultState.FeatureCollectors[scenario.Name].MarkPhaseAsCompleted(FeatureTestPhase.Cleanup);
            _sharedExecutionState.ResultState.LoadCollectors[scenario.Name].MarkPhaseAsCompleted(LoadTestPhase.Cleanup);
        }
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
