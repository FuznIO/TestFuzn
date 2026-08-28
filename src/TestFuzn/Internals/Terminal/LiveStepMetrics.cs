namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One row of the dashboard's per-step live table, copied from a top-level
/// <see cref="Fuzn.TestFuzn.Contracts.Results.Load.StepLoadResult"/> at ingest. Counts split
/// ok/failed/skipped so the layout can derive total and failure percentage.
/// <see cref="RequestsPerSecond"/> is the step's current rate over the most recently closed
/// sample interval; <see cref="AverageRequestsPerSecond"/> is its lifetime average across the
/// measurement phase. Response times are from the step's Ok stats.
/// </summary>
internal sealed class LiveStepMetrics
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Successful requests recorded for this step during the measurement phase.</summary>
    public int RequestCountOk { get; init; }

    /// <summary>Failed requests recorded for this step during the measurement phase.</summary>
    public int RequestCountFailed { get; init; }

    /// <summary>Iterations in which this step was skipped.</summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Current-interval rate: the step's combined ok + failed request delta over the most
    /// recently closed sample interval, divided by that interval's length in seconds. Zero
    /// until the first interval closes.
    /// </summary>
    public double RequestsPerSecond { get; init; }

    /// <summary>
    /// Lifetime-average rate: the step's combined ok + failed request count divided once by the
    /// elapsed measurement time (never a sum of separately rounded per-status rates). Zero
    /// before measurement starts; frozen once measurement ends.
    /// </summary>
    public double AverageRequestsPerSecond { get; init; }

    public TimeSpan ResponseTimeMean { get; init; }
    public TimeSpan ResponseTimePercentile95 { get; init; }
}
