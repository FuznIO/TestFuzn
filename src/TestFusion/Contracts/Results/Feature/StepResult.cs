namespace TestFusion.Contracts.Results.Feature;

public class StepResult
{
    public string Name { get; internal set; }
    public StepStatus Status { get; internal set; } = StepStatus.Failed;
    public Exception Exception { get; internal set; }
    public TimeSpan Duration { get; internal set; }
    public List<Attachment> Attachments { get; internal set; }
}