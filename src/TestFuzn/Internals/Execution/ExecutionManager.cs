using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Execution.Consumers;
using Fuzn.TestFuzn.Internals.Execution.Producers;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecutionManager
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly ConsumerManager _consumerManager;
    private readonly ProducerManager _producerManager;

    public ExecutionManager(ITestFrameworkAdapter testFramework, 
        SharedExecutionState sharedExecutionState,
        ProducerManager producerManager,
        ConsumerManager consumerManager)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
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

        _sharedExecutionState.Complete();
    }

    private void ExecuteAssertWhenDone()
    {
        if (_sharedExecutionState.TestType == TestType.Standard)
            return;

        if (_sharedExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Stopped)
            return;

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            var scenarioCollector = _sharedExecutionState.ScenarioResultState.LoadCollectors[scenario.Name];
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
                    _sharedExecutionState.TestRunState.FirstException = e;
                    scenarioCollector.SetAssertWhenDoneException(e);
                    scenarioCollector.SetStatus(TestStatus.Failed);
                }
            }
        }
    }
}
