using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Contracts.Results.Standard;

internal class GroupResult(string name)
{
    public string Name { get; set; } = name;
    public ConcurrentDictionary<string, TestResult> TestResults { get; } = new();

    public TestStatus Status
    {
        get
        {
            if (TestResults.Values.Any(x => x.Status == TestStatus.Failed))
                return TestStatus.Failed;

            if (TestResults.Values.All(s => s.Status == TestStatus.Skipped))
                return TestStatus.Skipped;

            return TestStatus.Passed;
        }
    }
}