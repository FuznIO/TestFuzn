using TestFusion.Contracts.Results.Load;

namespace TestFusion;

public class AssertStats
{
    public int RequestCount { get; internal set; }
    public int RequestsPerSecond { get; internal set; }
    public TimeSpan ResponseTimeMin { get; internal set; }
    public TimeSpan ResponseTimeMax { get; internal set; }
    public TimeSpan ResponseTimeMean { get; internal set; }
    public TimeSpan ResponseTimeStandardDeviation { get; internal set; }
    public TimeSpan ResponseTimeMedian { get; internal set; }
    public TimeSpan ResponseTimePercentile75 { get; internal set; }
    public TimeSpan ResponseTimePercentile95 { get; internal set; }
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
