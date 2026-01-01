using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn.Internals.Cleanup;

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
            _sharedExecutionState.ScenarioResultState.StandardCollectors[scenario.Name].MarkPhaseAsStarted(FeatureTestPhase.Cleanup);
            _sharedExecutionState.ScenarioResultState.LoadCollectors[scenario.Name].MarkPhaseAsStarted(LoadTestPhase.Cleanup);
        }

        var cleanupPerScenarioTasks = new List<Task>();

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            if (scenario.AfterScenarioAction != null)
                cleanupPerScenarioTasks.Add(ExecuteCleanupScenario(_testFramework, scenario));
        }

        await Task.WhenAll(cleanupPerScenarioTasks);

        await ExecuteCleanupTestMethod();

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            _sharedExecutionState.ScenarioResultState.StandardCollectors[scenario.Name].MarkPhaseAsCompleted(FeatureTestPhase.Cleanup);
            _sharedExecutionState.ScenarioResultState.LoadCollectors[scenario.Name].MarkPhaseAsCompleted(LoadTestPhase.Cleanup);
        }
    }

    private async Task ExecuteCleanupScenario(ITestFrameworkAdapter testFramework, Scenario scenario)
    {
        var context = ContextFactory.CreateScenarioContext(testFramework, "CleanupScenario");
        await scenario.AfterScenarioAction(context);
    }

    private async Task ExecuteCleanupTestMethod()
    {
        if (_sharedExecutionState.TestClassInstance is IAfterTest cleanup)
        {
            Context context = ContextFactory.CreateContext(_testFramework, "CleanupScenarioTestMethod");
            await cleanup.AfterTest(context);
        }
    }
}
