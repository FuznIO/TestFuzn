using TestFusion.Contracts.Reports;
using TestFusion.Internals.Results.Feature;
using TestFusion.Internals.State;

namespace TestFusion.Internals.Reports;

internal class ReportManager
{
    public async Task WriteFeatureReports(FeatureResultManager featureResultsManager)
    {
        var featureResults = featureResultsManager.GetTestSuiteResults();

        var data = new FeatureReportData();
        data.TestSuiteName = GlobalState.Configuration.TestSuiteName;
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
            var loadReportData = new LoadReportData();
            loadReportData.TestRunId = GlobalState.TestRunId;
            loadReportData.FeatureName = sharedExecutionState.FeatureName;
            loadReportData.TestsOutputDirectory = GlobalState.TestsOutputDirectory;
            loadReportData.ScenarioResult = sharedExecutionState.ResultState.LoadCollectors[scenario.Name].GetCurrentResult(true);

            foreach (var loadReport in loadReports)
                await loadReport.WriteReport(loadReportData);
        }
    }
}
