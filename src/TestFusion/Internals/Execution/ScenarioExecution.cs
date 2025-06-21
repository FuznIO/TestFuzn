namespace TestFusion.Internals.Execution;

internal class ScenarioExecutionInfo
{
    public string ScenarioName { get; set; }
    public bool IsWarmup { get; }
    public Guid ExecutionId { get; set; }

    public ScenarioExecutionInfo(string scenarioName, bool isWarmup)
    {
        ScenarioName = scenarioName;
        IsWarmup = isWarmup;
        ExecutionId = Guid.NewGuid();
    }
}
