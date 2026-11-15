using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides the load statistics of a scenario's iterations or of a step's runs, for assertions.
/// </summary>
public class AssertStats
{
    /// <summary>
    /// Gets the number of iterations, or of step runs.
    /// </summary>
    public int Count { get; internal set; }

    /// <summary>
    /// Gets the number of iterations, or of step runs, per second.
    /// </summary>
    public int PerSecond { get; internal set; }

    /// <summary>
    /// Gets the shortest duration.
    /// </summary>
    public TimeSpan DurationMin { get; internal set; }

    /// <summary>
    /// Gets the longest duration.
    /// </summary>
    public TimeSpan DurationMax { get; internal set; }

    /// <summary>
    /// Gets the mean (average) duration.
    /// </summary>
    public TimeSpan DurationMean { get; internal set; }

    /// <summary>
    /// Gets the standard deviation of the durations.
    /// </summary>
    public TimeSpan DurationStandardDeviation { get; internal set; }

    /// <summary>
    /// Gets the median (50th percentile) duration.
    /// </summary>
    public TimeSpan DurationMedian { get; internal set; }

    /// <summary>
    /// Gets the 75th percentile duration.
    /// </summary>
    public TimeSpan DurationPercentile75 { get; internal set; }

    /// <summary>
    /// Gets the 90th percentile duration.
    /// </summary>
    public TimeSpan DurationPercentile90 { get; internal set; }

    /// <summary>
    /// Gets the 95th percentile duration.
    /// </summary>
    public TimeSpan DurationPercentile95 { get; internal set; }

    /// <summary>
    /// Gets the 99th percentile duration.
    /// </summary>
    public TimeSpan DurationPercentile99 { get; internal set; }

    internal AssertStats(Stats stats)
    {
        Count = stats.Count;
        PerSecond = stats.PerSecond;
        DurationMin = stats.DurationMin;
        DurationMax = stats.DurationMax;
        DurationMean = stats.DurationMean;
        DurationStandardDeviation = stats.DurationStandardDeviation;
        DurationMedian = stats.DurationMedian;
        DurationPercentile75 = stats.DurationPercentile75;
        DurationPercentile90 = stats.DurationPercentile90;
        DurationPercentile95 = stats.DurationPercentile95;
        DurationPercentile99 = stats.DurationPercentile99;
    }
}
