using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class GradualLoadIncreaseHandler : ILoadHandler
{
    private readonly GradualLoadIncreaseConfiguration _configuration;
    private readonly string _scenarioName;
    private readonly TestExecutionState _testExecutionState;

    public GradualLoadIncreaseHandler(
        GradualLoadIncreaseConfiguration configuration,
        string scenarioName,
        TestExecutionState testExecutionState)
    {
        _configuration = configuration;
        _scenarioName = scenarioName;
        _testExecutionState = testExecutionState;
    }

    public async Task Execute()
    {
        var startRate = _configuration.StartRate;
        var endRate = _configuration.EndRate;
        var duration = _configuration.Duration;
        var sleepBetweenQueueAdding = duration.Ticks / (endRate - startRate);
        var currentRate = startRate;

        while (currentRate <= endRate
            && _testExecutionState.TestRunState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            for (int i = 0; i < currentRate; i++)
            {
                var message = new ExecuteScenarioMessage(_scenarioName, _configuration.IsWarmup);

                _testExecutionState.EnqueueScenarioExecution(message);
            }

            if (sleepBetweenQueueAdding > 0)
                await Task.Delay(TimeSpan.FromTicks(sleepBetweenQueueAdding));

            currentRate++;
        }
    }
}
