namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// An immutable copy of the display fields of a <see cref="Fuzn.TestFuzn.Contracts.Results.Load.Stats"/>
/// snapshot — counts, cumulative-average rate, and the full response-time percentile spread the
/// dashboard's latency panel shows. Copied at ingest so a published
/// <see cref="LiveMetricsSnapshot"/> shares no mutable objects with the collector side.
/// Property names mirror <see cref="Fuzn.TestFuzn.Contracts.Results.Load.Stats"/>.
/// </summary>
internal sealed class LiveStats
{
    /// <summary>A stats block with every value zero, used before any requests are recorded.</summary>
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
