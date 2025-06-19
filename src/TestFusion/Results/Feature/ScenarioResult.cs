namespace TestFusion.Results.Feature;

public class ScenarioResult
{
    public string Name { get; set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public bool HasInputData { get; set; } = false;

    public List<IterationResult> IterationResults { get; } = new();
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

    internal ScenarioResult(Scenario scenario)
    {
        Name = scenario.Name;
        HasInputData = scenario.InputDataInfo.HasInputData;
        StartTime = DateTime.UtcNow;
    }

    internal void AddIterationResult(IterationResult iterationResult)
    {
        IterationResults.Add(iterationResult);
    }

    public void MarkAsCompleted()
    {
        EndTime = DateTime.UtcNow;
    }
}

