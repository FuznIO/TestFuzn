using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn.Internals.Cleanup;

internal class CleanupManager
{
    private readonly IServiceProvider _serviceProvider;
    private TestExecutionState _testExecutionState;

    public CleanupManager(IServiceProvider serviceProvider, TestExecutionState testExecutionState)
    {
        _serviceProvider = serviceProvider;
        _testExecutionState = testExecutionState;
    }

    public async Task Run()
    {
        var timestampStarted = DateTime.UtcNow;

        _testExecutionState.TestResult.MarkPhaseAsStarted(StandardTestPhase.Cleanup, timestampStarted);

        if (_testExecutionState.TestType == Contracts.TestType.Load)
        {
            foreach (var scenario in _testExecutionState.Scenarios)
            {
                _testExecutionState.LoadCollectors[scenario.Name].MarkPhaseAsStarted(LoadTestPhase.Cleanup, timestampStarted);
            }
        }

        var cleanupPerScenarioTasks = new List<Task>();

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            if (scenario.AfterScenarioAction != null)
                cleanupPerScenarioTasks.Add(ExecuteCleanupScenario(_testExecutionState.TestFramework, scenario));
        }

        await Task.WhenAll(cleanupPerScenarioTasks);

        await ExecuteCleanupTestMethod();

        var timestampCompleted = DateTime.UtcNow;

        if (_testExecutionState.TestType == Contracts.TestType.Load)
        {
            foreach (var scenario in _testExecutionState.Scenarios)
            {
                _testExecutionState.LoadCollectors[scenario.Name].MarkPhaseAsCompleted(LoadTestPhase.Cleanup, timestampCompleted);
            }
        }

        _testExecutionState.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Cleanup, timestampCompleted);
    }

    private async Task ExecuteCleanupScenario(ITestFrameworkAdapter testFramework, Scenario scenario)
    {
        var context = ContextFactory.CreateScenarioContext(_testExecutionState.TestSession, _serviceProvider, testFramework, "AfterScenario");
        await scenario.AfterScenarioAction(context);
    }

    private async Task ExecuteCleanupTestMethod()
    {
        if (_testExecutionState.TestClassInstance is IAfterTest cleanup)
        {
            Context context = ContextFactory.CreateContext(_testExecutionState.TestSession, _serviceProvider, _testExecutionState.TestFramework, "AfterTest");
            await cleanup.AfterTest(context);
        }
    }
}
