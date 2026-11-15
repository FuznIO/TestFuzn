namespace Fuzn.TestFuzn.Contracts.Results.Load;

internal class Stats
{
    public int Count { get; internal set; }
    public int PerSecond { get; internal set; }
    public TimeSpan TotalExecutionDuration { get; internal set; }
    public TimeSpan DurationMin { get; internal set; }
    public TimeSpan DurationMax { get; internal set; }
    public TimeSpan DurationMean { get; internal set; }
    public TimeSpan DurationStandardDeviation { get; internal set; }
    public TimeSpan DurationMedian { get; internal set; }
    public TimeSpan DurationPercentile75 { get; internal set; }
    public TimeSpan DurationPercentile90 { get; internal set; }
    public TimeSpan DurationPercentile95 { get; internal set; }
    public TimeSpan DurationPercentile99 { get; internal set; }
}
