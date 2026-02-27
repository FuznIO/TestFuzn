using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class FixedLoadHandler : ILoadHandler
{
    private readonly FixedLoadConfiguration _configuration;
    private readonly string _scenarioName;
    private readonly TestExecutionState _testExecutionState;

    public FixedLoadHandler(
        FixedLoadConfiguration configuration,
        string scenarioName,
        TestExecutionState testExecutionState)
    {
        _configuration = configuration;
        _scenarioName = scenarioName;
        _testExecutionState = testExecutionState;
    }

    public async Task Execute()
    {
        var rate = _configuration.Rate;
        var interval = _configuration.Interval;
        var duration = _configuration.Duration;

        var totalIntervals = Math.Ceiling((double) duration.TotalSeconds / interval.TotalSeconds);
        var delayBetweenEnqueue = TimeSpan.FromTicks(interval.Ticks / rate);

        var intervalStopwatch = new Stopwatch();
        var currentIntervalIndex = 0;

        while (currentIntervalIndex < totalIntervals
            && _testExecutionState.ExecutionStatus != ExecutionStatus.Stopped)
        {
            currentIntervalIndex++;

            intervalStopwatch.Reset();
            intervalStopwatch.Start();

            for (int i = 0; i < rate; i++)
            {
                var message = new ExecuteScenarioMessage(_scenarioName, _configuration.IsWarmup);
                _testExecutionState.EnqueueScenarioExecution(message);

                var nextEnqueueTime = delayBetweenEnqueue.Ticks * (i + 1);
                var delay = TimeSpan.FromTicks(nextEnqueueTime - intervalStopwatch.ElapsedTicks);

                if (delay.TotalMilliseconds > 0)
                    await Task.Delay(delay);
            }

            intervalStopwatch.Stop();

            var intervalDelay = interval - intervalStopwatch.Elapsed;
            if (intervalDelay.TotalMilliseconds > 0)
            {
                await Task.Delay(intervalDelay);
            }
        }
    }
}
