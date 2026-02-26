using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Execution;

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
        var testResult = new TestResult(testInfo, null);

        var timestamp = DateTime.UtcNow;
        testResult.MarkPhaseAsStarted(StandardTestPhase.Init, timestamp);
        testResult.MarkPhaseAsCompleted(StandardTestPhase.Init, timestamp);
        testResult.MarkPhaseAsStarted(StandardTestPhase.Execute, timestamp);
        testResult.MarkPhaseAsCompleted(StandardTestPhase.Execute, timestamp);
        testResult.MarkPhaseAsStarted(StandardTestPhase.Cleanup, timestamp);
        testResult.MarkPhaseAsCompleted(StandardTestPhase.Cleanup, timestamp);

        var groupResult = _suiteResult.GroupResults.GetOrAdd(testInfo.Group.Name, (key) => new GroupResult(testInfo.Group.Name));

        if (!groupResult.TestResults.TryAdd(testResult.Name, testResult))
        {
            throw new Exception($"Test name '{testResult.Name}' is duplicated, it already exists in group '{testInfo.Group.Name}'.");
        }
    }

    public void AddTestResults(TestResult testResult)
    {
        var groupResult = _suiteResult.GroupResults.GetOrAdd(testResult.Group.Name, (key) => new GroupResult(testResult.Group.Name));

        if (!groupResult.TestResults.TryAdd(testResult.Name, testResult))
        {
            throw new Exception($"Test name '{testResult.Name}' is duplicated, it already exists in group '{testResult.Group.Name}'.");
        }
    }
}
