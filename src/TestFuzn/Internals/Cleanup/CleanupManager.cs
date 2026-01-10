using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn.Internals.Cleanup;

internal class CleanupManager
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly TestExecutionState _testExecutionState;

    public CleanupManager(ITestFrameworkAdapter testFramework, TestExecutionState testExecutionState)
    {
        _testFramework = testFramework;
        _testExecutionState = testExecutionState;
    }

    public async Task Run()
    {
        foreach (var scenario in _testExecutionState.Scenarios)
        {
            _testExecutionState.ScenarioResultState.StandardCollectors[scenario.Name].MarkPhaseAsStarted(StandardTestPhase.Cleanup);
            _testExecutionState.ScenarioResultState.LoadCollectors[scenario.Name].MarkPhaseAsStarted(LoadTestPhase.Cleanup);
        }

        var cleanupPerScenarioTasks = new List<Task>();

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            if (scenario.AfterScenarioAction != null)
                cleanupPerScenarioTasks.Add(ExecuteCleanupScenario(_testFramework, scenario));
        }

        await Task.WhenAll(cleanupPerScenarioTasks);

        await ExecuteCleanupTestMethod();

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            _testExecutionState.ScenarioResultState.StandardCollectors[scenario.Name].MarkPhaseAsCompleted(StandardTestPhase.Cleanup);
            _testExecutionState.ScenarioResultState.LoadCollectors[scenario.Name].MarkPhaseAsCompleted(LoadTestPhase.Cleanup);
        }
    }

    private async Task ExecuteCleanupScenario(ITestFrameworkAdapter testFramework, Scenario scenario)
    {
        var context = ContextFactory.CreateScenarioContext(testFramework, "AfterScenario");
        await scenario.AfterScenarioAction(context);
    }

    private async Task ExecuteCleanupTestMethod()
    {
        if (_testExecutionState.TestClassInstance is IAfterTest cleanup)
        {
            Context context = ContextFactory.CreateContext(_testFramework, "AfterTest");
            await cleanup.AfterTest(context);
        }
    }
}
