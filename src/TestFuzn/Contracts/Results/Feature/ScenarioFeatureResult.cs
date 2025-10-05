using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Contracts.Results.Feature;

public class ScenarioFeatureResult
{
    private bool _skipped = false;

    public string Name { get; set; }
    public string Id { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public List<string> Tags { get; set; }
    public DateTime InitStartTime { get; set; }
    public DateTime InitEndTime { get; set; }
    public DateTime ExecuteStartTime { get; set; }
    public DateTime ExecuteEndTime { get; set; }
    public DateTime CleanupStartTime { get; set; } 
    public DateTime CleanupEndTime { get; set; }
    public bool HasInputData { get; set; } = false;
    public TestType TestType { get; set; }
    public List<IterationFeatureResult> IterationResults { get; } = new();
    public int TotalCount => IterationResults.Count;
    public int PassedCount => IterationResults.Count(x => x.Passed);
    public int FailedCount => IterationResults.Count(x => !x.Passed);
    public ScenarioStatus Status
    {
        get
        {
            if (_skipped)
                return ScenarioStatus.Skipped;

            if (PassedCount == TotalCount)
                return ScenarioStatus.Passed;
            return ScenarioStatus.Failed;
        }
    }

    internal ScenarioFeatureResult(Scenario scenario)
    {
        Name = scenario.Name;
        Id = scenario.Id;
        Tags = scenario.TagsInternal;
        Metadata = scenario.MetadataInternal;
        HasInputData = scenario.InputDataInfo.HasInputData;
        TestType = scenario.TestType;
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

    internal void MarkAsSkipped()
    {
        _skipped = true;
    }

    public DateTime StartTime()
    {
        return InitStartTime;
    }

    public DateTime EndTime()
    {
        return CleanupEndTime;
    }

    public TimeSpan TestRunTotalDuration()
    {
        return EndTime() - StartTime();
    }
}

