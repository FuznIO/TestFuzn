using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Contracts.Reports;

internal class LoadReportData
{
    public SuiteInfo Suite { get; set; } = null!;
    public string TestRunId { get; internal set; } = null!;
    public string TestsResultsDirectory { get; internal set; } = null!;
    public TestResult Test { get; internal set; } = null!;
    // Totals since the start, at each snapshot (the snapshot table).
    public Dictionary<string, IReadOnlyList<ScenarioLoadResult>> Snapshots { get; internal set; } = new();

    // Each interval between snapshots on its own (the charts over time).
    public Dictionary<string, IReadOnlyList<LoadTimelinePoint>> Timelines { get; internal set; } = new();
    public Dictionary<string, IReadOnlyList<DurationBucket>> Distributions { get; internal set; } = new();
    public List<ScenarioLoadResult> ScenarioResults { get; internal set; } = new();
    public List<WorkerSummary> Workers { get; internal set; } = new();
    public List<string> Warnings { get; internal set; } = new();
}
