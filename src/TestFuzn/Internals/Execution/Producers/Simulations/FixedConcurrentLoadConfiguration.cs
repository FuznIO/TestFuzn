namespace FuznLabs.TestFuzn.Internals.Execution.Producers.Simulations;

internal class FixedConcurrentLoadConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public readonly int FixedCount;
    public readonly TimeSpan Duration;
    internal readonly int TotalCount;

    public FixedConcurrentLoadConfiguration(int fixedCount, TimeSpan duration)
    {
        FixedCount = fixedCount;
        Duration = duration;
    }

    public FixedConcurrentLoadConfiguration(int fixedCount, int totalCount)
    {
        FixedCount = fixedCount;
        TotalCount = totalCount;
    }

    public string GetDescription() => $"Fixed Concurrent Load - Fixed count: {FixedCount}, Total count: {TotalCount} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";
}
