using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One coherent point-in-time view of a scenario's live load metrics, published by
/// <see cref="ScenarioLiveMetrics"/> and read by the dashboard's render loop. Every collection
/// on it is a fresh copy made under the writer's lock (the per-interval bucket-count vectors
/// inside <see cref="LatencyBucketSeries"/> are immutable and shared instead), so all values —
/// paired ok/fail deltas, the sample list, the derived series, error and step rows, the
/// newest-interval fields, progress numbers — describe the same tick and never change after
/// publication; a renderer can hold one instance across a whole frame. Planned duration,
/// progress and ETA are null when any configured simulation is count-based (indeterminate —
/// the dashboard shows elapsed only), except that once measurement has started with a
/// determinate measurement plan, progress and ETA run against the measurement segment alone
/// even when the warmup plan was indeterminate. The series lists are oldest-first with the
/// newest sample last, matching the sparkline widgets' newest-at-the-right convention, and
/// every series has exactly one entry per entry of <see cref="Samples"/>.
/// </summary>
internal sealed class LiveMetricsSnapshot
{
    /// <summary>The scenario this view belongs to.</summary>
    public string ScenarioName { get; init; } = string.Empty;

    /// <summary>The load test phase the scenario is in, inferred from the snapshot's phase timestamps.</summary>
    public LoadTestPhase Phase { get; init; }

    /// <summary>
    /// Display label for the current phase: "init", "warmup: {sim}" (or "warmup {i}/{n}: {sim}"),
    /// the current measurement simulation (prefixed "sim {i}/{n}: " when there are several),
    /// "cleanup", or "completed".
    /// </summary>
    public string PhaseLabel { get; init; } = string.Empty;

    /// <summary>Elapsed wall time since init started; frozen at the total run duration once cleanup has ended.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Total planned duration of all simulations (warmup + measurement); null when indeterminate.</summary>
    public TimeSpan? PlannedDuration { get; init; }

    /// <summary>Planned duration of the measurement segment alone; null when any measurement simulation is count-based.</summary>
    public TimeSpan? PlannedMeasurementDuration { get; init; }

    /// <summary>
    /// Fraction of the planned duration completed, 0..1. When the full plan is indeterminate
    /// but measurement has started with a determinate measurement plan, the fraction of the
    /// measurement segment alone; null while nothing determinate applies.
    /// </summary>
    public double? ProgressFraction { get; init; }

    /// <summary>
    /// Planned time remaining, floored at zero; measurement-segment-only under the same
    /// fallback as <see cref="ProgressFraction"/>, null while nothing determinate applies.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>True once the measurement phase has completed.</summary>
    public bool IsCompleted { get; init; }

    /// <summary>The scenario's status as the collector reports it (Passed until an assert fails it).</summary>
    public TestStatus Status { get; init; }

    /// <summary>
    /// The simulation plan the dashboard's timeline is drawn from: every simulation in producer
    /// order (the warmup segment first) with its compact label, its planned duration — null for
    /// a count-based simulation, which the timeline draws hatched — and its warmup flag. The
    /// same immutable list on every snapshot of a model (it is fixed at construction), so it is
    /// shared rather than copied; empty for a view built without a plan, such as the init
    /// placeholder the console manager shows before the simulations are known.
    /// </summary>
    public IReadOnlyList<SimulationPlanEntry> PlanEntries { get; init; } = Array.Empty<SimulationPlanEntry>();

    /// <summary>
    /// The message of the assert failure that failed the scenario — the first non-null of the
    /// collector's warming-up, while-running and when-done assert exceptions. Null while no
    /// assert has failed, so a Failed <see cref="Status"/> always has its reason here.
    /// </summary>
    public string? StatusDetail { get; init; }

    /// <summary>Successful requests recorded during the measurement phase.</summary>
    public int RequestCountOk { get; init; }

    /// <summary>Failed requests recorded during the measurement phase.</summary>
    public int RequestCountFailed { get; init; }

    /// <summary>Successful requests recorded during the warmup phase.</summary>
    public int WarmupRequestCountOk { get; init; }

    /// <summary>Failed requests recorded during the warmup phase.</summary>
    public int WarmupRequestCountFailed { get; init; }

    /// <summary>The measurement phase's cumulative-average requests per second, as the collector reports it.</summary>
    public int RequestsPerSecond { get; init; }

    /// <summary>Counts and response-time spread of successful measurement requests.</summary>
    public LiveStats Ok { get; init; } = LiveStats.Empty;

    /// <summary>Counts and response-time spread of failed measurement requests.</summary>
    public LiveStats Failed { get; init; } = LiveStats.Empty;

    /// <summary>
    /// All requests recorded during the newest sample's interval — its ok + failed deltas,
    /// warmup and measurement alike — so the interval's total is one number a threshold can
    /// read; zero before the first sample. Not <see cref="IntervalLatency"/>'s RequestCount,
    /// which counts only the interval's successful requests.
    /// </summary>
    public int IntervalRequestCount { get; init; }

    /// <summary>
    /// The failed share of the newest sample's interval, 0..1: the newest failed delta divided by
    /// <see cref="IntervalRequestCount"/> — the same denominator, so the two always agree. Zero
    /// when the newest interval had no requests and before the first sample.
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// The newest sample's current-interval requests per second (the last entry of
    /// <see cref="RequestsPerSecondSeries"/>); zero before the first sample. Distinct from
    /// <see cref="RequestsPerSecond"/>, the collector's cumulative average.
    /// </summary>
    public double IntervalRequestsPerSecond { get; init; }

    /// <summary>
    /// The response-time distribution of the successful requests recorded during the newest
    /// sample's interval — median, p95, p99 and the latency bucket counts the series below are
    /// taken from, as TimeSpans. <see cref="IntervalLatency.Empty"/> before the first sample and
    /// for an idle interval. The cumulative spread is on <see cref="Ok"/>.
    /// </summary>
    public IntervalLatency IntervalLatency { get; init; } = IntervalLatency.Empty;

    /// <summary>
    /// The scenario's declared thresholds with their live reading at this tick, in declaration
    /// order: each one's Current from the newest closed interval — the same numbers as the
    /// interval fields above — its Ok/Warning/Breached state and how long the current breach
    /// has lasted; see <see cref="ThresholdEvaluator"/> for the rules. Judged only during the
    /// measurement phase: before it, and before its first interval closes, every threshold
    /// reads Ok with a Current of 0, and after it the last measurement-phase readings are
    /// re-published unchanged, so the final view agrees with the verdict. Empty when the
    /// scenario declares none. The completion verdict is not here but on the cumulative
    /// <see cref="ScenarioLoadResult.ThresholdResults"/>.
    /// </summary>
    public IReadOnlyList<LiveThreshold> Thresholds { get; init; } = Array.Empty<LiveThreshold>();

    /// <summary>The ring buffer's per-second samples, oldest first, newest last.</summary>
    public IReadOnlyList<LiveMetricsSample> Samples { get; init; } = Array.Empty<LiveMetricsSample>();

    /// <summary>Per-sample current-interval requests per second, ready for a sparkline.</summary>
    public IReadOnlyList<double> RequestsPerSecondSeries { get; init; } = Array.Empty<double>();

    /// <summary>Per-sample ok request deltas, ready for a sparkline.</summary>
    public IReadOnlyList<double> OkDeltaSeries { get; init; } = Array.Empty<double>();

    /// <summary>Per-sample failed request deltas, ready for a sparkline.</summary>
    public IReadOnlyList<double> FailedDeltaSeries { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Per-sample per-interval Ok p95 in milliseconds, ready for a sparkline: each entry is the
    /// p95 of just the requests recorded during that sample's interval, so latency shifts show
    /// within one tick. The cumulative p95 for the latency panel is on <see cref="Ok"/>.
    /// </summary>
    public IReadOnlyList<double> ResponseTimePercentile95Series { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Per-sample per-interval Ok median in milliseconds, ready for a sparkline — the same
    /// intervals as <see cref="ResponseTimePercentile95Series"/>; zero for an idle interval.
    /// </summary>
    public IReadOnlyList<double> ResponseTimeMedianSeries { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Per-sample per-interval Ok p99 in milliseconds, ready for a sparkline — the same
    /// intervals as <see cref="ResponseTimePercentile95Series"/>; zero for an idle interval.
    /// </summary>
    public IReadOnlyList<double> ResponseTimePercentile99Series { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Per-sample latency bucket counts, ready for a heatmap: one vector per sample holding
    /// <see cref="LatencyBuckets.Count"/> counts in bucket order — how many of that interval's
    /// successful requests fell into each <see cref="LatencyBuckets"/> bucket (all zeros for an
    /// idle interval). Each vector is the interval's own immutable read-only list, shared
    /// rather than copied.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<int>> LatencyBucketSeries { get; init; } = Array.Empty<IReadOnlyList<int>>();

    /// <summary>
    /// Distinct errors across all steps (sub-steps included) with cumulative counts, most
    /// recently active first, bounded to <see cref="ScenarioLiveMetrics.ErrorCapacity"/> entries.
    /// </summary>
    public IReadOnlyList<LiveErrorEntry> Errors { get; init; } = Array.Empty<LiveErrorEntry>();

    /// <summary>
    /// How many distinct errors the model has seen across all steps, sub-steps included —
    /// every one its tracker holds, published or not — so the ticker can say how many more
    /// exist than <see cref="Errors"/> carries once that list is at its capacity. Never less
    /// than the list's length on a published view; zero on a view built without a model.
    /// </summary>
    public int DistinctErrorCount { get; init; }

    /// <summary>One row per top-level step, in scenario declaration order.</summary>
    public IReadOnlyList<LiveStepMetrics> Steps { get; init; } = Array.Empty<LiveStepMetrics>();
}
