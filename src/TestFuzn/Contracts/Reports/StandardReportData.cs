using Fuzn.TestFuzn.Contracts.Results.Standard;
using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Contracts.Reports;

internal class StandardReportData
{
    public SuiteInfo Suite { get; set; }
    public string TestRunId { get; internal set; }
    public DateTime TestRunStartTime { get; internal set; }
    public DateTime TestRunEndTime { get; internal set; }
    public TimeSpan TestRunDuration { get; internal set; }
    public string TestsOutputDirectory { get; internal set; }
    public ConcurrentDictionary<string, GroupResult> GroupResults { get; internal set; }
}
