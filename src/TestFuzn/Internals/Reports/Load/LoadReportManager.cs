using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Reports;
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

        var testInfo = testExecutionState.TestClassInstance.TestInfo;

        var data = new LoadReportData();
        data.Suite = new Contracts.Reports.SuiteInfo();
        data.Suite.Name = _testSession.Configuration.Suite.Name;
        data.Suite.Id = _testSession.Configuration.Suite.Id;
        data.Suite.Metadata = _testSession.Configuration.Suite.Metadata;
        data.TestRunId = _testSession.TestRunId;
        data.Test = testExecutionState.TestResult;
        data.TestsOutputDirectory = _testSession.TestsOutputDirectory;

        foreach (var scenario in testExecutionState.Scenarios)
        {
            var scenarioResult = testExecutionState.LoadCollectors[scenario.Name].GetCurrentResult(true);
            data.ScenarioResults.Add(scenarioResult);
            data.Snapshots.Add(scenario.Name, testExecutionState.LoadSnapshotCollector.GetSnapshots(scenario.Name));
        }

        foreach (var loadReport in _loadReports)
            await loadReport.WriteReport(data);
    }
}
