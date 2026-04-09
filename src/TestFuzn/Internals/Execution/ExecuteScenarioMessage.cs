namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecuteScenarioMessage
{
    public Guid MessageId { get; set; }
    public Scenario Scenario { get; }
    public string ScenarioName => Scenario.Name;
    public bool IsWarmup { get; }

    public ExecuteScenarioMessage(Scenario scenario, bool isWarmup)
    {
        MessageId = Guid.NewGuid();
        Scenario = scenario;
        IsWarmup = isWarmup;
    }
}
