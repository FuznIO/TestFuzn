using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Internals.Results.Standard;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Reports;

internal class ReportManager
{
    public async Task WriteStandardReports(StandardResultManager standardResultsManager)
    {
        var groupResults = standardResultsManager.GetSuiteResults();

        var data = new StandardReportData();
        data.Suite = new Contracts.Reports.SuiteInfo();
        data.Suite.Name = GlobalState.Configuration.Suite.Name;
        data.Suite.Id = GlobalState.Configuration.Suite.Id;
        data.Suite.Metadata = GlobalState.Configuration.Suite.Metadata;
        data.TestRunId = GlobalState.TestRunId;
        data.TestRunStartTime = GlobalState.TestRunStartTime;
        data.TestRunEndTime = GlobalState.TestRunEndTime;
        data.TestRunDuration = data.TestRunEndTime - data.TestRunStartTime;
        data.TestsOutputDirectory = GlobalState.TestsOutputDirectory;
        data.GroupResults = groupResults.GroupResults;
        
        foreach (var standardReport in GlobalState.Configuration.StandardReports)
            await standardReport.WriteReport(data);
    }

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
        data.TestRunStartTime = testExecutionState.TestResult.StartTime();
        data.TestRunEndTime = testExecutionState.TestResult.EndTime();
        data.TestRunDuration = testExecutionState.TestResult.TestRunDuration();
        data.Group = testInfo.Group;
        data.Test = new Contracts.Reports.TestInfo();
        data.Test.Name = testInfo.Name;
        data.Test.FullName = testInfo.FullName;
        data.Test.Id = testInfo.Id;
        data.Test.Metadata = testInfo.Metadata ?? new();
        data.Test.Tags = testInfo.Tags ?? new();
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
