using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Per-scenario live metrics model behind the load test dashboard: turns the cumulative
/// <see cref="ScenarioLoadResult"/> snapshots the 1 Hz console loop reads into per-second
/// deltas, progress/ETA numbers and ticker rows, and publishes them as one immutable
/// <see cref="LiveMetricsSnapshot"/> per tick. Pure model — no rendering.
///
/// Wiring contract: every snapshot fed to <see cref="Record"/> must be live — obtained with
/// <c>ScenarioLoadCollector.GetCurrentResult(forceRefresh: true)</c>. The collector's plain
/// GetCurrentResult() serves a cached result that stays frozen across warmup (its cache
/// refreshes only when measurements are recorded) and again once measurements stop, so a
/// non-forced feed would show init-era zeros for all of warmup, a false delta spike at the
/// first measurement tick, and never observe cleanup ending. For the same reason, after the
/// run loop exits and cleanup has completed, the wiring must call Record one final time with a
/// force-refreshed snapshot — that final tick is what lands the "completed" phase label and
/// freezes <see cref="LiveMetricsSnapshot.Duration"/> at the total run duration.
///
/// Thread safety is part of the contract: <see cref="Record"/> is called by the snapshot loop
/// (a single writer; an internal lock additionally serializes writers), while the render loop
/// reads <see cref="Current"/> from another thread at any rate. Each Record builds a completely
/// new snapshot — ring contents, series, error and step rows all copied into fresh arrays under
/// the lock — and publishes it with a volatile reference swap, so a reader always sees one
/// coherent tick (ok/fail deltas that belong together, series matching samples) and never a
/// torn or partially updated view. A snapshot obtained from <see cref="Current"/> never changes.
///
/// Delta math: ok/fail cumulatives combine measurement and warmup counters, so the series stays
/// continuous through the warmup-to-measurement transition. The first Record only establishes
/// the baseline (no interval exists yet, so no sample is appended). A Record whose timestamp
/// does not advance past the previous one appends no sample and leaves the baseline untouched,
/// so those requests land in the next interval instead of vanishing. A cumulative counter that
/// moves backwards (a reset) clamps that counter's delta to zero and re-baselines at the new
/// value.
/// </summary>
internal sealed class ScenarioLiveMetrics
{
    /// <summary>
    /// Ring buffer capacity: 5 minutes of history at 1 Hz. Braille sparklines pack two samples
    /// per character, so this covers sparkline panels up to 150 characters wide.
    /// </summary>
    public const int SampleCapacity = 300;

    /// <summary>
    /// Maximum distinct errors in a published snapshot: the 20 most recently active. The
    /// internal tracker keeps every distinct error (the collectors cap distinct errors per
    /// step), so recency is judged on real count changes even for errors currently unpublished.
    /// </summary>
    public const int ErrorCapacity = 20;

    private readonly object _gate = new object();
    private readonly SimulationPlan _plan;
    private readonly LiveMetricsSample[] _samples = new LiveMetricsSample[SampleCapacity];
    private readonly Dictionary<string, ErrorTrackerEntry> _errorTracker = new();
    private readonly Dictionary<string, (long Ok, long Failed)> _stepBaselines = new();
    private readonly Dictionary<string, double> _stepIntervalRates = new();
    private int _appendedSampleCount;
    private int _nextSampleIndex;
    private bool _hasBaseline;
    private DateTime _baselineTimestamp;
    private long _baselineOkCumulative;
    private long _baselineFailedCumulative;
    private long _errorSequence;
    private LiveMetricsSnapshot _current;

    /// <summary>The scenario this model tracks.</summary>
    public string ScenarioName { get; }

    public ScenarioLiveMetrics(string scenarioName, IReadOnlyList<ILoadConfiguration> simulations)
    {
        if (scenarioName == null)
            throw new ArgumentNullException(nameof(scenarioName), "Scenario name cannot be null.");
        if (simulations == null)
            throw new ArgumentNullException(nameof(simulations), "Simulations cannot be null.");

        ScenarioName = scenarioName;
        _plan = new SimulationPlan(simulations);

        double? initialProgress = null;
        TimeSpan? initialRemaining = null;
        if (_plan.PlannedDuration != null)
        {
            initialProgress = 0.0;
            initialRemaining = _plan.PlannedDuration.Value;
        }

        _current = new LiveMetricsSnapshot
        {
            ScenarioName = scenarioName,
            Phase = LoadTestPhase.Init,
            PhaseLabel = "init",
            PlannedDuration = _plan.PlannedDuration,
            PlannedMeasurementDuration = _plan.PlannedMeasurementDuration,
            ProgressFraction = initialProgress,
            EstimatedTimeRemaining = initialRemaining
        };
    }

    /// <summary>The latest published view. Never null; safe to read from any thread.</summary>
    public LiveMetricsSnapshot Current => Volatile.Read(ref _current);

    /// <summary>
    /// Ingests one cumulative snapshot from the scenario's load collector, taken at the given
    /// UTC time, and publishes a new <see cref="Current"/> view. The snapshot must be live —
    /// obtained with <c>ScenarioLoadCollector.GetCurrentResult(forceRefresh: true)</c> per the
    /// wiring contract in the class summary; a cached (non-forced) result freezes during
    /// warmup and after the last measurement. <paramref name="timestamp"/> must be UTC: a
    /// <see cref="DateTimeKind.Local"/> value throws, and Unspecified is treated as UTC.
    /// </summary>
    public void Record(ScenarioLoadResult snapshot, DateTime timestamp)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot), "Snapshot cannot be null.");
        if (timestamp.Kind == DateTimeKind.Local)
            throw new ArgumentException("Timestamp must be UTC; a DateTimeKind.Local timestamp is not accepted.", nameof(timestamp));

        lock (_gate)
        {
            var okStats = CopyStats(snapshot.Ok);
            var failedStats = CopyStats(snapshot.Failed);

            AppendSampleIfIntervalElapsed(snapshot, okStats, failedStats, timestamp);
            UpdateErrorTracker(snapshot, timestamp);

            var phase = InferPhase(snapshot);
            var published = new LiveMetricsSnapshot
            {
                ScenarioName = ScenarioName,
                Phase = phase,
                PhaseLabel = BuildPhaseLabel(phase, snapshot, timestamp),
                Duration = ComputeDuration(snapshot, timestamp),
                PlannedDuration = _plan.PlannedDuration,
                PlannedMeasurementDuration = _plan.PlannedMeasurementDuration,
                ProgressFraction = ComputeProgressFraction(phase, snapshot, timestamp),
                EstimatedTimeRemaining = ComputeEstimatedTimeRemaining(phase, snapshot, timestamp),
                IsCompleted = snapshot.IsCompleted,
                Status = snapshot.Status,
                StatusDetail = BuildStatusDetail(snapshot),
                RequestCountOk = okStats.RequestCount,
                RequestCountFailed = failedStats.RequestCount,
                WarmupRequestCountOk = snapshot.WarmupRequestCountOk,
                WarmupRequestCountFailed = snapshot.WarmupRequestCountFailed,
                RequestsPerSecond = snapshot.RequestsPerSecond,
                Ok = okStats,
                Failed = failedStats,
                Samples = MaterializeSamples(out var requestsPerSecondSeries, out var okDeltaSeries, out var failedDeltaSeries, out var percentile95Series),
                RequestsPerSecondSeries = requestsPerSecondSeries,
                OkDeltaSeries = okDeltaSeries,
                FailedDeltaSeries = failedDeltaSeries,
                ResponseTimePercentile95Series = percentile95Series,
                Errors = MaterializeErrors(),
                Steps = MaterializeSteps(snapshot, timestamp)
            };

            Volatile.Write(ref _current, published);
        }
    }

    private void AppendSampleIfIntervalElapsed(ScenarioLoadResult snapshot, LiveStats okStats, LiveStats failedStats, DateTime timestamp)
    {
        long okCumulative = (long)okStats.RequestCount + snapshot.WarmupRequestCountOk;
        long failedCumulative = (long)failedStats.RequestCount + snapshot.WarmupRequestCountFailed;

        if (!_hasBaseline)
        {
            _hasBaseline = true;
            _baselineTimestamp = timestamp;
            _baselineOkCumulative = okCumulative;
            _baselineFailedCumulative = failedCumulative;
            RebaselineSteps(snapshot);
            return;
        }

        var intervalSeconds = (timestamp - _baselineTimestamp).TotalSeconds;
        if (intervalSeconds <= 0)
            return;

        var okDelta = okCumulative - _baselineOkCumulative;
        if (okDelta < 0)
            okDelta = 0;
        var failedDelta = failedCumulative - _baselineFailedCumulative;
        if (failedDelta < 0)
            failedDelta = 0;

        var requestsPerSecond = (okDelta + failedDelta) / intervalSeconds;
        var sample = new LiveMetricsSample(timestamp, (int)Math.Min(okDelta, int.MaxValue), (int)Math.Min(failedDelta, int.MaxValue), requestsPerSecond, snapshot.IntervalResponseTimePercentile95);

        _samples[_nextSampleIndex] = sample;
        _nextSampleIndex = (_nextSampleIndex + 1) % SampleCapacity;
        if (_appendedSampleCount < int.MaxValue)
            _appendedSampleCount++;

        _baselineTimestamp = timestamp;
        _baselineOkCumulative = okCumulative;
        _baselineFailedCumulative = failedCumulative;
        UpdateStepIntervalRates(snapshot, intervalSeconds);
    }

    /// <summary>
    /// Establishes per-step baselines on the first Record, so the first closed interval's step
    /// deltas count only what happened inside it.
    /// </summary>
    private void RebaselineSteps(ScenarioLoadResult snapshot)
    {
        if (snapshot.Steps == null)
            return;

        foreach (var step in snapshot.Steps.Values)
        {
            if (step == null)
                continue;

            _stepBaselines[step.Name] = (CumulativeCount(step.Ok), CumulativeCount(step.Failed));
        }
    }

    /// <summary>
    /// Closes the interval for the per-step current rates: each top-level step's ok/failed
    /// deltas against its baseline, combined and divided once by the interval length. A step
    /// without a baseline (first seen mid-run) counts from zero; a backwards-moving counter
    /// clamps to zero and re-baselines, mirroring the scenario-level delta rules.
    /// </summary>
    private void UpdateStepIntervalRates(ScenarioLoadResult snapshot, double intervalSeconds)
    {
        if (snapshot.Steps == null)
            return;

        foreach (var step in snapshot.Steps.Values)
        {
            if (step == null)
                continue;

            var okCumulative = CumulativeCount(step.Ok);
            var failedCumulative = CumulativeCount(step.Failed);
            _stepBaselines.TryGetValue(step.Name, out var baseline);

            var okDelta = okCumulative - baseline.Ok;
            if (okDelta < 0)
                okDelta = 0;
            var failedDelta = failedCumulative - baseline.Failed;
            if (failedDelta < 0)
                failedDelta = 0;

            _stepIntervalRates[step.Name] = (okDelta + failedDelta) / intervalSeconds;
            _stepBaselines[step.Name] = (okCumulative, failedCumulative);
        }
    }

    private static long CumulativeCount(Stats stats)
    {
        if (stats == null)
            return 0;

        return stats.RequestCount;
    }

    private LiveMetricsSample[] MaterializeSamples(out double[] requestsPerSecondSeries, out double[] okDeltaSeries, out double[] failedDeltaSeries, out double[] percentile95Series)
    {
        var count = _appendedSampleCount < SampleCapacity ? _appendedSampleCount : SampleCapacity;
        var oldestIndex = _appendedSampleCount <= SampleCapacity ? 0 : _nextSampleIndex;

        var samples = new LiveMetricsSample[count];
        requestsPerSecondSeries = new double[count];
        okDeltaSeries = new double[count];
        failedDeltaSeries = new double[count];
        percentile95Series = new double[count];

        for (var index = 0; index < count; index++)
        {
            var sample = _samples[(oldestIndex + index) % SampleCapacity];
            samples[index] = sample;
            requestsPerSecondSeries[index] = sample.RequestsPerSecond;
            okDeltaSeries[index] = sample.OkDelta;
            failedDeltaSeries[index] = sample.FailedDelta;
            percentile95Series[index] = sample.ResponseTimePercentile95.TotalMilliseconds;
        }

        return samples;
    }

    private void UpdateErrorTracker(ScenarioLoadResult snapshot, DateTime timestamp)
    {
        if (snapshot.Steps == null)
            return;

        var aggregated = new Dictionary<string, (string StepName, string Message, long Count)>();
        CollectErrors(snapshot.Steps.Values, aggregated);

        foreach (var pair in aggregated)
        {
            if (_errorTracker.TryGetValue(pair.Key, out var tracked))
            {
                if (tracked.Count != pair.Value.Count)
                {
                    tracked.Count = pair.Value.Count;
                    tracked.LastSeen = timestamp;
                    _errorSequence++;
                    tracked.Sequence = _errorSequence;
                }
            }
            else
            {
                _errorSequence++;
                _errorTracker.Add(pair.Key, new ErrorTrackerEntry
                {
                    StepName = pair.Value.StepName,
                    Message = pair.Value.Message,
                    Count = pair.Value.Count,
                    LastSeen = timestamp,
                    Sequence = _errorSequence
                });
            }
        }
    }

    private static void CollectErrors(IEnumerable<StepLoadResult> steps, Dictionary<string, (string StepName, string Message, long Count)> aggregated)
    {
        if (steps == null)
            return;

        foreach (var step in steps)
        {
            if (step == null)
                continue;

            if (step.Errors != null)
            {
                foreach (var errorPair in step.Errors)
                {
                    if (errorPair.Value == null)
                        continue;

                    // Read the shared, still-mutating count exactly once.
                    long count = errorPair.Value.Count;
                    var key = step.Name + "\u001f" + errorPair.Key;
                    if (aggregated.TryGetValue(key, out var existing))
                        aggregated[key] = (existing.StepName, existing.Message, existing.Count + count);
                    else
                        aggregated[key] = (step.Name, errorPair.Key, count);
                }
            }

            if (step.Steps != null)
                CollectErrors(step.Steps, aggregated);
        }
    }

    private LiveErrorEntry[] MaterializeErrors()
    {
        if (_errorTracker.Count == 0)
            return Array.Empty<LiveErrorEntry>();

        return _errorTracker.Values
            .OrderByDescending(entry => entry.Sequence)
            .Take(ErrorCapacity)
            .Select(entry => new LiveErrorEntry
            {
                StepName = entry.StepName,
                Message = entry.Message,
                Count = (int)Math.Min(entry.Count, int.MaxValue),
                LastSeen = entry.LastSeen
            })
            .ToArray();
    }

    private LiveStepMetrics[] MaterializeSteps(ScenarioLoadResult snapshot, DateTime timestamp)
    {
        if (snapshot.Steps == null || snapshot.Steps.Count == 0)
            return Array.Empty<LiveStepMetrics>();

        var rows = new List<LiveStepMetrics>(snapshot.Steps.Count);
        foreach (var step in snapshot.Steps.Values)
        {
            if (step == null)
                continue;

            var ok = CopyStats(step.Ok);
            var failed = CopyStats(step.Failed);
            _stepIntervalRates.TryGetValue(step.Name, out var intervalRate);
            rows.Add(new LiveStepMetrics
            {
                Name = step.Name,
                RequestCountOk = ok.RequestCount,
                RequestCountFailed = failed.RequestCount,
                SkippedCount = step.SkippedCount,
                RequestsPerSecond = intervalRate,
                AverageRequestsPerSecond = ComputeAverageRequestsPerSecond(ok.RequestCount, failed.RequestCount, snapshot, timestamp),
                ResponseTimeMean = ok.ResponseTimeMean,
                ResponseTimePercentile95 = ok.ResponseTimePercentile95
            });
        }

        return rows.ToArray();
    }

    /// <summary>
    /// Lifetime-average rate over the measurement phase: the combined ok + failed count divided
    /// once by the elapsed measurement time — never a sum of separately rounded per-status
    /// rates, which loses every sub-1-rps step to rounding. Zero before measurement starts;
    /// frozen at the measurement end time once measurement has ended.
    /// </summary>
    private static double ComputeAverageRequestsPerSecond(int okCount, int failedCount, ScenarioLoadResult snapshot, DateTime timestamp)
    {
        if (snapshot.MeasurementStartTime == default)
            return 0.0;

        var end = snapshot.MeasurementEndTime != default ? snapshot.MeasurementEndTime : timestamp;
        var elapsedSeconds = (end - snapshot.MeasurementStartTime).TotalSeconds;
        if (elapsedSeconds <= 0)
            return 0.0;

        return (okCount + failedCount) / elapsedSeconds;
    }

    private static LiveStats CopyStats(Stats stats)
    {
        if (stats == null)
            return LiveStats.Empty;

        return new LiveStats
        {
            RequestCount = stats.RequestCount,
            RequestsPerSecond = stats.RequestsPerSecond,
            ResponseTimeMin = stats.ResponseTimeMin,
            ResponseTimeMax = stats.ResponseTimeMax,
            ResponseTimeMean = stats.ResponseTimeMean,
            ResponseTimeStandardDeviation = stats.ResponseTimeStandardDeviation,
            ResponseTimeMedian = stats.ResponseTimeMedian,
            ResponseTimePercentile75 = stats.ResponseTimePercentile75,
            ResponseTimePercentile95 = stats.ResponseTimePercentile95,
            ResponseTimePercentile99 = stats.ResponseTimePercentile99
        };
    }

    private static LoadTestPhase InferPhase(ScenarioLoadResult snapshot)
    {
        if (snapshot.CleanupEndTime != default || snapshot.IsCompleted || snapshot.MeasurementEndTime != default)
            return LoadTestPhase.Cleanup;
        if (snapshot.MeasurementStartTime != default)
            return LoadTestPhase.Measurement;
        if (snapshot.WarmupStartTime != default)
            return LoadTestPhase.Warmup;

        return LoadTestPhase.Init;
    }

    private string BuildPhaseLabel(LoadTestPhase phase, ScenarioLoadResult snapshot, DateTime timestamp)
    {
        switch (phase)
        {
            case LoadTestPhase.Cleanup:
                if (snapshot.CleanupEndTime != default)
                    return "completed";
                return "cleanup";
            case LoadTestPhase.Measurement:
                return _plan.MeasurementPhaseLabel(ClampNonNegative(timestamp - snapshot.MeasurementStartTime));
            case LoadTestPhase.Warmup:
                return _plan.WarmupPhaseLabel(ClampNonNegative(timestamp - snapshot.WarmupStartTime));
            default:
                return "init";
        }
    }

    private static TimeSpan ComputeDuration(ScenarioLoadResult snapshot, DateTime timestamp)
    {
        if (snapshot.InitStartTime == default)
            return TimeSpan.Zero;
        if (snapshot.CleanupEndTime != default)
            return ClampNonNegative(snapshot.CleanupEndTime - snapshot.InitStartTime);

        return ClampNonNegative(timestamp - snapshot.InitStartTime);
    }

    /// <summary>
    /// Planned time completed so far, for progress math. Warmup progress runs against the
    /// warmup segment's planned duration; once measurement starts the whole warmup segment
    /// counts as done regardless of its actual length, so progress lands exactly on the
    /// segment boundary at the transition. Only meaningful when the plan is determinate.
    /// </summary>
    private TimeSpan ComputePlannedTimeCompleted(LoadTestPhase phase, ScenarioLoadResult snapshot, DateTime timestamp)
    {
        var total = _plan.PlannedDuration.Value;
        switch (phase)
        {
            case LoadTestPhase.Cleanup:
                return total;
            case LoadTestPhase.Measurement:
            {
                var completed = _plan.PlannedWarmupDuration.Value + ClampNonNegative(timestamp - snapshot.MeasurementStartTime);
                if (completed > total)
                    return total;
                return completed;
            }
            case LoadTestPhase.Warmup:
            {
                var elapsedInWarmup = ClampNonNegative(timestamp - snapshot.WarmupStartTime);
                if (elapsedInWarmup > _plan.PlannedWarmupDuration.Value)
                    return _plan.PlannedWarmupDuration.Value;
                return elapsedInWarmup;
            }
            default:
                return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// The completed/total pair progress and ETA are computed from: the full plan when it is
    /// determinate; once measurement has started, the measurement segment alone when the
    /// warmup plan was indeterminate but the measurement plan is not (a count-based warmup
    /// must not keep a fully determinate measurement phase indeterminate); otherwise null —
    /// no fake numbers.
    /// </summary>
    private (TimeSpan Completed, TimeSpan Total)? ComputeProgressBasis(LoadTestPhase phase, ScenarioLoadResult snapshot, DateTime timestamp)
    {
        if (_plan.PlannedDuration != null)
            return (ComputePlannedTimeCompleted(phase, snapshot, timestamp), _plan.PlannedDuration.Value);

        if (_plan.PlannedMeasurementDuration != null && snapshot.MeasurementStartTime != default)
            return (ComputeMeasurementTimeCompleted(phase, snapshot, timestamp), _plan.PlannedMeasurementDuration.Value);

        return null;
    }

    /// <summary>
    /// Measurement-segment time completed for the measurement-only progress basis: the whole
    /// segment once the phase has moved past measurement, otherwise the elapsed measurement
    /// time capped at the segment's planned duration.
    /// </summary>
    private TimeSpan ComputeMeasurementTimeCompleted(LoadTestPhase phase, ScenarioLoadResult snapshot, DateTime timestamp)
    {
        var total = _plan.PlannedMeasurementDuration.Value;
        if (phase == LoadTestPhase.Cleanup)
            return total;

        var elapsedInMeasurement = ClampNonNegative(timestamp - snapshot.MeasurementStartTime);
        if (elapsedInMeasurement > total)
            return total;
        return elapsedInMeasurement;
    }

    private double? ComputeProgressFraction(LoadTestPhase phase, ScenarioLoadResult snapshot, DateTime timestamp)
    {
        var basis = ComputeProgressBasis(phase, snapshot, timestamp);
        if (basis == null)
            return null;

        if (basis.Value.Total <= TimeSpan.Zero)
            return 1.0;

        return basis.Value.Completed / basis.Value.Total;
    }

    private TimeSpan? ComputeEstimatedTimeRemaining(LoadTestPhase phase, ScenarioLoadResult snapshot, DateTime timestamp)
    {
        var basis = ComputeProgressBasis(phase, snapshot, timestamp);
        if (basis == null)
            return null;

        if (basis.Value.Total <= TimeSpan.Zero)
            return TimeSpan.Zero;

        return basis.Value.Total - basis.Value.Completed;
    }

    private static string? BuildStatusDetail(ScenarioLoadResult snapshot)
    {
        if (snapshot.AssertWhileWarmingUpException != null)
            return snapshot.AssertWhileWarmingUpException.Message;
        if (snapshot.AssertWhileRunningException != null)
            return snapshot.AssertWhileRunningException.Message;
        if (snapshot.AssertWhenDoneException != null)
            return snapshot.AssertWhenDoneException.Message;

        return null;
    }

    private static TimeSpan ClampNonNegative(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            return TimeSpan.Zero;

        return value;
    }

    private sealed class ErrorTrackerEntry
    {
        public string StepName = string.Empty;
        public string Message = string.Empty;
        public long Count;
        public DateTime LastSeen;
        public long Sequence;
    }
}
