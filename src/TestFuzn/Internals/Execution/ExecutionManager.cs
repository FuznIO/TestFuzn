using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Execution.Consumers;
using Fuzn.TestFuzn.Internals.Execution.Producers;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecutionManager
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly TestExecutionState _testExecutionState;
    private readonly ConsumerManager _consumerManager;
    private readonly ProducerManager _producerManager;

    public ExecutionManager(ITestFrameworkAdapter testFramework, 
        TestExecutionState testExecutionState,
        ProducerManager producerManager,
        ConsumerManager consumerManager)
    {
        _testFramework = testFramework;
        _testExecutionState = testExecutionState;
        _producerManager = producerManager;
        _consumerManager = consumerManager;
    }
    public async Task Run()
    {
        _producerManager.StartProducers();

        _consumerManager.StartConsumers();

        await _producerManager.WaitForProducersToComplete();

        await _consumerManager.WaitForConsumersToFinish();

        ExecuteAssertWhenDone();

        _testExecutionState.Complete();
    }

    private void ExecuteAssertWhenDone()
    {
        if (_testExecutionState.TestResult.TestType == TestType.Standard)
            return;

        if (_testExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Stopped)
            return;

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var scenarioCollector = _testExecutionState.LoadCollectors[scenario.Name];
            var scenarioResult = scenarioCollector.GetCurrentResult();
            if (scenario.AssertWhenDoneAction != null)
            {
                try
                {
                    var context = ContextFactory.CreateScenarioContext(_testFramework, "AssertWhenDoneAction");
                    scenario.AssertWhenDoneAction(context, new AssertScenarioStats(scenarioResult));
                }
                catch (Exception e)
                {
                    _testExecutionState.TestRunState.FirstException = e;
                    _testExecutionState.TestResult.Status = TestStatus.Failed;
                    scenarioCollector.SetAssertWhenDoneException(e);
                    scenarioCollector.SetStatus(TestStatus.Failed);
                }
            }
        }
    }
}
