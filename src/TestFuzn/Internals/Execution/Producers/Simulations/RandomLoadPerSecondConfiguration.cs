using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class RandomLoadPerSecondConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public int MinRate { get; }
    public int MaxRate { get; }
    public TimeSpan Duration { get; }

    // Every worker draws the same total rate from the same seed and runs its part of it,
    // so the cluster total keeps the single-node distribution.
    public int? Seed { get; private init; }
    public int WorkerIndex { get; private init; }
    public int WorkerCount { get; private init; } = 1;

    public RandomLoadPerSecondConfiguration(int minRate, int maxRate, TimeSpan duration)
    {
        SimulationGuard.NotNegative(minRate, nameof(minRate));
        SimulationGuard.NotNegative(maxRate, nameof(maxRate));
        SimulationGuard.NotAbove(minRate, maxRate, nameof(minRate), nameof(maxRate));
        SimulationGuard.Positive(duration, nameof(duration));
        MinRate = minRate;
        MaxRate = maxRate;
        Duration = duration;
    }

    public string GetDescription() => $"Random Load per Second - Min rate: {MinRate}, Max rate: {MaxRate}, Duration: {Duration:g} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";

    public ILoadConfiguration Split(int workerIndex, int workerCount, int seed)
    {
        return new RandomLoadPerSecondConfiguration(MinRate, MaxRate, Duration)
        {
            IsWarmup = IsWarmup,
            Seed = seed,
            WorkerIndex = workerIndex,
            WorkerCount = workerCount
        };
    }
}
