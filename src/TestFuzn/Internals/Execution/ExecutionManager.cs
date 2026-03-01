using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Execution.Consumers;
using Fuzn.TestFuzn.Internals.Execution.Producers;
using Fuzn.TestFuzn.Contracts;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecutionManager
{
    private readonly TestExecutionState _testExecutionState;
    private readonly ProducerManager _producerManager;
    private readonly ConsumerManager _consumerManager;
    private readonly ExecuteScenarioMessageHandler _executeScenarioMessageHandler;

    public ExecutionManager(
        TestExecutionState testExecutionState,
        ProducerManager producerManager,
        ConsumerManager consumerManager,
        ExecuteScenarioMessageHandler executeScenarioMessageHandler)
    {
        _testExecutionState = testExecutionState;
        _producerManager = producerManager;
        _consumerManager = consumerManager;
        _executeScenarioMessageHandler = executeScenarioMessageHandler;
    }

    public async Task Run()
    {
        _producerManager.StartProducers(_testExecutionState);

        _consumerManager.StartConsumers(_testExecutionState, _executeScenarioMessageHandler);

        await _producerManager.WaitForProducersToComplete();

        await _consumerManager.WaitForConsumersToFinish();

        ExecuteAssertWhenDone();

        _testExecutionState.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Execute, DateTime.UtcNow);
    }

    private void ExecuteAssertWhenDone()
    {
        if (_testExecutionState.TestResult.TestType == TestType.Standard)
            return;

        if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
            return;

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var scenarioCollector = _testExecutionState.LoadCollectors[scenario.Name];
            var scenarioResult = scenarioCollector.GetCurrentResult();
            if (scenario.AssertWhenDoneAction != null)
            {
                try
                {
                    var context = ContextFactory.CreateScenarioContext(_testExecutionState.TestFramework, "AssertWhenDoneAction");
                    scenario.AssertWhenDoneAction(context, new AssertScenarioStats(scenarioResult));
                }
                catch (Exception e)
                {
                    _testExecutionState.FirstException = e;
                    _testExecutionState.TestResult.Status = TestStatus.Failed;
                    scenarioCollector.SetAssertWhenDoneException(e);
                    scenarioCollector.SetStatus(TestStatus.Failed);
                }
            }
        }
    }
}
