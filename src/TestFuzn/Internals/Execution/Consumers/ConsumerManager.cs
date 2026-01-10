using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Consumers;

internal class ConsumerManager
{
    private Task _consumer;
    private readonly TestExecutionState _testExecutionState;
    private readonly ExecuteScenarioMessageHandler _scenarioExecutor;

    public ConsumerManager(
        TestExecutionState testExecutionState,
        ExecuteScenarioMessageHandler scenarioExecutor)
    {
        _testExecutionState = testExecutionState;
        _scenarioExecutor = scenarioExecutor;
    }

    public void StartConsumers()
    {
        _consumer = Task.Run(Consume);
    }

    public async Task Consume()
    {
        var queue = _testExecutionState.ExecutionState.MessageQueue;
        await Parallel.ForEachAsync(queue.GetConsumingEnumerable(), async (message, cancellationToken) =>
        {
            if (_testExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Stopped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            var scenario = _testExecutionState.Scenarios.Single(s => s.Name == message.ScenarioName);

            await _scenarioExecutor.Execute(message, scenario);

            _testExecutionState.RemoveFromQueues(message);

            if (_testExecutionState.IsScenarioExecutionComplete(message.ScenarioName))
            {
                _testExecutionState.ScenarioResultState.StandardCollectors[message.ScenarioName].MarkPhaseAsCompleted(StandardTestPhase.Execute);
                
                if (_testExecutionState.TestType == TestType.Load)
                {
                    _testExecutionState.ScenarioResultState.LoadCollectors[message.ScenarioName].MarkPhaseAsCompleted(LoadTestPhase.Measurement);
                    var scenarioLoadResult = _testExecutionState.ScenarioResultState.LoadCollectors[message.ScenarioName].GetCurrentResult(true);
                    await _scenarioExecutor.WriteToSinks(scenario, scenarioLoadResult, true);
                }
            }
        });

        _testExecutionState.MarkConsumingCompleted();
    }

    public async Task WaitForConsumersToFinish()
    {
        await Task.WhenAll(_consumer);
        
        if (_testExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Running)
            _testExecutionState.TestRunState.ExecutionStatus = ExecutionStatus.Completed;
    }
}
