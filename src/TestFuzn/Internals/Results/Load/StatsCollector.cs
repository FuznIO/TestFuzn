using HdrHistogram;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal class StatsCollector
{
    private readonly bool _trackIntervalLatency;
    private LongHistogram _histogram;
    private Recorder _intervalRecorder;
    private HistogramBase _intervalHistogram;
    private int _count;
    private TimeSpan _totalExecutionDuration;
    private int _requestsPerSecond = 0;
    private TimeSpan _min = TimeSpan.Zero;
    private TimeSpan _max;

    /// <summary>
    /// <paramref name="trackIntervalLatency"/> additionally records every measurement into a
    /// double-buffered interval histogram (HdrHistogram's Recorder), so
    /// <see cref="GetIntervalLatency"/> can report per-interval percentiles and bucket counts for
    /// the live dashboard. Off by default — the scenario-level and per-step Ok collectors turn it
    /// on; the Failed collectors do not.
    /// </summary>
    public StatsCollector(bool trackIntervalLatency = false)
    {
        _trackIntervalLatency = trackIntervalLatency;
    }

    public void Record(TimeSpan executionDuration, DateTime startTime, DateTime endTime)
    {
        if (_histogram == null)
            _histogram = new LongHistogram(1, TimeSpan.TicksPerMinute * 5, 3);
        if (_trackIntervalLatency && _intervalRecorder == null)
            _intervalRecorder = new Recorder(1, TimeSpan.TicksPerMinute * 5, 3, (id, lowest, highest, digits) => new LongHistogram(id, lowest, highest, digits));

        _totalExecutionDuration = TimeSpan.FromTicks(_totalExecutionDuration.Ticks + executionDuration.Ticks);
        _count++;

        var testRunTimeInSeconds = (endTime - startTime).TotalSeconds;
        if (testRunTimeInSeconds < 1)
            _requestsPerSecond = _count;
        else
            _requestsPerSecond = (int) Math.Round(_count / testRunTimeInSeconds);

        _histogram.RecordValue(executionDuration.Ticks);
        if (_intervalRecorder != null)
            _intervalRecorder.RecordValue(executionDuration.Ticks);
        if (_min == TimeSpan.Zero || executionDuration < _min)
            _min = executionDuration;
        if (executionDuration > _max)
            _max = executionDuration;
    }

    /// <summary>
    /// The response-time distribution — count, mean, median, p95, p99 and bucket counts — of the
    /// measurements recorded since the previous call: each call closes the current interval and
    /// starts a new one (read-and-reset, like the underlying Recorder's GetIntervalHistogram),
    /// so the owning collector reads it ONCE per force refresh and shares the result until the
    /// next one.
    /// <see cref="IntervalLatency.Empty"/> when the interval had no measurements or interval
    /// tracking is off. Callers serialize this with <see cref="Record"/> via the owning
    /// collector's lock; the Recorder's own write coordination keeps the buffer swap safe
    /// regardless, without adding any locking to the recording hot path.
    /// </summary>
    public IntervalLatency GetIntervalLatency()
    {
        if (_intervalRecorder == null)
            return IntervalLatency.Empty;

        _intervalHistogram = _intervalRecorder.GetIntervalHistogram(_intervalHistogram);
        if (_intervalHistogram.TotalCount == 0)
            return IntervalLatency.Empty;

        return new IntervalLatency(
            ClampToInt(_intervalHistogram.TotalCount),
            TimeSpan.FromTicks((long) _intervalHistogram.GetMean()),
            TimeSpan.FromTicks(_intervalHistogram.GetValueAtPercentile(50)),
            TimeSpan.FromTicks(_intervalHistogram.GetValueAtPercentile(95)),
            TimeSpan.FromTicks(_intervalHistogram.GetValueAtPercentile(99)),
            CountBuckets(_intervalHistogram));
    }

    /// <summary>
    /// Distributes a closed interval histogram's values over the <see cref="LatencyBuckets"/>.
    /// The histogram keeps values to 3 significant digits, so it is walked one equivalent-value
    /// range at a time (only the non-empty ranges) and each range is placed by its lowest value:
    /// a value on a bucket bound always counts toward the bucket it bounds, and a value less
    /// than one resolution step (at most 0.1 %) above the bound counts with it.
    /// </summary>
    private static int[] CountBuckets(HistogramBase histogram)
    {
        var counts = new int[LatencyBuckets.Count];
        foreach (var recorded in histogram.RecordedValues())
        {
            var lowestEquivalentValue = histogram.LowestEquivalentValue(recorded.ValueIteratedTo);
            var index = LatencyBuckets.IndexOf(TimeSpan.FromTicks(lowestEquivalentValue));
            counts[index] = ClampToInt(counts[index] + recorded.CountAtValueIteratedTo);
        }

        return counts;
    }

    private static int ClampToInt(long value)
    {
        return (int) Math.Min(value, int.MaxValue);
    }

    public Stats GetCurrentResult()
    {
        var stats = new Stats();

        if (_count == 0)
        {
            stats.TotalExecutionDuration = TimeSpan.Zero;
            stats.RequestCount = 0;
            stats.ResponseTimeMin = TimeSpan.Zero;
            stats.ResponseTimeMax = TimeSpan.Zero;
            stats.ResponseTimeMean = TimeSpan.Zero;
            stats.ResponseTimeStandardDeviation = TimeSpan.Zero;
            stats.ResponseTimeMedian = TimeSpan.Zero;
            stats.ResponseTimePercentile75 = TimeSpan.Zero;
            stats.ResponseTimePercentile95 = TimeSpan.Zero;
            stats.ResponseTimePercentile99 = TimeSpan.Zero;
            return stats;
        }

        stats.TotalExecutionDuration = _totalExecutionDuration;
        stats.RequestCount = _count;
        stats.RequestsPerSecond = _requestsPerSecond;
        stats.ResponseTimeMin = _min;
        stats.ResponseTimeMax = _max;
        stats.ResponseTimeMean = TimeSpan.FromTicks((long) _histogram.GetMean());
        stats.ResponseTimeStandardDeviation = TimeSpan.FromTicks((long) _histogram.GetStdDeviation());
        stats.ResponseTimeMedian = TimeSpan.FromTicks(_histogram.GetValueAtPercentile(50));
        stats.ResponseTimePercentile75 = TimeSpan.FromTicks(_histogram.GetValueAtPercentile(75));
        stats.ResponseTimePercentile95 = TimeSpan.FromTicks(_histogram.GetValueAtPercentile(95));
        stats.ResponseTimePercentile99 = TimeSpan.FromTicks(_histogram.GetValueAtPercentile(99));

        return stats;
    }
}
