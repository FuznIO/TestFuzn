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
        _consumer = Task.Run(Consume, _testExecutionState.CancellationToken);
    }

    private async Task Consume()
    {
        var queue = _testExecutionState.MessageQueue;
        var cancellationToken = _testExecutionState.CancellationToken;

        await Parallel.ForEachAsync(
            queue.GetConsumingEnumerable(cancellationToken), 
            new ParallelOptions { MaxDegreeOfParallelism = int.MaxValue, CancellationToken = cancellationToken },
            async (message, ct) =>
        {
            await _executeScenarioMessageHandler.Execute(message);

            _testExecutionState.RemoveFromQueues(message);

            if (_testExecutionState.IsScenarioExecutionComplete(message.ScenarioName))
            {
                if (_testExecutionState.TestResult.TestType == TestType.Load)
                {
                    var completedCollector = _testExecutionState.LoadCollectors[message.ScenarioName];
                    completedCollector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, DateTime.UtcNow);
                    await _executeScenarioMessageHandler.WriteToSinksAndSnapshotCollector(_testExecutionState, message.Scenario, () => completedCollector.GetCurrentResult(true), true);
                }
            }
        });

        FinalizeLoadMeasurementPhase();

        if (_testExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
            _testExecutionState.MarkConsumingCompleted();
    }

    private void FinalizeLoadMeasurementPhase()
    {
        if (_testExecutionState.TestResult.TestType != TestType.Load)
            return;

        var now = DateTime.UtcNow;
        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var collector = _testExecutionState.LoadCollectors[scenario.Name];
            var result = collector.GetCurrentResult();
            if (!result.IsCompleted)
                collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, now);
        }
    }

    public async Task WaitForConsumersToFinish()
    {
        await Task.WhenAll(_consumer);
        
        if (_testExecutionState.ExecutionStatus == ExecutionStatus.Running)
            _testExecutionState.ExecutionStatus = ExecutionStatus.Completed;
    }
}
