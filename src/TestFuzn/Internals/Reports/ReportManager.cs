using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Internals.Results.Feature;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Reports;

internal class ReportManager
{
    public async Task WriteFeatureReports(FeatureResultManager featureResultsManager)
    {
        var featureResults = featureResultsManager.GetTestSuiteResults();

        var data = new FeatureReportData();
        data.TestSuite = new Contracts.Reports.TestSuiteInfo();
        data.TestSuite.Name = GlobalState.Configuration.TestSuite.Name;
        data.TestSuite.Id = GlobalState.Configuration.TestSuite.Id;
        data.TestSuite.Metadata = GlobalState.Configuration.TestSuite.Metadata;
        data.TestRunId = GlobalState.TestRunId;
        data.TestRunStartTime = GlobalState.TestRunStartTime;
        data.TestRunEndTime = GlobalState.TestRunEndTime;
        data.TestRunDuration = data.TestRunEndTime - data.TestRunStartTime;
        data.TestsOutputDirectory = GlobalState.TestsOutputDirectory;
        data.Results = featureResults;
        
        foreach (var featureReport in GlobalState.Configuration.FeatureReports)
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
            data.TestSuite = new Contracts.Reports.TestSuiteInfo();
            data.TestSuite.Name = GlobalState.Configuration.TestSuite.Name;
            data.TestSuite.Id = GlobalState.Configuration.TestSuite.Id;
            data.TestSuite.Metadata = GlobalState.Configuration.TestSuite.Metadata;
            data.TestRunId = GlobalState.TestRunId;
            data.Group = new Contracts.Reports.GroupInfo();
            data.Group.Name = sharedExecutionState.IFeatureTestClassInstance.Group.Name;
            data.Group.Id = sharedExecutionState.IFeatureTestClassInstance.Group.Id;
            data.Group.Metadata = sharedExecutionState.IFeatureTestClassInstance.Group.Metadata;
            data.TestsOutputDirectory = GlobalState.TestsOutputDirectory;
            data.ScenarioResult = sharedExecutionState.ResultState.LoadCollectors[scenario.Name].GetCurrentResult(true);

            foreach (var loadReport in loadReports)
                await loadReport.WriteReport(data);
        }
    }
}
