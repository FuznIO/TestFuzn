using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn.Internals.Thresholds;

/// <summary>
/// Evaluates a scenario's declared <see cref="Threshold"/>s — live on every dashboard sample and
/// once as the verdict at completion — and is the one place the threshold metrics are read and
/// compared, so the live state and the verdict can never disagree on what a metric is or when
/// it is violated. How a threshold or a result is described is <see cref="ThresholdFormat"/>'s.
///
/// Live (the instance side): <see cref="Evaluate"/> runs on every sample
/// <see cref="Fuzn.TestFuzn.Internals.Terminal.ScenarioLiveMetrics.Record"/> ingests — under the
/// model's writer lock, on the sampler thread — and reads each threshold's Current from the
/// newest closed interval, the same numbers the dashboard's interval tiles show: the interval's
/// Ok mean, p95 and p99 (<see cref="IntervalLatency"/>), its failed share
/// (<see cref="Fuzn.TestFuzn.Internals.Terminal.LiveMetricsSnapshot.ErrorRate"/>) and its rate
/// (<see cref="Fuzn.TestFuzn.Internals.Terminal.LiveMetricsSnapshot.IntervalRequestsPerSecond"/>,
/// successful and failed requests alike). Three phase rules frame the evaluation:
/// (1) nothing is judged before the measurement phase — during init and warmup every threshold
/// is published in its placeholder state, Ok with a Current of 0 and no breach, because warmup
/// traffic is in the interval fields but never in the verdict (a 5 rps warmup is no breach of a
/// 50 rps minimum, a warmup failure none of the error rate) — nor before the first interval has
/// closed (the baseline Record);
/// (2) during measurement every closed interval is judged as it is: an interval with no
/// requests at all (an idle second) reads a rate of 0, which breaches a rate minimum — a
/// stalled target is a breach — while the error rate, with no request to take a share of,
/// reads Ok rather than a false breach, and the response-time thresholds read a Current of 0
/// from the Empty interval latency, which is Ok;
/// (3) once the phase has left measurement — the drained final sample and everything after —
/// the last measurement-phase states are frozen and re-published unchanged (same Current, State
/// and BreachedFor), so the last frame still shows the breach the verdict is about to report
/// instead of a drained, all-green interval: the verdict is the authority, and the live view
/// never contradicts it there.
/// State rule: for a maximum-style threshold (<see cref="ThresholdComparison.LessThanOrEqualTo"/>
/// — the response times and the error rate) the state is Breached when Current &gt; Limit,
/// Warning when Current ≥ <see cref="WarningFraction"/> × Limit (80 % of the limit) without
/// exceeding it, else Ok; for a minimum-style threshold (<see cref="ThresholdComparison.GreaterThanOrEqualTo"/>
/// — requests per second) the exact mirror: Breached when Current &lt; Limit, Warning when
/// Current ≤ Limit / <see cref="WarningFraction"/> (125 % of the limit) without falling below
/// it, else Ok. A zero limit has no warning band: the state is Ok until the limit is crossed.
/// BreachedFor is the wall-clock duration since the sample at which the current uninterrupted
/// Breached run began, measured on the sample timestamps passed to Evaluate — never the clock —
/// so it is zero on the sample a breach begins and resets to zero the moment the state leaves
/// Breached.
///
/// Verdict (the static side): <see cref="EvaluateVerdict"/> runs once at completion, where the
/// AssertWhenDone callback runs, on the cumulative <see cref="ScenarioLoadResult"/>: the
/// response times from the cumulative Ok <see cref="Stats"/>, the error rate as the failed
/// request count over the ok + failed request count of the measurement phase (warmup excluded,
/// as the cumulative statistics are; 0 when there were no requests), and the request rate as
/// the cumulative Ok <see cref="Stats.RequestsPerSecond"/> the AssertStats vocabulary exposes.
/// A threshold passes when its comparison holds — the same test as the live Breached rule, so
/// the verdict and a breach agree; a value equal to the limit passes.
/// </summary>
internal sealed class ThresholdEvaluator
{
    /// <summary>The share of a limit at which the live state turns Warning: 80 % of a maximum, or a minimum divided by it (125 %).</summary>
    public const double WarningFraction = 0.8;

    private readonly IReadOnlyList<Threshold> _thresholds;
    private readonly LiveThreshold[] _placeholder;
    private readonly DateTime?[] _breachStartedAt;
    private LiveThreshold[]? _lastMeasurementStates;

    public ThresholdEvaluator(IReadOnlyList<Threshold> thresholds)
    {
        if (thresholds == null)
            throw new ArgumentNullException(nameof(thresholds), "Thresholds cannot be null.");

        _thresholds = thresholds;
        _placeholder = InitialStates(thresholds);
        _breachStartedAt = new DateTime?[thresholds.Count];
    }

    /// <summary>The thresholds evaluated, in declaration order.</summary>
    public IReadOnlyList<Threshold> Thresholds => _thresholds;

    /// <summary>
    /// The placeholder view — every threshold Ok with a Current of 0 and no breach, in
    /// declaration order — published before the measurement phase and before its first
    /// interval closes.
    /// </summary>
    public static LiveThreshold[] InitialStates(IReadOnlyList<Threshold> thresholds)
    {
        if (thresholds == null)
            throw new ArgumentNullException(nameof(thresholds), "Thresholds cannot be null.");

        var states = new LiveThreshold[thresholds.Count];
        for (var index = 0; index < states.Length; index++)
            states[index] = new LiveThreshold(thresholds[index], 0.0, ThresholdState.Ok, TimeSpan.Zero);

        return states;
    }

    /// <summary>
    /// Evaluates every threshold for the sample taken at <paramref name="timestamp"/> per the
    /// live rules in the class summary. <paramref name="phase"/> is the phase the model
    /// inferred for the sample and <paramref name="hasClosedInterval"/> whether any interval
    /// has closed yet (false on the baseline Record); the remaining arguments describe the
    /// newest closed interval — its total request count (ok + failed), its failed share, its
    /// rate and its Ok response-time distribution, the snapshot's interval fields. Returns the
    /// states in declaration order (empty when there are no thresholds); the placeholder and
    /// the frozen states are the same array instance sample after sample, which is safe as
    /// neither an array nor its entries ever change once published. Not thread-safe: the model
    /// calls it under its writer lock.
    /// </summary>
    public LiveThreshold[] Evaluate(LoadTestPhase phase, bool hasClosedInterval, int intervalRequestCount, double errorRate, double intervalRequestsPerSecond, IntervalLatency intervalLatency, DateTime timestamp)
    {
        if (intervalLatency == null)
            throw new ArgumentNullException(nameof(intervalLatency), "Interval latency cannot be null.");

        // Rule 1: nothing is judged before measurement — the interval fields carry warmup
        // traffic the verdict never sees — so init and warmup publish the placeholder.
        if (phase == LoadTestPhase.Init || phase == LoadTestPhase.Warmup)
            return _placeholder;

        // Rule 3: once the phase has left measurement the last measurement-phase states stand,
        // the drained final sample included; a measurement phase that never had a sample
        // leaves the placeholder standing.
        if (phase != LoadTestPhase.Measurement)
        {
            if (_lastMeasurementStates != null)
                return _lastMeasurementStates;

            return _placeholder;
        }

        // Rule 1, second half: until the first interval closes there is nothing to judge.
        if (!hasClosedInterval)
        {
            _lastMeasurementStates = _placeholder;
            return _placeholder;
        }

        var states = new LiveThreshold[_thresholds.Count];
        for (var index = 0; index < states.Length; index++)
        {
            var threshold = _thresholds[index];
            var current = IntervalCurrentOf(threshold, errorRate, intervalRequestsPerSecond, intervalLatency);
            var state = StateOf(threshold, current);

            // Rule 2: an interval without any request has no failed share to judge — Ok for the
            // error rate, not a false breach — while its zero rate is judged as it is: a stalled
            // target breaches a rate minimum.
            if (intervalRequestCount == 0 && threshold.Metric == ThresholdMetric.ErrorRate)
                state = ThresholdState.Ok;

            var breachedFor = TimeSpan.Zero;
            if (state == ThresholdState.Breached)
            {
                // Read the element into a local: flow analysis does not track array elements.
                var breachStartedAt = _breachStartedAt[index];
                if (breachStartedAt == null)
                {
                    breachStartedAt = timestamp;
                    _breachStartedAt[index] = timestamp;
                }

                breachedFor = timestamp - breachStartedAt.Value;
                if (breachedFor < TimeSpan.Zero)
                    breachedFor = TimeSpan.Zero;
            }
            else
            {
                _breachStartedAt[index] = null;
            }

            states[index] = new LiveThreshold(threshold, current, state, breachedFor);
        }

        _lastMeasurementStates = states;
        return states;
    }

    /// <summary>
    /// The live state of a threshold for the given current value — the state rule in the class
    /// summary, without the phase rules and the idle-interval error-rate exception
    /// <see cref="Evaluate"/> applies on top of it.
    /// </summary>
    public static ThresholdState StateOf(Threshold threshold, double current)
    {
        if (threshold == null)
            throw new ArgumentNullException(nameof(threshold), "Threshold cannot be null.");

        if (IsViolated(threshold, current))
            return ThresholdState.Breached;

        // A zero limit has no warning band.
        if (threshold.Limit <= 0)
            return ThresholdState.Ok;

        if (threshold.Comparison == ThresholdComparison.LessThanOrEqualTo)
        {
            if (current >= WarningFraction * threshold.Limit)
                return ThresholdState.Warning;

            return ThresholdState.Ok;
        }

        if (current <= threshold.Limit / WarningFraction)
            return ThresholdState.Warning;

        return ThresholdState.Ok;
    }

    /// <summary>Whether the value fails the threshold's comparison: above a maximum, below a minimum; a value equal to the limit holds.</summary>
    public static bool IsViolated(Threshold threshold, double current)
    {
        if (threshold == null)
            throw new ArgumentNullException(nameof(threshold), "Threshold cannot be null.");

        if (threshold.Comparison == ThresholdComparison.LessThanOrEqualTo)
            return current > threshold.Limit;

        return current < threshold.Limit;
    }

    /// <summary>
    /// The verdict at completion: every threshold's cumulative value (see
    /// <see cref="CumulativeCurrentOf"/>) and whether its comparison held, in declaration order.
    /// Pure — stores nothing; empty when there are no thresholds.
    /// </summary>
    public static IReadOnlyList<ThresholdResult> EvaluateVerdict(IReadOnlyList<Threshold> thresholds, ScenarioLoadResult result)
    {
        if (thresholds == null)
            throw new ArgumentNullException(nameof(thresholds), "Thresholds cannot be null.");
        if (result == null)
            throw new ArgumentNullException(nameof(result), "Result cannot be null.");

        var results = new ThresholdResult[thresholds.Count];
        for (var index = 0; index < results.Length; index++)
        {
            var threshold = thresholds[index];
            var current = CumulativeCurrentOf(threshold, result);
            results[index] = new ThresholdResult(threshold, current, !IsViolated(threshold, current));
        }

        return results;
    }

    /// <summary>
    /// The cumulative value the verdict reads for a threshold's metric, in the metric's unit:
    /// the Ok mean, p95 or p99 in milliseconds, the failed share of all measurement requests,
    /// or the Ok requests per second. Zero for a result without the stats in question.
    /// </summary>
    public static double CumulativeCurrentOf(Threshold threshold, ScenarioLoadResult result)
    {
        if (threshold == null)
            throw new ArgumentNullException(nameof(threshold), "Threshold cannot be null.");
        if (result == null)
            throw new ArgumentNullException(nameof(result), "Result cannot be null.");

        switch (threshold.Metric)
        {
            case ThresholdMetric.ResponseTimeMean:
                if (result.Ok == null)
                    return 0.0;
                return result.Ok.ResponseTimeMean.TotalMilliseconds;
            case ThresholdMetric.ResponseTimePercentile95:
                if (result.Ok == null)
                    return 0.0;
                return result.Ok.ResponseTimePercentile95.TotalMilliseconds;
            case ThresholdMetric.ResponseTimePercentile99:
                if (result.Ok == null)
                    return 0.0;
                return result.Ok.ResponseTimePercentile99.TotalMilliseconds;
            case ThresholdMetric.ErrorRate:
            {
                var okCount = RequestCountOf(result.Ok);
                var failedCount = RequestCountOf(result.Failed);
                var requestCount = okCount + failedCount;
                if (requestCount == 0)
                    return 0.0;
                return (double)failedCount / requestCount;
            }
            case ThresholdMetric.RequestsPerSecond:
                if (result.Ok == null)
                    return 0.0;
                return result.Ok.RequestsPerSecond;
            default:
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold.Metric, "Unknown threshold metric.");
        }
    }

    /// <summary>The newest interval's value for a threshold's metric, in the metric's unit.</summary>
    private static double IntervalCurrentOf(Threshold threshold, double errorRate, double intervalRequestsPerSecond, IntervalLatency intervalLatency)
    {
        switch (threshold.Metric)
        {
            case ThresholdMetric.ResponseTimeMean:
                return intervalLatency.ResponseTimeMean.TotalMilliseconds;
            case ThresholdMetric.ResponseTimePercentile95:
                return intervalLatency.ResponseTimePercentile95.TotalMilliseconds;
            case ThresholdMetric.ResponseTimePercentile99:
                return intervalLatency.ResponseTimePercentile99.TotalMilliseconds;
            case ThresholdMetric.ErrorRate:
                return errorRate;
            case ThresholdMetric.RequestsPerSecond:
                return intervalRequestsPerSecond;
            default:
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold.Metric, "Unknown threshold metric.");
        }
    }

    private static long RequestCountOf(Stats stats)
    {
        if (stats == null)
            return 0;

        return stats.RequestCount;
    }
}
