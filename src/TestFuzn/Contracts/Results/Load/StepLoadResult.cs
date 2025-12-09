namespace Fuzn.TestFuzn.Contracts.Results.Load;

internal class StepLoadResult
{
    public string Name { get; internal set; }
    public string Id { get; internal set; }
    public int RequestCount 
    { 
        get
        {
            return Ok.RequestCount + Failed.RequestCount;
        }
    }
    public TimeSpan TotalExecutionDuration
    { 
        get
        {
            return Ok.TotalExecutionDuration + Failed.TotalExecutionDuration;
        }
    }
    public StepStatus Status { get; internal set; }
    public Stats Ok { get; internal set; }
    public Stats Failed { get; internal set; }
    public int SkippedCount { get; internal set; }
    internal Dictionary<string, ErrorEntry> Errors { get; set; }
    public List<StepLoadResult> Steps { get; set; }
}