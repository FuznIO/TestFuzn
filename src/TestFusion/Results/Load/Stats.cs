namespace TestFusion.Results.Load;

public class Stats
{
    public int RequestCount { get; internal set; }
    public int RequestsPerSecond { get; internal set; }
    public TimeSpan TotalExecutionDuration { get; internal set; }
    public TimeSpan ResponseTimeMin { get; internal set; }
    public TimeSpan ResponseTimeMax { get; internal set; }
    public TimeSpan ResponseTimeMean { get; internal set; }
    public TimeSpan ResponseTimeStandardDeviation { get; internal set; }
    public TimeSpan ResponseTimeMedian { get; internal set; }
    public TimeSpan ResponseTimePercentile75 { get; internal set; }
    public TimeSpan ResponseTimePercentile95 { get; internal set; }
    public TimeSpan ResponseTimePercentile99 { get; internal set; }
}
