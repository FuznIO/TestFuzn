using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn.Contracts.Results.Standard;

internal class TestResult
{
    public string GroupName { get; set; }
    public string Name { get; set; }
    public string FullName { get; set; }
    public string Id { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public List<string> Tags { get; set; }
    public string Description { get; set; }
    public DateTime InitStartTime { get; private set; }
    public DateTime InitEndTime { get; private set; }
    public DateTime ExecuteStartTime { get; private set; }
    public DateTime ExecuteEndTime { get; private set; }
    public DateTime CleanupStartTime { get; private set; } 
    public DateTime CleanupEndTime { get; private set; }
    public bool HasInputData { get; set; } = false;
    public TestType TestType { get; set; }
    public List<IterationResult> IterationResults { get; } = new();
    public int TotalCount => IterationResults.Count;
    public int PassedCount => IterationResults.Count(x => x.Passed);
    public int FailedCount => IterationResults.Count(x => !x.Passed);
    
    private TestStatus? _status;
    public TestStatus Status
    {
        get
        {
            if (_status != null)
                return (TestStatus) _status;

            if (PassedCount == TotalCount)
                return TestStatus.Passed;
            return TestStatus.Failed;
        }
        set
        {
            _status = value;
        }
    }

    public TestResult(TestInfo testInfo, Scenario scenario)
    {
        GroupName = testInfo.Group.Name;
        Name = testInfo.Name;
        FullName = testInfo.FullName;
        Id = testInfo.Id;
        Description = testInfo.Description;
        Metadata = testInfo.Metadata;
        Tags = testInfo.Tags;
        if (testInfo.Skipped)
        {
            Status = TestStatus.Skipped;
            TestType = TestType.Standard;
        }

        if (scenario != null)
        {
            HasInputData = scenario.InputDataInfo.HasInputData;
            TestType = scenario.TestType;
        }
    }

    internal void MarkPhaseAsStarted(StandardTestPhase standardTestPhase, DateTime timestamp)
    {
        switch (standardTestPhase)
        {
            case StandardTestPhase.Init:
                InitStartTime = timestamp;
                break;
            case StandardTestPhase.Execute:
                ExecuteStartTime = timestamp;
                break;
            case StandardTestPhase.Cleanup:
                CleanupStartTime = timestamp;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(standardTestPhase), standardTestPhase, null);
        }
    }

    internal void MarkPhaseAsCompleted(StandardTestPhase standardTestPhase, DateTime timestamp)
    {
        switch (standardTestPhase)
        {
            case StandardTestPhase.Init:
                InitEndTime = timestamp;
                break;
            case StandardTestPhase.Execute:
                ExecuteEndTime = timestamp;
                break;
            case StandardTestPhase.Cleanup:
                CleanupEndTime = timestamp;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(standardTestPhase), standardTestPhase, null);
        }
    }

    public DateTime StartTime()
    {
        return InitStartTime;
    }

    public DateTime EndTime()
    {
        return CleanupEndTime;
    }

    public TimeSpan TestRunDuration()
    {
        return EndTime() - StartTime();
    }
}