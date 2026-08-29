namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One row of the dashboard's per-step live table, copied from a top-level
/// <see cref="Fuzn.TestFuzn.Contracts.Results.Load.StepLoadResult"/> at ingest (nested
/// sub-steps are not rows; their errors surface in the error ticker). Counts split
/// ok/failed/skipped so the layout can derive total and failure percentage.
/// <see cref="RequestsPerSecond"/> is the step's current rate over the most recently closed
/// sample interval; <see cref="AverageRequestsPerSecond"/> is its lifetime average across the
/// measurement phase. Response times are from the step's Ok stats. The series are the step's
/// own per-interval history, oldest first with the newest interval last, one entry per closed
/// interval and bounded to <see cref="ScenarioLiveMetrics.SampleCapacity"/> entries — for a
/// step the collector reports on every tick (every top-level step) they run in lockstep with
/// the snapshot's <see cref="LiveMetricsSnapshot.Samples"/>. They cover measurement executions
/// only: the collector counts a warmup iteration for the scenario and never records it per
/// step, so every step series is zero for the whole warmup phase — while the scenario's series
/// shows the warmup traffic — and picks up at the first measurement interval. Fresh copies per
/// published snapshot, never the live ring.
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

    /// <summary>The step's cumulative Ok p95 over the measurement phase; the dashboard renders the interval reading (the newest sample of <see cref="ResponseTimePercentile95Series"/>) and this cumulative value is kept for the model's completeness and future readers.</summary>
    public TimeSpan ResponseTimePercentile95 { get; init; }

    /// <summary>Per-interval current rate of this step, oldest first — its <see cref="RequestsPerSecond"/> for every closed interval, ready for a sparkline; zero across the warmup phase, like the delta series.</summary>
    public IReadOnlyList<double> RequestsPerSecondSeries { get; init; } = Array.Empty<double>();

    /// <summary>Per-interval successful executions of this step, oldest first; zero for every warmup interval (warmup iterations are counted for the scenario only, never per step) and for a measurement interval in which the step was idle or skipped.</summary>
    public IReadOnlyList<double> OkDeltaSeries { get; init; } = Array.Empty<double>();

    /// <summary>Per-interval failed executions of this step, oldest first; zero for every warmup interval (warmup iterations are counted for the scenario only, never per step) and for a measurement interval in which the step was idle or skipped.</summary>
    public IReadOnlyList<double> FailedDeltaSeries { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Per-interval Ok p95 of this step in milliseconds, oldest first: the p95 of just the
    /// step's successful executions recorded during that interval (its
    /// <see cref="Fuzn.TestFuzn.Contracts.Results.Load.StepLoadResult.IntervalLatency"/>), zero
    /// for an interval with none. The cumulative p95 is <see cref="ResponseTimePercentile95"/>.
    /// </summary>
    public IReadOnlyList<double> ResponseTimePercentile95Series { get; init; } = Array.Empty<double>();
}
