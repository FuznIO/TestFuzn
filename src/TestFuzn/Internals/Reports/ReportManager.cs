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
        data.Suite = new Contracts.Reports.SuiteInfo();
        data.Suite.Name = GlobalState.Configuration.Suite.Name;
        data.Suite.Id = GlobalState.Configuration.Suite.Id;
        data.Suite.Metadata = GlobalState.Configuration.Suite.Metadata;
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

        var testInfo = sharedExecutionState.TestClassInstance.TestInfo;

        var data = new LoadReportData();
        data.TestSuite = new Contracts.Reports.SuiteInfo();
        data.TestSuite.Name = GlobalState.Configuration.Suite.Name;
        data.TestSuite.Id = GlobalState.Configuration.Suite.Id;
        data.TestSuite.Metadata = GlobalState.Configuration.Suite.Metadata;
        data.TestRunId = GlobalState.TestRunId;
        data.Group = new Contracts.Reports.GroupInfo();
        data.Group.Name = testInfo.Group.Name;
        data.Test = new Contracts.Reports.TestInfo();
        data.Test.Name = testInfo.Name;
        data.Test.FullName = testInfo.FullName;
        data.Test.Id = testInfo.Id;
        data.Test.Metadata = testInfo.Metadata ?? new();
        data.Test.Tags = testInfo.Tags ?? new();
        data.TestsOutputDirectory = GlobalState.TestsOutputDirectory;

        foreach (var scenario in sharedExecutionState.Scenarios)
        {
            var scenarioResult = sharedExecutionState.ScenarioResultState.LoadCollectors[scenario.Name].GetCurrentResult(true);
            data.ScenarioResults.Add(scenarioResult);
        }

        foreach (var loadReport in loadReports)
            await loadReport.WriteReport(data);
    }
}
