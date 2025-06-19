using TestFusion.Results.Feature;

namespace TestFusion.Plugins.Report;

public class FeatureReportData
{
    public string TestSuiteName { get; internal set; }
    public string TestRunId { get; internal set; }
    public string TestsOutputDirectory { get; internal set; }
    public TestSuiteResults Results { get; internal set; }
}
