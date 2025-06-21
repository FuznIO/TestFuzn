using TestFusion.Internals.Results.Load;
using TestFusion.Internals.State;

namespace TestFusion.Internals.Execution.Consumers;

internal class ConsumerManager(
    SharedExecutionState sharedExecutionState,
    ScenarioExecutor scenarioExecutor,
    LoadResultsManager loadResultsManager)
{
    private Task _consumer;
    private readonly SharedExecutionState _sharedExecutionState = sharedExecutionState;
    private readonly ScenarioExecutor _scenarioExecutor = scenarioExecutor;
    private readonly LoadResultsManager _loadResultsManager = loadResultsManager;

    public void StartConsumers()
    {
        _consumer = Task.Run(Consume);
    }

    public async Task Consume()
    {
        await Parallel.ForEachAsync(_sharedExecutionState.ScenarioExecutionQueue.GetConsumingEnumerable(), async (scenarioExecution, cancellationToken) =>
        {
            if (_sharedExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            var scenario = _sharedExecutionState.Scenarios.Single(s => s.Name == scenarioExecution.ScenarioName);

            await _scenarioExecutor.Execute(scenario, scenarioExecution.IsWarmup);

            _sharedExecutionState.RemoveFromQueues(scenarioExecution);

            if (_sharedExecutionState.IsScenarioExecutionComplete(scenarioExecution.ScenarioName))
            {
                if (_sharedExecutionState.TestType == TestType.Feature)
                {
                    _sharedExecutionState.ScenarioResult.MarkAsCompleted();
                }
                else if (_sharedExecutionState.TestType == TestType.Load)
                {
                    _loadResultsManager.GetScenarioCollector(scenarioExecution.ScenarioName).MarkPhaseAsCompleted(TestPhase.Measurement);
                    var scenarioLoadResult = _loadResultsManager.GetScenarioCollector(scenarioExecution.ScenarioName).GetCurrentResult(true);
                    await _scenarioExecutor.WriteToSinks(scenario, scenarioLoadResult, true);
                }
            }
        });

        _sharedExecutionState.MarkConsumingCompleted();
    }

    public async Task WaitForConsumersToFinish()
    {
        await Task.WhenAll(_consumer);
        
        if (_sharedExecutionState.ExecutionStatus == ExecutionStatus.Running)
            _sharedExecutionState.ExecutionStatus = ExecutionStatus.Completed;
    }
}
