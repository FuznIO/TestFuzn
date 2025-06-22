namespace TestFusion.Contracts.Results.Load;

public class ScenarioLoadResult
{
    public string ScenarioName { get; internal set; }
    public DateTime StartTime { get; internal set; }
    public DateTime EndTime { get; internal set; }
    public DateTime InitStartTime { get; internal set; }
    public DateTime InitEndTime { get; internal set; }
    public DateTime WarmupStartTime { get; internal set; }
    public DateTime WarmupEndTime { get; internal set; }
    public DateTime MeasurementStartTime { get; internal set; }
    public DateTime MeasurementEndTime { get; internal set; }
    public DateTime CleanupStartTime { get; internal set; }
    public DateTime CleanupEndTime { get; internal set; }
    internal bool IsCompleted { get; set; }
    public DateTime Created { get; internal set; }
    public int RequestCount { get; internal set; }
    public TimeSpan TotalExecutionDuration
    { 
        get
        {
            return Ok.TotalExecutionDuration + Failed.TotalExecutionDuration;
        }
    }
    public int RequestsPerSecond { get; internal set; }
    public ScenarioStatus Status { get; internal set; }
    public Stats Ok { get; internal set; }
    public Stats Failed { get; internal set; }
    public int WarmupRequestCountOk { get; internal set; }
    public int WarmupRequestCountFailed { get; internal set; }
    public Dictionary<string, StepLoadResult> Steps { get; internal set; } = new();
    internal List<Exception>? AssertWhenDoneExceptions { get; set; }

    public double OkPercentage
    {
        get
        {
            return 100.0 * Ok.RequestCount / Math.Max(1, RequestCount);
        }
    }


    public bool HasWarmupStep()
    {
        if (WarmupRequestCountOk > 0 || WarmupRequestCountFailed > 0)
            return true;

        return false;
    }
}
