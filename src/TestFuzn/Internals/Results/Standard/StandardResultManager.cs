using Fuzn.TestFuzn.Contracts.Results.Feature;
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
        AddResult(testInfo);
    }

    public void AddNonSkippedTestResults(SharedExecutionState sharedExecutionState)
    {
        var scenarioLoadResults = sharedExecutionState.ScenarioResultState.LoadCollectors.Select(x => x.Value.GetCurrentResult())
                                    .ToList();

        AddResult(sharedExecutionState.TestClassInstance.TestInfo, 
            sharedExecutionState.ScenarioResultState.StandardCollectors.First().Value,
            scenarioLoadResults);
    }    

    private void AddResult(TestInfo testInfo, 
        ScenarioStandardResult scenarioStandardResult = null,
        List<ScenarioLoadResult> scenarioLoadResults = null)
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
            if (scenarioStandardResult.TestType == Contracts.TestType.Standard)
            {
                testResult.Status = scenarioStandardResult.Status;
                testResult.ScenarioResult = scenarioStandardResult;
                testResult.Duration = scenarioStandardResult.TestRunTotalDuration();
            }
            else
            {
                bool passed = false;
                TimeSpan duration = TimeSpan.Zero;
                foreach (var scenario in scenarioLoadResults)
                {
                    if (!passed && scenario.Status == TestStatus.Failed)
                    {
                        passed = false;
                    }

                    duration += scenario.TotalExecutionDuration;
                }

                testResult.Duration = duration;
            }
        }

        if (!groupResult.TestResults.TryAdd(testResult.Name, testResult))
        {
            throw new Exception($"Test name '{testResult.Name}' is duplicated, it already exists in group '{testInfo.Group.Name}'.");
        }
    }
}
