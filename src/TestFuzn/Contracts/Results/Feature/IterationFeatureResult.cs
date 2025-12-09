namespace Fuzn.TestFuzn.Contracts.Results.Feature;

internal class IterationFeatureResult
{
    public string? InputData { get; internal set; }
    public string CorrelationId { get; internal set; }
    public Dictionary<string, StepFeatureResult> StepResults { get; } = new();
    public bool Passed => StepResults.All(x => x.Value.Status == StepStatus.Passed);
    public TimeSpan ExecutionDuration { get; internal set; }
}

