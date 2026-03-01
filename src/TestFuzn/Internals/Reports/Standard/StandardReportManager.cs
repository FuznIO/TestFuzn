using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Internals.Results.Standard;

namespace Fuzn.TestFuzn.Internals.Reports.Standard;

internal class StandardReportManager
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
}
