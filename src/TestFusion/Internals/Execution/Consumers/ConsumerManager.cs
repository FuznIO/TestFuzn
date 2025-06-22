using TestFusion.Internals.Results.Load;
using TestFusion.Internals.State;

namespace TestFusion.Internals.Execution.Consumers;

internal class ConsumerManager
{
    private Task _consumer;
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly ExecuteScenarioMessageHandler _scenarioExecutor;

    public ConsumerManager(
        SharedExecutionState sharedExecutionState,
        ExecuteScenarioMessageHandler scenarioExecutor)
    {
        _sharedExecutionState = sharedExecutionState;
        _scenarioExecutor = scenarioExecutor;
    }

    public void StartConsumers()
    {
        _consumer = Task.Run(Consume);
    }

    public async Task Consume()
    {
        var queue = _sharedExecutionState.ExecutionState.MessageQueue;
        await Parallel.ForEachAsync(queue.GetConsumingEnumerable(), async (message, cancellationToken) =>
        {
            if (_sharedExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Stopped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            var scenario = _sharedExecutionState.Scenarios.Single(s => s.Name == message.ScenarioName);

            await _scenarioExecutor.Execute(message, scenario);

            _sharedExecutionState.RemoveFromQueues(message);

            if (_sharedExecutionState.IsScenarioExecutionComplete(message.ScenarioName))
            {
                if (_sharedExecutionState.TestType == TestType.Feature)
                {
                    _sharedExecutionState.ResultState.FeatureCollectors[message.ScenarioName].MarkAsCompleted();
                }
                else if (_sharedExecutionState.TestType == TestType.Load)
                {
                    _sharedExecutionState.ResultState.LoadCollectors[message.ScenarioName].MarkPhaseAsCompleted(LoadTestPhase.Measurement);
                    var scenarioLoadResult = _sharedExecutionState.ResultState.LoadCollectors[message.ScenarioName].GetCurrentResult(true);
                    await _scenarioExecutor.WriteToSinks(scenario, scenarioLoadResult, true);
                }
            }
        });

        _sharedExecutionState.MarkConsumingCompleted();
    }

    public async Task WaitForConsumersToFinish()
    {
        await Task.WhenAll(_consumer);
        
        if (_sharedExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Running)
            _sharedExecutionState.TestRunState.ExecutionStatus = ExecutionStatus.Completed;
    }
}
