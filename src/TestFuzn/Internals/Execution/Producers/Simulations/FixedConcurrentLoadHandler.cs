using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class FixedConcurrentLoadHandler : ILoadHandler
{
    private readonly FixedConcurrentLoadConfiguration _configuration;
    private readonly TestExecutionState _testExecutionState;
    private readonly Scenario _scenario;

    public FixedConcurrentLoadHandler(
        FixedConcurrentLoadConfiguration configuration,
        Scenario scenario,
        TestExecutionState testExecutionState
        )
    {
        _configuration = configuration;
        _scenario = scenario;
        _testExecutionState = testExecutionState;
    }

    public async Task Execute()
    {
        var duration = _configuration.Duration;
        var stopwatch = Stopwatch.StartNew();
        var totalCount = _configuration.TotalCount;
        var i = 0;

        while (_testExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
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

            var addCount = _configuration.FixedCount - _testExecutionState.GetConstantQueueCount(_scenario.Name);

            while (addCount > 0)
            {
                if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                    return;

                addCount--;
                i++;

                var message = new ExecuteScenarioMessage(_scenario, _configuration.IsWarmup);

                _testExecutionState.AddToConstantQueue(message);
                _testExecutionState.EnqueueScenarioExecution(message);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(10), _testExecutionState.CancellationToken);
        }
        stopwatch.Stop();
    }
}
