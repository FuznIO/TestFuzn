using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Contracts.Reports;

public class LoadReportData
{
    public TestSuiteInfo TestSuite { get; set; }
    public string TestRunId { get; internal set; }
    public string TestsOutputDirectory { get; internal set; }
    public FeatureInfo Feature { get; internal set; }
    public ScenarioLoadResult ScenarioResult { get; internal set; }
}
