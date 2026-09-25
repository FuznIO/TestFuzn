using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class ScenarioLiveMetrics
{
    public const int SampleCapacity = 300;

    public const int ErrorCapacity = 20;

    private readonly object _gate = new object();
    private readonly IReadOnlyList<SimulationInfo> _simulations;
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

    public string ScenarioName { get; }

    public ScenarioLiveMetrics(string scenarioName, IReadOnlyList<SimulationInfo>? simulations = null)
    {
        if (scenarioName == null)
            throw new ArgumentNullException(nameof(scenarioName), "Scenario name cannot be null.");

        ScenarioName = scenarioName;
        _simulations = simulations ?? Array.Empty<SimulationInfo>();
        _current = new LiveMetricsSnapshot
        {
            ScenarioName = scenarioName,
            Phase = LoadTestPhase.Init
        };
    }

    public LiveMetricsSnapshot Current => Volatile.Read(ref _current);

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
                Duration = ComputeDuration(snapshot, timestamp),
                Timestamp = timestamp,
                PhaseTimes = new TestPhasesLayout.PhaseTimes
                {
                    InitStart = snapshot.InitStartTime,
                    InitEnd = snapshot.InitEndTime,
                    WarmupStart = snapshot.WarmupStartTime,
                    WarmupEnd = snapshot.WarmupEndTime,
                    MeasurementStart = snapshot.MeasurementStartTime,
                    MeasurementEnd = snapshot.MeasurementEndTime,
                    CleanupStart = snapshot.CleanupStartTime,
                    CleanupEnd = snapshot.CleanupEndTime
                },
                HasWarmup = snapshot.HasWarmupStep(),
                Simulations = _simulations,
                IsCompleted = snapshot.IsCompleted,
                Status = snapshot.Status,
                StatusDetail = BuildStatusDetail(snapshot),
                Description = TextOrEmpty(snapshot.Description),
                AssertWhileWarmingUpException = snapshot.AssertWhileWarmingUpException,
                AssertWhileRunningException = snapshot.AssertWhileRunningException,
                AssertWhenDoneException = snapshot.AssertWhenDoneException,
                RequestCountOk = okStats.RequestCount,
                RequestCountFailed = failedStats.RequestCount,
                WarmupRequestCountOk = snapshot.WarmupRequestCountOk,
                WarmupRequestCountFailed = snapshot.WarmupRequestCountFailed,
                RequestsPerSecond = snapshot.RequestsPerSecond,
                Ok = okStats,
                Failed = failedStats,
                Samples = MaterializeSamples(out var requestsPerSecondSeries),
                RequestsPerSecondSeries = requestsPerSecondSeries,
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
        var sample = new LiveMetricsSample(timestamp, (int)Math.Min(okDelta, int.MaxValue), (int)Math.Min(failedDelta, int.MaxValue), requestsPerSecond);

        _samples[_nextSampleIndex] = sample;
        _nextSampleIndex = (_nextSampleIndex + 1) % SampleCapacity;
        if (_appendedSampleCount < int.MaxValue)
            _appendedSampleCount++;

        _baselineTimestamp = timestamp;
        _baselineOkCumulative = okCumulative;
        _baselineFailedCumulative = failedCumulative;
        UpdateStepIntervalRates(snapshot, intervalSeconds);
    }

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

    private LiveMetricsSample[] MaterializeSamples(out double[] requestsPerSecondSeries)
    {
        var count = _appendedSampleCount < SampleCapacity ? _appendedSampleCount : SampleCapacity;
        var oldestIndex = _appendedSampleCount <= SampleCapacity ? 0 : _nextSampleIndex;

        var samples = new LiveMetricsSample[count];
        requestsPerSecondSeries = new double[count];

        for (var index = 0; index < count; index++)
        {
            var sample = _samples[(oldestIndex + index) % SampleCapacity];
            samples[index] = sample;
            requestsPerSecondSeries[index] = sample.RequestsPerSecond;
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

        return MaterializeSteps(snapshot.Steps.Values, snapshot, timestamp);
    }

    private LiveStepMetrics[] MaterializeSteps(IEnumerable<StepLoadResult> steps, ScenarioLoadResult snapshot, DateTime timestamp)
    {
        var rows = new List<LiveStepMetrics>();
        foreach (var step in steps)
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
                Ok = ok,
                Failed = failed,
                Steps = step.Steps != null ? MaterializeSteps(step.Steps, snapshot, timestamp) : Array.Empty<LiveStepMetrics>()
            });
        }

        return rows.ToArray();
    }

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

    private static string TextOrEmpty(string? text)
    {
        if (text == null)
            return string.Empty;

        return text;
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


    private static TimeSpan ComputeDuration(ScenarioLoadResult snapshot, DateTime timestamp)
    {
        if (snapshot.InitStartTime == default)
            return TimeSpan.Zero;
        if (snapshot.CleanupEndTime != default)
            return ClampNonNegative(snapshot.CleanupEndTime - snapshot.InitStartTime);

        return ClampNonNegative(timestamp - snapshot.InitStartTime);
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
