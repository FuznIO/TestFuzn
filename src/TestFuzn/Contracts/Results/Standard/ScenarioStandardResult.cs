using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn.Contracts.Results.Standard;

internal class ScenarioStandardResult
{
    private bool _skipped = false;

    public string Name { get; set; }
    public string Description { get; set; }
    public string FullName { get; set; }
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
    public List<IterationResult> IterationResults { get; } = new();
    public int TotalCount => IterationResults.Count;
    public int PassedCount => IterationResults.Count(x => x.Passed);
    public int FailedCount => IterationResults.Count(x => !x.Passed);
    public TestStatus Status
    {
        get
        {
            if (_skipped)
                return TestStatus.Skipped;

            if (PassedCount == TotalCount)
                return TestStatus.Passed;
            return TestStatus.Failed;
        }
    }

    internal ScenarioStandardResult(Scenario scenario)
    {
        Name = scenario.Name;
        Id = scenario.Id;
        Description = scenario.Description;
        HasInputData = scenario.InputDataInfo.HasInputData;
        TestType = scenario.TestType;
    }

    internal void AddIterationResult(IterationResult iterationResult)
    {
        IterationResults.Add(iterationResult);
    }

    internal void MarkPhaseAsStarted(StandardTestPhase standardTestPhase)
    {
        switch (standardTestPhase)
        {
            case StandardTestPhase.Init:
                InitStartTime = DateTime.UtcNow;
                break;
            case StandardTestPhase.Execute:
                ExecuteStartTime = DateTime.UtcNow;
                break;
            case StandardTestPhase.Cleanup:
                CleanupStartTime = DateTime.UtcNow;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(standardTestPhase), standardTestPhase, null);
        }
    }

    internal void MarkPhaseAsCompleted(StandardTestPhase standardTestPhase)
    {
        switch (standardTestPhase)
        {
            case StandardTestPhase.Init:
                InitEndTime = DateTime.UtcNow;
                break;
            case StandardTestPhase.Execute:
                ExecuteEndTime = DateTime.UtcNow;
                break;
            case StandardTestPhase.Cleanup:
                CleanupEndTime = DateTime.UtcNow;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(standardTestPhase), standardTestPhase, null);
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

