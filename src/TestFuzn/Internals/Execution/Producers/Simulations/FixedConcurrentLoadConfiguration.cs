using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class FixedConcurrentLoadConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public readonly int FixedCount;
    public readonly TimeSpan Duration;
    internal readonly int TotalCount;

    public FixedConcurrentLoadConfiguration(int fixedCount, TimeSpan duration)
    {
        SimulationGuard.NotNegative(fixedCount, "count");
        SimulationGuard.Positive(duration, nameof(duration));
        FixedCount = fixedCount;
        Duration = duration;
    }

    public FixedConcurrentLoadConfiguration(int fixedCount, int totalCount)
    {
        FixedCount = fixedCount;
        TotalCount = totalCount;
        Duration = TimeSpan.Zero;
    }

    private FixedConcurrentLoadConfiguration(int fixedCount, TimeSpan duration, int totalCount)
    {
        FixedCount = fixedCount;
        Duration = duration;
        TotalCount = totalCount;
    }

    public ILoadConfiguration Split(int workerIndex, int workerCount, int seed)
    {
        return new FixedConcurrentLoadConfiguration(
            LoadSplit.Part(FixedCount, workerIndex, workerCount, IsWarmup),
            Duration,
            LoadSplit.Part(TotalCount, workerIndex, workerCount, IsWarmup)) { IsWarmup = IsWarmup };
    }

    public string GetDescription() => TotalCount > 0
        ? $"Fixed Concurrent Load - Count: {FixedCount}, Total count: {TotalCount} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}"
        : $"Fixed Concurrent Load - Count: {FixedCount}, Duration: {Duration:g} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";
}
