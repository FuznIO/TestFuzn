namespace FuznLabs.TestFuzn.Internals.Execution;

internal class ExecuteScenarioMessage
{
    public Guid MessageId { get; set; }
    public string ScenarioName { get; set; }
    public bool IsWarmup { get; }
    
    public ExecuteScenarioMessage(string scenarioName, bool isWarmup)
    {
        MessageId = Guid.NewGuid();
        ScenarioName = scenarioName;
        IsWarmup = isWarmup;
    }
}
