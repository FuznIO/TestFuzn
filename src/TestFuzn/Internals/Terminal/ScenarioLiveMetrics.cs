using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Per-scenario live metrics model behind the load test dashboard: turns the cumulative
/// <see cref="ScenarioLoadResult"/> snapshots the 1 Hz console loop reads into per-second
/// deltas (for the scenario and for each of its top-level steps), per-interval latency series,
/// progress/ETA numbers and ticker rows, and publishes them — with the plan's entries, for the
/// dashboard's timeline — as one immutable <see cref="LiveMetricsSnapshot"/> per tick. Pure
/// model — no rendering.
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
/// new snapshot — ring contents, the scenario's and every step's series, error and step rows all
/// copied into fresh arrays under the lock (only the immutable per-interval bucket-count vectors
/// are shared) — and publishes it with a volatile reference swap, so a reader always sees one
/// coherent tick (ok/fail deltas that belong together, series matching samples) and never a
/// torn or partially updated view. A snapshot obtained from <see cref="Current"/> never changes.
///
/// Delta math: ok/fail cumulatives combine measurement and warmup counters, so the series stays
/// continuous through the warmup-to-measurement transition. The first Record only establishes
/// the baseline (no interval exists yet, so no sample is appended). A Record whose timestamp
/// does not advance past the previous one appends no sample and leaves the baseline untouched,
/// so those requests land in the next interval instead of vanishing. A cumulative counter that
/// moves backwards (a reset) clamps that counter's delta to zero and re-baselines at the new
/// value. Per-step deltas are taken against per-step baselines with the same clamping and
/// re-baselining rules, closed on the same tick as the scenario's sample, but over measurement
/// executions only: <c>ScenarioLoadCollector.RecordWarmup</c> bumps the scenario's warmup
/// counters and never records into the step collectors, so while the scenario's series shows
/// the warmup traffic every step series is zero for the whole warmup phase and picks up at the
/// first measurement interval. A step that recorded nothing during a measurement interval —
/// idle, or skipped because an earlier step failed — gets zero deltas for that sample. Memory
/// is bounded: one <see cref="SampleCapacity"/>-entry ring for the scenario plus one per
/// top-level step, each allocated once when the step is first seen.
///
/// Thresholds: the scenario's declared thresholds are evaluated on every Record by a
/// <see cref="ThresholdEvaluator"/> against the newest closed interval — the same numbers the
/// snapshot's interval fields carry — and published as <see cref="LiveMetricsSnapshot.Thresholds"/>,
/// but judged only while the inferred phase is the measurement phase: before it (init, warmup)
/// and before the first interval closes every threshold is published in its placeholder state,
/// and once the phase has left measurement the last measurement-phase states are frozen and
/// re-published unchanged, so the final frame agrees with the verdict; the evaluator documents
/// these rules, the state rule and the breach duration. The verdict that fails a scenario is
/// not taken here: it is evaluated once at completion on the cumulative result, where
/// AssertWhenDone runs.
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
    private readonly ThresholdEvaluator _thresholdEvaluator;
    private readonly SampleRing _samples = new SampleRing();
    private readonly Dictionary<string, ErrorTrackerEntry> _errorTracker = new();
    private readonly Dictionary<string, StepTracker> _stepTrackers = new();
    private bool _hasBaseline;
    private DateTime _baselineTimestamp;
    private long _baselineOkCumulative;
    private long _baselineFailedCumulative;
    private long _errorSequence;
    private LiveMetricsSnapshot _current;

    /// <summary>The scenario this model tracks.</summary>
    public string ScenarioName { get; }

    /// <summary>A model for a scenario that declares no thresholds.</summary>
    public ScenarioLiveMetrics(string scenarioName, IReadOnlyList<ILoadConfiguration> simulations)
        : this(scenarioName, simulations, Array.Empty<Threshold>())
    {
    }

    /// <param name="scenarioName">The scenario this model tracks.</param>
    /// <param name="simulations">The scenario's ordered typed simulations, the plan progress and phase labels are computed from.</param>
    /// <param name="thresholds">The scenario's declared thresholds in declaration order, evaluated live on every Record; empty when it declares none.</param>
    public ScenarioLiveMetrics(string scenarioName, IReadOnlyList<ILoadConfiguration> simulations, IReadOnlyList<Threshold> thresholds)
    {
        if (scenarioName == null)
            throw new ArgumentNullException(nameof(scenarioName), "Scenario name cannot be null.");
        if (simulations == null)
            throw new ArgumentNullException(nameof(simulations), "Simulations cannot be null.");
        if (thresholds == null)
            throw new ArgumentNullException(nameof(thresholds), "Thresholds cannot be null.");

        ScenarioName = scenarioName;
        _plan = new SimulationPlan(simulations);
        _thresholdEvaluator = new ThresholdEvaluator(thresholds);

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
            EstimatedTimeRemaining = initialRemaining,
            PlanEntries = _plan.Entries,
            Thresholds = ThresholdEvaluator.InitialStates(thresholds)
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
            var newestInterval = ReadNewestInterval();
            var thresholds = _thresholdEvaluator.Evaluate(phase, _samples.Count > 0, newestInterval.RequestCount, newestInterval.ErrorRate, newestInterval.RequestsPerSecond, newestInterval.Latency, timestamp);
            MaterializeSeries(_samples, out var requestsPerSecondSeries, out var okDeltaSeries, out var failedDeltaSeries, out var percentile95Series);
            MaterializeLatencySeries(_samples, out var medianSeries, out var percentile99Series, out var bucketSeries);
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
                PlanEntries = _plan.Entries,
                StatusDetail = BuildStatusDetail(snapshot),
                RequestCountOk = okStats.RequestCount,
                RequestCountFailed = failedStats.RequestCount,
                WarmupRequestCountOk = snapshot.WarmupRequestCountOk,
                WarmupRequestCountFailed = snapshot.WarmupRequestCountFailed,
                RequestsPerSecond = snapshot.RequestsPerSecond,
                Ok = okStats,
                Failed = failedStats,
                IntervalRequestCount = newestInterval.RequestCount,
                ErrorRate = newestInterval.ErrorRate,
                IntervalRequestsPerSecond = newestInterval.RequestsPerSecond,
                IntervalLatency = newestInterval.Latency,
                Thresholds = thresholds,
                Samples = MaterializeSamples(_samples),
                RequestsPerSecondSeries = requestsPerSecondSeries,
                OkDeltaSeries = okDeltaSeries,
                FailedDeltaSeries = failedDeltaSeries,
                ResponseTimePercentile95Series = percentile95Series,
                ResponseTimeMedianSeries = medianSeries,
                ResponseTimePercentile99Series = percentile99Series,
                LatencyBucketSeries = bucketSeries,
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
        var intervalLatency = IntervalLatencyOf(snapshot);
        var sample = new LiveMetricsSample(timestamp, ClampToInt(okDelta), ClampToInt(failedDelta), requestsPerSecond, intervalLatency.ResponseTimePercentile95);
        _samples.Append(sample, intervalLatency);

        _baselineTimestamp = timestamp;
        _baselineOkCumulative = okCumulative;
        _baselineFailedCumulative = failedCumulative;
        CloseStepIntervals(snapshot, timestamp, intervalSeconds);
    }

    /// <summary>
    /// Establishes per-step baselines on the first Record, so the first closed interval's step
    /// deltas count only what happened inside it. This is also where a step's tracker — and
    /// its ring — is allocated, once.
    /// </summary>
    private void RebaselineSteps(ScenarioLoadResult snapshot)
    {
        if (snapshot.Steps == null)
            return;

        foreach (var step in snapshot.Steps.Values)
        {
            if (step == null)
                continue;

            var tracker = GetOrAddStepTracker(step.Name);
            tracker.OkBaseline = CumulativeCount(step.Ok);
            tracker.FailedBaseline = CumulativeCount(step.Failed);
        }
    }

    /// <summary>
    /// Closes the interval for every top-level step the snapshot reports: each step's ok/failed
    /// deltas against its baseline, combined and divided once by the interval length for its
    /// current rate, appended to the step's ring as one sample together with the step's own
    /// per-interval latency (its Ok p95; <see cref="IntervalLatency.Empty"/> when the step
    /// recorded no successful execution in the interval). A step without a baseline (first seen
    /// mid-run) counts from zero; a backwards-moving counter clamps to zero and re-baselines,
    /// mirroring the scenario-level delta rules.
    /// </summary>
    private void CloseStepIntervals(ScenarioLoadResult snapshot, DateTime timestamp, double intervalSeconds)
    {
        if (snapshot.Steps == null)
            return;

        foreach (var step in snapshot.Steps.Values)
        {
            if (step == null)
                continue;

            var tracker = GetOrAddStepTracker(step.Name);
            var okCumulative = CumulativeCount(step.Ok);
            var failedCumulative = CumulativeCount(step.Failed);

            var okDelta = okCumulative - tracker.OkBaseline;
            if (okDelta < 0)
                okDelta = 0;
            var failedDelta = failedCumulative - tracker.FailedBaseline;
            if (failedDelta < 0)
                failedDelta = 0;

            var requestsPerSecond = (okDelta + failedDelta) / intervalSeconds;
            var intervalLatency = IntervalLatencyOf(step);
            tracker.RequestsPerSecond = requestsPerSecond;
            tracker.Samples.Append(new LiveMetricsSample(timestamp, ClampToInt(okDelta), ClampToInt(failedDelta), requestsPerSecond, intervalLatency.ResponseTimePercentile95), intervalLatency);
            tracker.OkBaseline = okCumulative;
            tracker.FailedBaseline = failedCumulative;
        }
    }

    private StepTracker GetOrAddStepTracker(string stepName)
    {
        if (_stepTrackers.TryGetValue(stepName, out var tracker))
            return tracker;

        tracker = new StepTracker();
        _stepTrackers.Add(stepName, tracker);
        return tracker;
    }

    private static long CumulativeCount(Stats stats)
    {
        if (stats == null)
            return 0;

        return stats.RequestCount;
    }

    private static int ClampToInt(long value)
    {
        return (int)Math.Min(value, int.MaxValue);
    }

    /// <summary>The snapshot's per-interval Ok latency; <see cref="IntervalLatency.Empty"/> when the snapshot carries none.</summary>
    private static IntervalLatency IntervalLatencyOf(ScenarioLoadResult snapshot)
    {
        if (snapshot.IntervalLatency == null)
            return IntervalLatency.Empty;

        return snapshot.IntervalLatency;
    }

    /// <summary>The step's per-interval Ok latency; <see cref="IntervalLatency.Empty"/> when the step carries none.</summary>
    private static IntervalLatency IntervalLatencyOf(StepLoadResult step)
    {
        if (step.IntervalLatency == null)
            return IntervalLatency.Empty;

        return step.IntervalLatency;
    }

    /// <summary>
    /// The count-side view of the newest closed interval, for the snapshot's interval fields:
    /// all requests (ok + failed) of the newest sample, the failed share of that same count, the
    /// sample's rate and the interval's latency. All zero / Empty before the first sample.
    /// </summary>
    private NewestInterval ReadNewestInterval()
    {
        var count = _samples.Count;
        if (count == 0)
            return new NewestInterval(0, 0.0, 0.0, IntervalLatency.Empty);

        var newest = _samples.SampleAt(count - 1);
        var requestCount = ClampToInt((long)newest.OkDelta + newest.FailedDelta);
        var errorRate = 0.0;
        if (requestCount > 0)
            errorRate = (double)newest.FailedDelta / requestCount;

        return new NewestInterval(requestCount, errorRate, newest.RequestsPerSecond, _samples.IntervalLatencyAt(count - 1));
    }

    private static LiveMetricsSample[] MaterializeSamples(SampleRing ring)
    {
        var count = ring.Count;
        var samples = new LiveMetricsSample[count];
        for (var index = 0; index < count; index++)
            samples[index] = ring.SampleAt(index);

        return samples;
    }

    /// <summary>The four sparkline series a ring yields, oldest first — the scenario's and each step's alike.</summary>
    private static void MaterializeSeries(SampleRing ring, out double[] requestsPerSecondSeries, out double[] okDeltaSeries, out double[] failedDeltaSeries, out double[] percentile95Series)
    {
        var count = ring.Count;
        requestsPerSecondSeries = new double[count];
        okDeltaSeries = new double[count];
        failedDeltaSeries = new double[count];
        percentile95Series = new double[count];

        for (var index = 0; index < count; index++)
        {
            var sample = ring.SampleAt(index);
            requestsPerSecondSeries[index] = sample.RequestsPerSecond;
            okDeltaSeries[index] = sample.OkDelta;
            failedDeltaSeries[index] = sample.FailedDelta;
            percentile95Series[index] = sample.ResponseTimePercentile95.TotalMilliseconds;
        }
    }

    /// <summary>
    /// The per-interval latency series behind the scenario's median and p99 sparklines and the
    /// heatmap, oldest first. The bucket vectors are the intervals' own immutable read-only
    /// lists, shared rather than copied.
    /// </summary>
    private static void MaterializeLatencySeries(SampleRing ring, out double[] medianSeries, out double[] percentile99Series, out IReadOnlyList<int>[] bucketSeries)
    {
        var count = ring.Count;
        medianSeries = new double[count];
        percentile99Series = new double[count];
        bucketSeries = new IReadOnlyList<int>[count];

        for (var index = 0; index < count; index++)
        {
            var intervalLatency = ring.IntervalLatencyAt(index);
            medianSeries[index] = intervalLatency.ResponseTimeMedian.TotalMilliseconds;
            percentile99Series[index] = intervalLatency.ResponseTimePercentile99.TotalMilliseconds;
            bucketSeries[index] = intervalLatency.BucketCounts;
        }
    }

    /// <summary>
    /// Folds the snapshot's errors into the tracker: a new key is first (and last) seen now with
    /// a zero rate; an existing key refreshes its recency when its count changes, and — once its
    /// timestamp has advanced past the previous observation — takes the count delta since that
    /// observation over the actual gap as its rate, then re-baselines. A Record whose timestamp
    /// does not advance leaves the rate baseline alone, so those occurrences count in the next
    /// evaluation instead of vanishing (the same rule as the sample deltas).
    /// </summary>
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

                var gapSeconds = (timestamp - tracked.RateBaselineTimestamp).TotalSeconds;
                if (gapSeconds > 0)
                {
                    var countDelta = pair.Value.Count - tracked.RateBaselineCount;
                    if (countDelta < 0)
                        countDelta = 0;

                    tracked.RatePerSecond = countDelta / gapSeconds;
                    tracked.RateBaselineCount = pair.Value.Count;
                    tracked.RateBaselineTimestamp = timestamp;
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
                    FirstSeen = timestamp,
                    LastSeen = timestamp,
                    RatePerSecond = 0.0,
                    RateBaselineCount = pair.Value.Count,
                    RateBaselineTimestamp = timestamp,
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
                Count = ClampToInt(entry.Count),
                FirstSeen = entry.FirstSeen,
                LastSeen = entry.LastSeen,
                RatePerSecond = entry.RatePerSecond
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
            var requestsPerSecond = 0.0;
            double[] requestsPerSecondSeries = Array.Empty<double>();
            double[] okDeltaSeries = Array.Empty<double>();
            double[] failedDeltaSeries = Array.Empty<double>();
            double[] percentile95Series = Array.Empty<double>();
            if (_stepTrackers.TryGetValue(step.Name, out var tracker))
            {
                requestsPerSecond = tracker.RequestsPerSecond;
                MaterializeSeries(tracker.Samples, out requestsPerSecondSeries, out okDeltaSeries, out failedDeltaSeries, out percentile95Series);
            }

            rows.Add(new LiveStepMetrics
            {
                Name = step.Name,
                RequestCountOk = ok.RequestCount,
                RequestCountFailed = failed.RequestCount,
                SkippedCount = step.SkippedCount,
                RequestsPerSecond = requestsPerSecond,
                AverageRequestsPerSecond = ComputeAverageRequestsPerSecond(ok.RequestCount, failed.RequestCount, snapshot, timestamp),
                ResponseTimeMean = ok.ResponseTimeMean,
                ResponseTimePercentile95 = ok.ResponseTimePercentile95,
                RequestsPerSecondSeries = requestsPerSecondSeries,
                OkDeltaSeries = okDeltaSeries,
                FailedDeltaSeries = failedDeltaSeries,
                ResponseTimePercentile95Series = percentile95Series
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

    /// <summary>
    /// A <see cref="SampleCapacity"/>-entry ring of closed intervals — one per tick that closed
    /// an interval — holding each interval's sample and its per-interval latency side by side,
    /// appended together so the two can never drift apart. Allocated once; oldest-first
    /// indexing over the entries currently held. One ring serves the scenario, and one each of
    /// its top-level steps.
    /// </summary>
    private sealed class SampleRing
    {
        private readonly LiveMetricsSample[] _samples = new LiveMetricsSample[SampleCapacity];
        private readonly IntervalLatency[] _intervalLatencies = new IntervalLatency[SampleCapacity];
        private int _appendedCount;
        private int _nextIndex;

        /// <summary>Entries currently held: every appended entry until the ring fills, then <see cref="SampleCapacity"/>.</summary>
        public int Count => _appendedCount < SampleCapacity ? _appendedCount : SampleCapacity;

        public void Append(LiveMetricsSample sample, IntervalLatency intervalLatency)
        {
            _samples[_nextIndex] = sample;
            _intervalLatencies[_nextIndex] = intervalLatency;
            _nextIndex = (_nextIndex + 1) % SampleCapacity;
            if (_appendedCount < int.MaxValue)
                _appendedCount++;
        }

        /// <summary>The sample at the given oldest-first position, 0 being the oldest entry held.</summary>
        public LiveMetricsSample SampleAt(int index)
        {
            return _samples[PhysicalIndex(index)];
        }

        /// <summary>The per-interval latency of the entry at the given oldest-first position.</summary>
        public IntervalLatency IntervalLatencyAt(int index)
        {
            return _intervalLatencies[PhysicalIndex(index)];
        }

        private int PhysicalIndex(int index)
        {
            var oldestIndex = _appendedCount <= SampleCapacity ? 0 : _nextIndex;
            return (oldestIndex + index) % SampleCapacity;
        }
    }

    /// <summary>Per-step delta state: the baselines the next interval's deltas count from, the current-interval rate and the step's own ring.</summary>
    private sealed class StepTracker
    {
        public long OkBaseline;
        public long FailedBaseline;
        public double RequestsPerSecond;
        public readonly SampleRing Samples = new SampleRing();
    }

    /// <summary>The newest closed interval's count-side values, read once per Record for the snapshot's interval fields.</summary>
    private readonly struct NewestInterval
    {
        public int RequestCount { get; }
        public double ErrorRate { get; }
        public double RequestsPerSecond { get; }
        public IntervalLatency Latency { get; }

        public NewestInterval(int requestCount, double errorRate, double requestsPerSecond, IntervalLatency latency)
        {
            RequestCount = requestCount;
            ErrorRate = errorRate;
            RequestsPerSecond = requestsPerSecond;
            Latency = latency;
        }
    }

    private sealed class ErrorTrackerEntry
    {
        public string StepName = string.Empty;
        public string Message = string.Empty;
        public long Count;
        public DateTime FirstSeen;
        public DateTime LastSeen;
        public double RatePerSecond;
        /// <summary>The count and time of the previous rate evaluation — the "previous sample" the next rate's delta and gap are taken against.</summary>
        public long RateBaselineCount;
        public DateTime RateBaselineTimestamp;
        public long Sequence;
    }
}
