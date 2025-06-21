namespace TestFusion.Contracts.Results.Load;

public class ScenarioLoadResult
{
    public string FeatureName { get; internal set; }
    public string ScenarioName { get; internal set; }
    public DateTime StartTime { get; internal set; }
    public DateTime EndTime { get; internal set; }
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
    public Dictionary<string, StepLoadResult> Steps { get; internal set; } = new();
    internal List<Exception>? AssertWhenDoneExceptions { get; set; }

    public double OkPercentage
    {
        get
        {
            return 100.0 * Ok.RequestCount / Math.Max(1, RequestCount);
        }
    }
}
