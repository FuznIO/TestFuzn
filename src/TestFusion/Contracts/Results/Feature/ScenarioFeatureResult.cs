using TestFusion.Internals.Execution;

namespace TestFusion.Contracts.Results.Feature;

public class ScenarioFeatureResult
{
    public string Name { get; set; }
    public DateTime InitStartTime { get; set; }
    public DateTime InitEndTime { get; set; }
    public DateTime ExecuteStartTime { get; set; }
    public DateTime ExecuteEndTime { get; set; }
    public DateTime CleanupStartTime { get; set; } 
    public DateTime CleanupEndTime { get; set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public bool HasInputData { get; set; } = false;
    public bool IsLoadTest { get; set; } = false;

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

    internal void MarkPhaseAsStarted(FeatureTestPhase featureTestPhase)
    {
        switch (featureTestPhase)
        {
            case FeatureTestPhase.Init:
                InitStartTime = DateTime.UtcNow;
                break;
            case FeatureTestPhase.Execute:
                ExecuteStartTime = DateTime.UtcNow;
                break;
            case FeatureTestPhase.Cleanup:
                CleanupStartTime = DateTime.UtcNow;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(featureTestPhase), featureTestPhase, null);
        }
    }

    internal void MarkPhaseAsCompleted(FeatureTestPhase featureTestPhase)
    {
        switch (featureTestPhase)
        {
            case FeatureTestPhase.Init:
                InitEndTime = DateTime.UtcNow;
                break;
            case FeatureTestPhase.Execute:
                ExecuteEndTime = DateTime.UtcNow;
                break;
            case FeatureTestPhase.Cleanup:
                CleanupEndTime = DateTime.UtcNow;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(featureTestPhase), featureTestPhase, null);
        }
    }
}

