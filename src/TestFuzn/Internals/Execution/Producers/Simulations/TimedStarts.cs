using System.Diagnostics;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal static class TimedStarts
{
    private static readonly TimeSpan StopCheck = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ShortestWait = TimeSpan.FromMilliseconds(1);

    public static async Task Run(
        IEnumerable<TimeSpan> startTimes,
        TimeSpan duration,
        Scenario scenario,
        bool isWarmup,
        TestExecutionState testExecutionState)
    {
        var stopwatch = Stopwatch.StartNew();

        foreach (var startTime in startTimes)
        {
            if (startTime >= duration)
                break;

            if (!await WaitUntil(stopwatch, startTime, testExecutionState))
                return;

            testExecutionState.EnqueueScenarioExecution(new ExecuteScenarioMessage(scenario, isWarmup));
        }

        await WaitUntil(stopwatch, duration, testExecutionState);
    }

    private static async Task<bool> WaitUntil(Stopwatch stopwatch, TimeSpan target, TestExecutionState testExecutionState)
    {
        while (true)
        {
            if (testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                return false;

            var remaining = target - stopwatch.Elapsed;
            if (remaining < ShortestWait)
                return true;

            await Task.Delay(remaining < StopCheck ? remaining : StopCheck, testExecutionState.CancellationToken);
        }
    }
}
