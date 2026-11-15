using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Reports.Load;

internal class LoadReportManager
{
    private readonly IEnumerable<ILoadReport> _loadReports;
    private readonly TestSession _testSession;

    public LoadReportManager(IEnumerable<ILoadReport> loadReports,
        TestSession testSession)
    {
        _loadReports = loadReports;
        _testSession = testSession;
    }

    public async Task WriteLoadReports(TestExecutionState testExecutionState)
    {
        if (testExecutionState.TestResult.TestType != TestType.Load)
            return;

        if (!_loadReports.Any())
            return;

        // A load child writes no report, and a remote client downloads the controller's.
        var cluster = _testSession.Cluster;
        if (!cluster.ReportsEnabled || cluster.RunLoadTestsOnCluster)
            return;

        var testInfo = testExecutionState.TestClassInstance.TestInfo;

        var data = new LoadReportData();
        data.Suite = new Contracts.Reports.SuiteInfo();
        data.Suite.Name = _testSession.Configuration.Suite.Name;
        data.Suite.Id = _testSession.Configuration.Suite.Id;
        data.Suite.Metadata = _testSession.Configuration.Suite.Metadata;
        data.TestRunId = _testSession.TestRunId;
        data.Test = testExecutionState.TestResult;
        data.TestsResultsDirectory = _testSession.TestsResultsDirectory;
        data.Workers = testExecutionState.Workers;
        data.Warnings = testExecutionState.Warnings;

        foreach (var scenario in testExecutionState.Scenarios)
        {
            var collector = testExecutionState.LoadCollectors[scenario.Name];
            var scenarioResult = collector.GetCurrentResult(true);
            data.ScenarioResults.Add(scenarioResult);
            var snapshots = WithFinalSnapshot(testExecutionState.LoadSnapshotCollector.GetSnapshots(scenario.Name), scenarioResult, collector.GetOkState());
            data.Snapshots.Add(scenario.Name, snapshots.Select(snapshot => snapshot.Result).ToList());
            data.Timelines.Add(scenario.Name, EvenlySpreadSnapshots.Timeline(snapshots, scenario.SimulationsInternal));
            var state = collector.GetState();
            data.Distributions.Add(scenario.Name, DurationDistribution.From(state.Ok, state.Failed));
        }

        foreach (var loadReport in _loadReports)
            await loadReport.WriteReport(data);
    }

    private static IReadOnlyList<LoadSnapshot> WithFinalSnapshot(IReadOnlyList<LoadSnapshot> snapshots, ScenarioLoadResult result, StatsState ok)
    {
        var total = (result.Ok?.Count ?? 0) + (result.Failed?.Count ?? 0);
        var last = snapshots.Count > 0 ? snapshots[^1].Result : null;
        if (last != null && (last.Ok?.Count ?? 0) + (last.Failed?.Count ?? 0) >= total)
            return snapshots;

        var end = result.MeasurementEndTime != default ? result.MeasurementEndTime : DateTime.UtcNow;
        return [.. snapshots, new LoadSnapshot(end, result, ok)];
    }
}
