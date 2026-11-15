using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class RandomLoadPerSecondHandler : ILoadHandler
{
    private readonly RandomLoadPerSecondConfiguration _configuration;
    private readonly Scenario _scenario;
    private readonly TestExecutionState _testExecutionState;

    public RandomLoadPerSecondHandler(
        RandomLoadPerSecondConfiguration configuration,
        Scenario scenario,
        TestExecutionState testExecutionState)
    {
        _configuration = configuration;
        _scenario = scenario;
        _testExecutionState = testExecutionState;
    }

    public async Task Execute()
    {
        Random random;
        if (_configuration.Seed.HasValue)
            random = new Random(_configuration.Seed.Value);
        else
            random = new Random();

        await TimedStarts.Run(
            StartTimes(_configuration, random),
            _configuration.Duration,
            _scenario,
            _configuration.IsWarmup,
            _testExecutionState);
    }

    internal static IEnumerable<TimeSpan> StartTimes(RandomLoadPerSecondConfiguration configuration, Random random)
    {
        if (configuration.MaxRate <= 0)
            yield break;

        for (long second = 0; second * TimeSpan.TicksPerSecond < configuration.Duration.Ticks; second++)
        {
            var rate = random.Next(configuration.MinRate, configuration.MaxRate + 1);
            if (configuration.WorkerCount > 1)
                rate = LoadSplit.Part(rate, configuration.WorkerIndex, configuration.WorkerCount, configuration.IsWarmup);

            for (var index = 0; index < rate; index++)
                yield return TimeSpan.FromTicks(second * TimeSpan.TicksPerSecond + index * TimeSpan.TicksPerSecond / rate);
        }
    }
}
