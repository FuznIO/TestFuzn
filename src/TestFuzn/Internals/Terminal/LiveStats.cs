namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class LiveStats
{
    public static readonly LiveStats Empty = new LiveStats();

    public int RequestCount { get; init; }
    public int RequestsPerSecond { get; init; }
    public TimeSpan ResponseTimeMin { get; init; }
    public TimeSpan ResponseTimeMax { get; init; }
    public TimeSpan ResponseTimeMean { get; init; }
    public TimeSpan ResponseTimeStandardDeviation { get; init; }
    public TimeSpan ResponseTimeMedian { get; init; }
    public TimeSpan ResponseTimePercentile75 { get; init; }
    public TimeSpan ResponseTimePercentile95 { get; init; }
    public TimeSpan ResponseTimePercentile99 { get; init; }
}
