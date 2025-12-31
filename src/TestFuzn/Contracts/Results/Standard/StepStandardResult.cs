namespace Fuzn.TestFuzn.Contracts.Results.Feature;

internal class StepStandardResult
{
    public string Name { get; internal set; }
    public string Id { get; internal set; }
    public StepStatus Status { get; internal set; } = StepStatus.Failed;
    public Exception Exception { get; internal set; }
    public TimeSpan Duration { get; internal set; }
    public List<Attachment> Attachments { get; internal set; }
    public List<Comment> Comments { get; internal set; }
    public List<StepStandardResult> StepResults { get; internal set; }
}