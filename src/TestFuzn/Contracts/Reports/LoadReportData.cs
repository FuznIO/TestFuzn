using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;

namespace Fuzn.TestFuzn.Contracts.Reports;

internal class LoadReportData
{
    public SuiteInfo Suite { get; set; }
    public string TestRunId { get; internal set; }
    public string TestsOutputDirectory { get; internal set; }
    public TestResult Test { get; internal set; }
    public List<ScenarioLoadResult> ScenarioResults { get; internal set; } = new();
}
