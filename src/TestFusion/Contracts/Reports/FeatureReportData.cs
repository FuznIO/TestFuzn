using TestFusion.Contracts.Results.Feature;

namespace TestFusion.Contracts.Reports;

public class FeatureReportData
{
    public string TestSuiteName { get; internal set; }
    public string TestRunId { get; internal set; }
    public string TestsOutputDirectory { get; internal set; }
    public TestSuiteFeatureResult Results { get; internal set; }
}
