namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class LiveStats
{
    public static readonly LiveStats Empty = new LiveStats();

    public int Count { get; init; }
    public int PerSecond { get; init; }
    public TimeSpan DurationMin { get; init; }
    public TimeSpan DurationMax { get; init; }
    public TimeSpan DurationMean { get; init; }
    public TimeSpan DurationStandardDeviation { get; init; }
    public TimeSpan DurationMedian { get; init; }
    public TimeSpan DurationPercentile75 { get; init; }
    public TimeSpan DurationPercentile90 { get; init; }
    public TimeSpan DurationPercentile95 { get; init; }
    public TimeSpan DurationPercentile99 { get; init; }
}
