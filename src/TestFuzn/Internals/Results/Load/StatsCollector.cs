using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal class StatsCollector
{
    private readonly Histogram _histogram = new();
    private int _count;
    private TimeSpan _totalExecutionDuration;
    private int _perSecond = 0;

    public int Count => _count;
    private TimeSpan _min = TimeSpan.Zero;
    private TimeSpan _max;

    public void Record(TimeSpan executionDuration, DateTime startTime, DateTime endTime)
    {
        _totalExecutionDuration = TimeSpan.FromTicks(_totalExecutionDuration.Ticks + executionDuration.Ticks);
        _count++;
        _perSecond = PerSecond(_count, endTime - startTime);

        _histogram.Record(executionDuration.Ticks);
        if (_min == TimeSpan.Zero || executionDuration < _min)
            _min = executionDuration;
        if (executionDuration > _max)
            _max = executionDuration;
    }

    public StatsState GetState()
    {
        var state = new StatsState();
        state.Count = _count;
        state.TotalTicks = _totalExecutionDuration.Ticks;
        state.MinTicks = _min.Ticks;
        state.MaxTicks = _max.Ticks;
        if (_histogram.TotalCount > 0)
            state.Histogram = _histogram.Encode();

        return state;
    }

    public void Add(StatsState state)
    {
        if (state.Count == 0)
            return;

        if (state.Histogram != null)
            _histogram.Add(Histogram.Decode(state.Histogram));

        _count += state.Count;
        _totalExecutionDuration = TimeSpan.FromTicks(_totalExecutionDuration.Ticks + state.TotalTicks);

        var min = TimeSpan.FromTicks(state.MinTicks);
        if (_min == TimeSpan.Zero || min < _min)
            _min = min;

        var max = TimeSpan.FromTicks(state.MaxTicks);
        if (max > _max)
            _max = max;
    }

    // Merged stats come from several machines, so the rate uses the measurement duration, not the last record time.
    public void SetPerSecond(TimeSpan measurementDuration)
    {
        _perSecond = PerSecond(_count, measurementDuration);
    }

    internal static int PerSecond(int count, TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
            return count;

        return (int) Math.Round(count / duration.TotalSeconds);
    }

    public Stats GetCurrentResult()
    {
        var stats = new Stats();

        if (_count == 0)
        {
            stats.TotalExecutionDuration = TimeSpan.Zero;
            stats.Count = 0;
            stats.DurationMin = TimeSpan.Zero;
            stats.DurationMax = TimeSpan.Zero;
            stats.DurationMean = TimeSpan.Zero;
            stats.DurationStandardDeviation = TimeSpan.Zero;
            stats.DurationMedian = TimeSpan.Zero;
            stats.DurationPercentile75 = TimeSpan.Zero;
            stats.DurationPercentile90 = TimeSpan.Zero;
            stats.DurationPercentile95 = TimeSpan.Zero;
            stats.DurationPercentile99 = TimeSpan.Zero;
            return stats;
        }

        stats.TotalExecutionDuration = _totalExecutionDuration;
        stats.Count = _count;
        stats.PerSecond = _perSecond;
        stats.DurationMin = _min;
        stats.DurationMax = _max;
        stats.DurationMean = TimeSpan.FromTicks(_totalExecutionDuration.Ticks / _count);
        if (_histogram.TotalCount == 0)
            return stats;

        stats.DurationStandardDeviation = TimeSpan.FromTicks((long) _histogram.StandardDeviation());
        stats.DurationMedian = Percentile(50);
        stats.DurationPercentile75 = Percentile(75);
        stats.DurationPercentile90 = Percentile(90);
        stats.DurationPercentile95 = Percentile(95);
        stats.DurationPercentile99 = Percentile(99);

        return stats;
    }

    // A bucket's highest value can be above the slowest time recorded in it; min and max are exact.
    private TimeSpan Percentile(double percentile)
    {
        return TimeSpan.FromTicks(Math.Clamp(_histogram.ValueAtPercentile(percentile), _min.Ticks, _max.Ticks));
    }
}
