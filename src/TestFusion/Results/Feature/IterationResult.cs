namespace TestFusion.Results.Feature;

public class IterationResult
{
    public string? InputData { get; set; }
    public Dictionary<string, StepResult> StepResults { get; } = new();
    public bool Passed => StepResults.All(x => x.Value.Status == StepStatus.Passed);
    public TimeSpan ExecutionDuration { get; set; }
}

