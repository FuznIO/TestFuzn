namespace TestFusion.Contracts.Results.Feature;

public class ScenarioFeatureResult
{
    public string Name { get; set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public bool HasInputData { get; set; } = false;

    public List<IterationFeatureResult> IterationResults { get; } = new();
    public int TotalCount => IterationResults.Count;
    public int PassedCount => IterationResults.Count(x => x.Passed);
    public int FailedCount => IterationResults.Count(x => !x.Passed);
    public ScenarioStatus Status
    {
        get
        {
            if (PassedCount == TotalCount)
                return ScenarioStatus.Passed;
            return ScenarioStatus.Failed;
        }
    }

    internal ScenarioFeatureResult(Scenario scenario)
    {
        Name = scenario.Name;
        HasInputData = scenario.InputDataInfo.HasInputData;
        StartTime = DateTime.UtcNow;
    }

    internal void AddIterationResult(IterationFeatureResult iterationResult)
    {
        IterationResults.Add(iterationResult);
    }

    public void MarkAsCompleted()
    {
        EndTime = DateTime.UtcNow;
    }
}

