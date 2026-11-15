using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class FixedLoadHandler : ILoadHandler
{
    private readonly FixedLoadConfiguration _configuration;
    private readonly Scenario _scenario;
    private readonly TestExecutionState _testExecutionState;

    public FixedLoadHandler(
        FixedLoadConfiguration configuration,
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
            StartTimes(_configuration.Rate, _configuration.Interval),
            _configuration.Duration,
            _scenario,
            _configuration.IsWarmup,
            _testExecutionState);
    }

    internal static IEnumerable<TimeSpan> StartTimes(int rate, TimeSpan interval)
    {
        if (rate <= 0)
            yield break;

        for (long index = 0; ; index++)
            yield return TimeSpan.FromTicks((long) ((Int128) index * interval.Ticks / rate));
    }
}
