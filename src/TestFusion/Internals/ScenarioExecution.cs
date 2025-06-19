namespace TestFusion.Internals;

internal class ScenarioExecutionInfo
{
    public string ScenarioName { get; set; }
    public Guid ExecutionId { get; set; }

    public ScenarioExecutionInfo(string scenarioName)
    {
        ScenarioName = scenarioName;
        ExecutionId = Guid.NewGuid();
    }
}
