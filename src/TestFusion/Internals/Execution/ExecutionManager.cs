using TestFusion.Internals.State;
using TestFusion.Contracts.Adapters;
using TestFusion.Internals.Execution.Consumers;
using TestFusion.Internals.Execution.Producers;

namespace TestFusion.Internals.Execution;

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
    public async Task Execute()
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
        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            _sharedExecutionState.ResultState.FeatureCollectors[scenario.Name].MarkAsCompleted();
        }
        
        if (_sharedExecutionState.TestType == TestType.Feature)
            return;

        if (_sharedExecutionState.TestRunState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            foreach (var scenario in _sharedExecutionState.Scenarios)
            {
                var scenarioCollector = _sharedExecutionState.ResultState.LoadCollectors[scenario.Name];
                scenarioCollector.MarkPhaseAsCompleted(LoadTestPhase.Measurement);
                var scenarioResult = scenarioCollector.GetCurrentResult();
                if (scenario.AssertWhenDoneAction != null)
                {
                    try
                    {
                        var context = ContextFactory.CreateContext(_testFramework, "AssertWhenDoneAction");
                        scenario.AssertWhenDoneAction(context, new AssertScenarioStats(scenarioResult));
                    }
                    catch (Exception e)
                    {
                        _sharedExecutionState.TestRunState.FirstException = e;
                        scenarioCollector.AssertWhenDoneException(e);
                        scenarioCollector.SetStatus(ScenarioStatus.Failed);
                    }
                }
            }
        }
        
    }
}
