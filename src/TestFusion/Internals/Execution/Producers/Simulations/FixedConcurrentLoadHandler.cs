using System.Diagnostics;
using TestFusion.Internals.State;

namespace TestFusion.Internals.Execution.Producers.Simulations;

internal class FixedConcurrentLoadHandler : ILoadHandler
{
    private readonly FixedConcurrentLoadConfiguration _configuration;
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly string _scenarioName;

    public FixedConcurrentLoadHandler(
        FixedConcurrentLoadConfiguration configuration,
        string scenarioName,
        SharedExecutionState sharedExecutionState
        )
    {
        _configuration = configuration;
        _scenarioName = scenarioName;
        _sharedExecutionState = sharedExecutionState;
    }

    public async Task Execute()
    {
        var duration = _configuration.Duration;
        var stopwatch = Stopwatch.StartNew();
        var totalCount = _configuration.TotalCount;
        var i = 0;

        while (_sharedExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            if (totalCount > 0)
            {
                if (i >= totalCount)
                    break;
            }
            else if (stopwatch.Elapsed >= duration)
            {
                break;
            }

            var addCount = _configuration.FixedCount - _sharedExecutionState.GetConstantQueueCount(_scenarioName);

            while (addCount > 0)
            {
                addCount--;
                i++;

                var execution = new ScenarioExecutionInfo(_scenarioName, _configuration.IsWarmup);

                _sharedExecutionState.AddToConstantQueue(execution);
                _sharedExecutionState.EnqueueScenarioExecution(execution);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
        stopwatch.Stop();
    }
}
