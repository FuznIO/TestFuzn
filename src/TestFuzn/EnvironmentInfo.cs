namespace Fuzn.TestFuzn;

public class ExecutionInfo
{    
    public string TargetEnvironment { get; internal set; }
    public string ExecutionEnvironment { get; internal set; }
    public string NodeName { get; internal set; }
    public string TestRunId { get; internal set; }
    public string CorrelationId { get; set; }
}
