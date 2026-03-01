using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Reports.Load;

internal class LoadReportManager
{
    
    public async Task WriteLoadReports(TestExecutionState testExecutionState)
    {
        if (testExecutionState.TestResult.TestType != TestType.Load)
            return;

        var loadReports = GlobalState.Configuration.LoadReports;

        if (loadReports == null || loadReports.Count == 0)
            return;

        var testInfo = testExecutionState.TestClassInstance.TestInfo;

        var data = new LoadReportData();
        data.Suite = new Contracts.Reports.SuiteInfo();
        data.Suite.Name = GlobalState.Configuration.Suite.Name;
        data.Suite.Id = GlobalState.Configuration.Suite.Id;
        data.Suite.Metadata = GlobalState.Configuration.Suite.Metadata;
        data.TestRunId = GlobalState.TestRunId;
        data.Test = testExecutionState.TestResult;
        data.TestsOutputDirectory = GlobalState.TestsOutputDirectory;

        foreach (var scenario in testExecutionState.Scenarios)
        {
            var scenarioResult = testExecutionState.LoadCollectors[scenario.Name].GetCurrentResult(true);
            data.ScenarioResults.Add(scenarioResult);
        }

        foreach (var loadReport in loadReports)
            await loadReport.WriteReport(data);
    }
}
