using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class FixedConcurrentLoadHandler : ILoadHandler
{
    private static readonly TimeSpan StopCheck = TimeSpan.FromMilliseconds(100);

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
        var totalCount = _configuration.TotalCount;
        var slotFreed = _testExecutionState.ConstantSlotFreed[_scenario.Name];
        var stopwatch = Stopwatch.StartNew();
        var started = 0;

        while (_testExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            if (totalCount > 0)
            {
                if (started >= totalCount)
                    break;
            }
            else if (stopwatch.Elapsed >= duration)
            {
                break;
            }

            var addCount = _configuration.FixedCount - _testExecutionState.GetConstantQueueCount(_scenario.Name);
            if (totalCount > 0)
                addCount = Math.Min(addCount, totalCount - started);

            while (addCount > 0)
            {
                if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                    return;

                addCount--;
                started++;

                var message = new ExecuteScenarioMessage(_scenario, _configuration.IsWarmup);

                _testExecutionState.AddToConstantQueue(message);
                _testExecutionState.EnqueueScenarioExecution(message);
            }

            var wait = StopCheck;
            if (totalCount <= 0)
            {
                var remaining = duration - stopwatch.Elapsed;
                if (remaining < wait)
                    wait = remaining;
            }

            if (wait > TimeSpan.Zero)
                await slotFreed.WaitAsync(wait, _testExecutionState.CancellationToken);
        }
    }
}
