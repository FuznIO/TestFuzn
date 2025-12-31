using Fuzn.TestFuzn.Contracts.Results.Feature;

namespace Fuzn.TestFuzn.Contracts.Reports;

internal class StandardReportData
{
    public SuiteInfo TestSuite { get; set; }
    public string TestRunId { get; internal set; }
    public DateTime TestRunStartTime { get; internal set; }
    public DateTime TestRunEndTime { get; internal set; }
    public TimeSpan TestRunDuration { get; internal set; }
    public string TestsOutputDirectory { get; internal set; }
    public SuiteResult Results { get; internal set; }
}
