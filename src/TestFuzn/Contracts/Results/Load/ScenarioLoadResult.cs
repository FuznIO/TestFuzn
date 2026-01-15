namespace Fuzn.TestFuzn.Contracts.Results.Load;

internal class ScenarioLoadResult
{
    public string ScenarioName { get; internal set; }
    public string Id { get; internal set; }
    public string Description { get; internal set; }
    public List<string> Simulations { get; internal set; }
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
    public TestStatus Status { get; internal set; }
    public Stats Ok { get; internal set; }
    public Stats Failed { get; internal set; }
    public int WarmupRequestCountOk { get; internal set; }
    public int WarmupRequestCountFailed { get; internal set; }
    public Dictionary<string, StepLoadResult> Steps { get; internal set; } = new();
    public Exception? AssertWhileRunningException { get; internal set; }
    public Exception? AssertWhenDoneException { get; internal set; }

    public double OkPercentage
    {
        get
        {
            return 100.0 * Ok.RequestCount / Math.Max(1, RequestCount);
        }
    }

    internal bool HasWarmupStep()
    {
        if (WarmupRequestCountOk > 0 || WarmupRequestCountFailed > 0)
            return true;

        return false;
    }

    internal TimeSpan InitTotalDuration()
    {
        return InitEndTime - InitStartTime;
    }

    internal TimeSpan WarmupTotalDuration()
    {
        return WarmupEndTime - WarmupStartTime;
    }

    internal TimeSpan MeasurementTotalDuration()
    {
        return MeasurementEndTime - MeasurementStartTime;
    }

    internal TimeSpan CleanupTotalDuration()
    {
        return CleanupEndTime - CleanupStartTime;
    }

    internal DateTime StartTime()
    {
        return InitStartTime;
    }

    internal DateTime EndTime()
    {
        return CleanupEndTime;
    }

    internal TimeSpan TestRunTotalDuration()
    {
        return EndTime() - StartTime();
    }
}
