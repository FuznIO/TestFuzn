using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;

namespace Fuzn.TestFuzn.Contracts.Reports;

internal class LoadReportData
{
    public SuiteInfo Suite { get; set; } = null!;
    public string TestRunId { get; internal set; } = null!;
    public string TestsResultsDirectory { get; internal set; } = null!;
    public TestResult Test { get; internal set; } = null!;
    public Dictionary<string, IReadOnlyList<ScenarioLoadResult>> Snapshots { get; internal set; } = new();
    public List<ScenarioLoadResult> ScenarioResults { get; internal set; } = new();
}
