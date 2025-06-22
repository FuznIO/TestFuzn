using TestFusion.Internals.State;

namespace TestFusion.Internals.Execution.Producers.Simulations;

internal class GradualLoadIncreaseHandler : ILoadHandler
{
    private readonly GradualLoadIncreaseConfiguration _configuration;
    private readonly string _scenarioName;
    private readonly SharedExecutionState _sharedExecutionState;

    public GradualLoadIncreaseHandler(
        GradualLoadIncreaseConfiguration configuration,
        string scenarioName,
        SharedExecutionState sharedExecutionState)
    {
        _configuration = configuration;
        _scenarioName = scenarioName;
        _sharedExecutionState = sharedExecutionState;
    }

    public async Task Execute()
    {
        var startRate = _configuration.StartRate;
        var endRate = _configuration.EndRate;
        var duration = _configuration.Duration;
        var sleepBetweenQueueAdding = duration.Ticks / (endRate - startRate);
        var currentRate = startRate;

        while (currentRate <= endRate
            && _sharedExecutionState.TestRunState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            for (int i = 0; i < currentRate; i++)
            {
                var message = new ExecuteScenarioMessage(_scenarioName, _configuration.IsWarmup);

                _sharedExecutionState.EnqueueScenarioExecution(message);
            }

            if (sleepBetweenQueueAdding > 0)
                await Task.Delay(TimeSpan.FromTicks(sleepBetweenQueueAdding));

            currentRate++;
        }
    }
}
