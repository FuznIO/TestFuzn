using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Consumers;

internal class ConsumerManager
{
    private Task _consumer = null!;
    private readonly IServiceProvider _serviceProvider;
    private TestExecutionState _testExecutionState = null!;
    private ExecuteScenarioMessageHandler _executeScenarioMessageHandler = null!;

    public ConsumerManager(
        IServiceProvider serviceProvider,
        TestExecutionState testExecutionState,
        ExecuteScenarioMessageHandler executeScenarioMessageHandler)
    {
        _serviceProvider = serviceProvider;
        _testExecutionState = testExecutionState;
        _executeScenarioMessageHandler = executeScenarioMessageHandler;
    }

    public void StartConsumers()
    {
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

            await _executeScenarioMessageHandler.Execute(message, scenario);

            _testExecutionState.RemoveFromQueues(message);

            if (_testExecutionState.IsScenarioExecutionComplete(message.ScenarioName))
            {
                if (_testExecutionState.TestResult.TestType == TestType.Load)
                {
                    _testExecutionState.LoadCollectors[message.ScenarioName].MarkPhaseAsCompleted(LoadTestPhase.Measurement, DateTime.UtcNow);
                    var scenarioLoadResult = _testExecutionState.LoadCollectors[message.ScenarioName].GetCurrentResult(true);
                    await _executeScenarioMessageHandler.WriteToSinks(_testExecutionState, scenario, scenarioLoadResult, true);
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
