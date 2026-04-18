namespace Fuzn.TestFuzn.Contracts.Results.Standard;

internal class IterationResult
{
    public object? InputData { get; internal set; }
    public string CorrelationId { get; internal set; }
    public Dictionary<string, StepStandardResult> StepResults { get; } = new();
    public bool Passed => StepResults.All(x => x.Value.Status == StepStatus.Passed);
    public DateTime InitStartTime { get; internal set; }
    public DateTime InitEndTime { get; internal set; }
    public DateTime ExecuteStartTime { get; internal set; }
    public DateTime ExecuteEndTime { get; internal set; }
    public DateTime CleanupStartTime { get; internal set; }
    public DateTime CleanupEndTime { get; internal set; }

    public TimeSpan InitDuration() => InitEndTime - InitStartTime;
    public TimeSpan ExecuteDuration() => ExecuteEndTime - ExecuteStartTime;
    public TimeSpan CleanupDuration() => CleanupEndTime - CleanupStartTime;
    public TimeSpan Duration() => CleanupEndTime - InitStartTime;
}

