using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Internals.Results.Standard;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Reports;

internal class ReportManager
{
    public async Task WriteStandardReports(StandardResultManager featureResultsManager)
    {
        var featureResults = featureResultsManager.GetSuiteResults();

        var data = new StandardReportData();
        data.Suite = new SuiteInfo();
        data.Suite.Name = GlobalState.Configuration.TestSuite.Name;
        data.Suite.Id = GlobalState.Configuration.TestSuite.Id;
        data.Suite.Metadata = GlobalState.Configuration.TestSuite.Metadata;
        data.TestRunId = GlobalState.TestRunId;
        data.TestRunStartTime = GlobalState.TestRunStartTime;
        data.TestRunEndTime = GlobalState.TestRunEndTime;
        data.TestRunDuration = data.TestRunEndTime - data.TestRunStartTime;
        data.TestsOutputDirectory = GlobalState.TestsOutputDirectory;
        data.GroupResults = featureResults.GroupResults;
        
        foreach (var featureReport in GlobalState.Configuration.StandardReports)
            await featureReport.WriteReport(data);
    }

    public async Task WriteLoadReports(SharedExecutionState sharedExecutionState)
    {
        if (sharedExecutionState.TestType != TestType.Load)
            return;

        var loadReports = GlobalState.Configuration.LoadReports;

        if (loadReports == null || loadReports.Count == 0)
            return;

        foreach (var scenario in sharedExecutionState.Scenarios)
        {
            var data = new LoadReportData();
            data.TestSuite = new Contracts.Reports.SuiteInfo();
            data.TestSuite.Name = GlobalState.Configuration.TestSuite.Name;
            data.TestSuite.Id = GlobalState.Configuration.TestSuite.Id;
            data.TestSuite.Metadata = GlobalState.Configuration.TestSuite.Metadata;
            data.TestRunId = GlobalState.TestRunId;
            data.Group = new Contracts.Reports.GroupInfo();
            data.Group.Name = sharedExecutionState.TestClassInstance.TestInfo.Group.Name;
            data.TestsOutputDirectory = GlobalState.TestsOutputDirectory;
            data.ScenarioResult = sharedExecutionState.ScenarioResultState.LoadCollectors[scenario.Name].GetCurrentResult(true);

            foreach (var loadReport in loadReports)
                await loadReport.WriteReport(data);
        }
    }
}
