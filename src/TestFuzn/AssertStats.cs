using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides load test statistics for assertions.
/// </summary>
public class AssertStats
{
    /// <summary>
    /// Gets the total number of requests.
    /// </summary>
    public int RequestCount { get; internal set; }

    /// <summary>
    /// Gets the number of requests per second.
    /// </summary>
    public int RequestsPerSecond { get; internal set; }

    /// <summary>
    /// Gets the minimum response time.
    /// </summary>
    public TimeSpan ResponseTimeMin { get; internal set; }

    /// <summary>
    /// Gets the maximum response time.
    /// </summary>
    public TimeSpan ResponseTimeMax { get; internal set; }

    /// <summary>
    /// Gets the mean (average) response time.
    /// </summary>
    public TimeSpan ResponseTimeMean { get; internal set; }

    /// <summary>
    /// Gets the standard deviation of response times.
    /// </summary>
    public TimeSpan ResponseTimeStandardDeviation { get; internal set; }

    /// <summary>
    /// Gets the median (50th percentile) response time.
    /// </summary>
    public TimeSpan ResponseTimeMedian { get; internal set; }

    /// <summary>
    /// Gets the 75th percentile response time.
    /// </summary>
    public TimeSpan ResponseTimePercentile75 { get; internal set; }

    /// <summary>
    /// Gets the 95th percentile response time.
    /// </summary>
    public TimeSpan ResponseTimePercentile95 { get; internal set; }

    /// <summary>
    /// Gets the 99th percentile response time.
    /// </summary>
    public TimeSpan ResponseTimePercentile99 { get; internal set; }

    internal AssertStats(Stats stats)
    {
        RequestCount = stats.RequestCount;
        RequestsPerSecond = stats.RequestsPerSecond;
        ResponseTimeMin = stats.ResponseTimeMin;
        ResponseTimeMax = stats.ResponseTimeMax;
        ResponseTimeMean = stats.ResponseTimeMean;
        ResponseTimeStandardDeviation = stats.ResponseTimeStandardDeviation;
        ResponseTimeMedian = stats.ResponseTimeMedian;
        ResponseTimePercentile75 = stats.ResponseTimePercentile75;
        ResponseTimePercentile95 = stats.ResponseTimePercentile95;
        ResponseTimePercentile99 = stats.ResponseTimePercentile99;
    }
}
