using HdrHistogram;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal class StatsCollector
{
    private LongHistogram _histogram;
    private int _count;
    private TimeSpan _totalExecutionDuration;
    private int _requestsPerSecond = 0;
    private TimeSpan _min = TimeSpan.Zero;
    private TimeSpan _max;

    public void Record(TimeSpan executionDuration, DateTime startTime, DateTime endTime)
    {
        if (_histogram == null)
            _histogram = new LongHistogram(1, TimeSpan.TicksPerMinute * 5, 3);

        _totalExecutionDuration = TimeSpan.FromTicks(_totalExecutionDuration.Ticks + executionDuration.Ticks);
        _count++;

        var testRunTimeInSeconds = (int) (endTime - startTime).TotalSeconds;
        if (testRunTimeInSeconds == 0)
            _requestsPerSecond = _count;
        else
            _requestsPerSecond = _count / testRunTimeInSeconds;

        _histogram.RecordValue(executionDuration.Ticks);
        if (_min == TimeSpan.Zero || executionDuration < _min)
            _min = executionDuration;
        if (executionDuration > _max)
            _max = executionDuration;
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
