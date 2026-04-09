using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class GradualLoadIncreaseHandler : ILoadHandler
{
    private readonly GradualLoadIncreaseConfiguration _configuration;
    private readonly Scenario _scenario;
    private readonly TestExecutionState _testExecutionState;

    public GradualLoadIncreaseHandler(
        GradualLoadIncreaseConfiguration configuration,
        Scenario scenario,
        TestExecutionState testExecutionState)
    {
        _configuration = configuration;
        _scenario = scenario;
        _testExecutionState = testExecutionState;
    }

    public async Task Execute()
    {
        var startRate = _configuration.StartRate;
        var endRate = _configuration.EndRate;
        var duration = _configuration.Duration;
        var rateSteps = endRate - startRate;
        var sleepBetweenQueueAdding = rateSteps > 0
            ? duration.Ticks / rateSteps
            : duration.Ticks;
        var currentRate = startRate;

        while (currentRate <= endRate
            && _testExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            for (int i = 0; i < currentRate; i++)
            {
                if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                    return;

                var message = new ExecuteScenarioMessage(_scenario, _configuration.IsWarmup);

                _testExecutionState.EnqueueScenarioExecution(message);
            }

            if (sleepBetweenQueueAdding > 0)
                await Task.Delay(TimeSpan.FromTicks(sleepBetweenQueueAdding), _testExecutionState.CancellationToken);

            currentRate++;
        }
    }
}
