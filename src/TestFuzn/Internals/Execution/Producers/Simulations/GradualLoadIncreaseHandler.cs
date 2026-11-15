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
        await TimedStarts.Run(
            StartTimes(_configuration.StartRate, _configuration.EndRate, _configuration.Duration),
            _configuration.Duration,
            _scenario,
            _configuration.IsWarmup,
            _testExecutionState);
    }

    internal static IEnumerable<TimeSpan> StartTimes(int startRate, int endRate, TimeSpan duration)
    {
        var seconds = duration.TotalSeconds;
        if (seconds <= 0 || startRate + endRate <= 0)
            yield break;

        var total = (startRate + endRate) / 2.0 * seconds;
        var halfSlope = (endRate - startRate) / (2 * seconds);
        double rate = startRate;

        yield return TimeSpan.Zero;

        for (long index = 1; index < total - 1e-9; index++)
        {
            var discriminant = rate * rate + 4 * halfSlope * index;
            if (discriminant < 0)
                yield break;

            var at = 2 * index / (rate + Math.Sqrt(discriminant));
            yield return TimeSpan.FromTicks((long) (at * TimeSpan.TicksPerSecond));
        }
    }
}
