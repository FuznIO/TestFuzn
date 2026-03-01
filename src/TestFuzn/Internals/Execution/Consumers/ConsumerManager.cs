using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Consumers;

internal class ConsumerManager
{
    private Task _consumer = null!;
    private TestExecutionState _testExecutionState = null!;
    private ExecuteScenarioMessageHandler _messageHandler = null!;

    public void StartConsumers(TestExecutionState testExecutionState,
        ExecuteScenarioMessageHandler messageHandler)
    {
        _testExecutionState = testExecutionState;
        _messageHandler = messageHandler;
        _consumer = Task.Run(Consume);
    }

    private async Task Consume()
    {
        var queue = _testExecutionState.ExecutionState.MessageQueue;

        await Parallel.ForEachAsync(queue.GetConsumingEnumerable(), async (message, cancellationToken) =>
        {
            if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            var scenario = _testExecutionState.Scenarios.Single(s => s.Name == message.ScenarioName);

            await _messageHandler.Execute(_testExecutionState, message, scenario);

            _testExecutionState.RemoveFromQueues(message);

            if (_testExecutionState.IsScenarioExecutionComplete(message.ScenarioName))
            {
                if (_testExecutionState.TestResult.TestType == TestType.Load)
                {
                    _testExecutionState.LoadCollectors[message.ScenarioName].MarkPhaseAsCompleted(LoadTestPhase.Measurement, DateTime.UtcNow);
                    var scenarioLoadResult = _testExecutionState.LoadCollectors[message.ScenarioName].GetCurrentResult(true);
                    await _messageHandler.WriteToSinks(_testExecutionState, scenario, scenarioLoadResult, true);
                }
            }
        });

        _testExecutionState.MarkConsumingCompleted();
    }

    public async Task WaitForConsumersToFinish()
    {
        await Task.WhenAll(_consumer);
        
        if (_testExecutionState.ExecutionStatus == ExecutionStatus.Running)
            _testExecutionState.ExecutionStatus = ExecutionStatus.Completed;
    }
}
