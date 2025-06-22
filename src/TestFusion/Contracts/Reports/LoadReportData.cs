using TestFusion.Contracts.Results.Load;

namespace TestFusion.Contracts.Reports;

public class LoadReportData
{
    public string TestRunId { get; internal set; }
    public string TestsOutputDirectory { get; internal set; }
    public string FeatureName { get; internal set; }
    public ScenarioLoadResult ScenarioResult { get; internal set; }
}