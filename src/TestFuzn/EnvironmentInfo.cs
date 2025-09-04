namespace FuznLabs.TestFuzn;

public class ExecutionInfo
{
    public string EnvironmentName { get; internal set; }
    public string NodeName { get; internal set; }
    public string TestRunId { get; internal set; }
    public string CorrelationId { get; set; }
}
