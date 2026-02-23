using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Results.Standard;

internal class StandardResultManager
{
    private static SuiteResult _suiteResult { get; } = new SuiteResult();

    public SuiteResult GetSuiteResults()
    {
        return _suiteResult;
    }

    public void AddSkippedTestResult(TestInfo testInfo)
    {
        var startTime = DateTime.UtcNow;

        AddResult(testInfo, startTime, startTime);
    }

    public void AddNonSkippedTestResults(TestExecutionState testExecutionState)
    {
        var scenarioLoadResults = testExecutionState.ScenarioResultState.LoadCollectors.Select(x => x.Value.GetCurrentResult())
                                    .ToList();

        var firstScenario = testExecutionState.ScenarioResultState.StandardCollectors.First().Value;
        var lastScenario = testExecutionState.ScenarioResultState.StandardCollectors.Last().Value;

        AddResult(testExecutionState.TestClassInstance.TestInfo,
            firstScenario.StartTime(),
            lastScenario.EndTime(),
            firstScenario,
            scenarioLoadResults);
    }    

    private void AddResult(TestInfo testInfo, 
        DateTime testStartTime,
        DateTime testEndTime,
        ScenarioStandardResult? scenarioStandardResult = null,
        List<ScenarioLoadResult>? scenarioLoadResults = null)
    {
        var groupResult = _suiteResult.GroupResults.GetOrAdd(testInfo.Group.Name,
                                (key) => new GroupResult(testInfo.Group.Name));

        var testResult = new TestResult();

        testResult.Name = testInfo.Name;
        testResult.FullName = testInfo.FullName;
        testResult.Id = testInfo.Id;
        testResult.Metadata = testInfo.Metadata;
        testResult.Tags = testInfo.Tags;
        if (testInfo.Skipped)
        {
            testResult.Status = TestStatus.Skipped;
        }
        else
        { 
            if (scenarioStandardResult!.TestType == Contracts.TestType.Standard)
            {
                testResult.Status = scenarioStandardResult.Status;
                testResult.ScenarioResult = scenarioStandardResult;
                testResult.StartTime = testStartTime;
                testResult.EndTime = testEndTime;
                testResult.Duration = scenarioStandardResult.TestRunTotalDuration();
            }
            else
            {
                TestStatus status = TestStatus.Passed;
                TimeSpan duration = TimeSpan.Zero;
                foreach (var scenario in scenarioLoadResults!)
                {
                    if (scenario.Status == TestStatus.Failed)
                    {
                        status = TestStatus.Failed;
                    }

                    duration += scenario.TotalExecutionDuration;
                }

                testResult.Status = status;
                testResult.StartTime = testStartTime;
                testResult.EndTime = testEndTime;
                testResult.Duration = duration;
            }
        }

        if (!groupResult.TestResults.TryAdd(testResult.Name, testResult))
        {
            throw new Exception($"Test name '{testResult.Name}' is duplicated, it already exists in group '{testInfo.Group.Name}'.");
        }
    }
}
