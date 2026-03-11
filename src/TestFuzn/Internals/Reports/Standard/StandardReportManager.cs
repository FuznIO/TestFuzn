using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Internals.Results.Standard;

namespace Fuzn.TestFuzn.Internals.Reports.Standard;

internal class StandardReportManager
{
    private readonly IEnumerable<IStandardReport> _standardReports;
    private readonly TestSession _testSession;

    public StandardReportManager(IEnumerable<IStandardReport> standardReports,
        TestSession testSession)
    {
        _standardReports = standardReports;
        _testSession = testSession;
    }

    public async Task WriteStandardReports(StandardResultManager standardResultsManager)
    {
        var groupResults = standardResultsManager.GetSuiteResults();

        var data = new StandardReportData();
        data.Suite = new Contracts.Reports.SuiteInfo();
        data.Suite.Name = _testSession.Configuration.Suite.Name;
        data.Suite.Id = _testSession.Configuration.Suite.Id;
        data.Suite.Metadata = _testSession.Configuration.Suite.Metadata;
        data.TestRunId = _testSession.TestRunId;
        data.TestRunStartTime = _testSession.TestRunStartTime;
        data.TestRunEndTime = _testSession.TestRunEndTime;
        data.TestRunDuration = data.TestRunEndTime - data.TestRunStartTime;
        data.TestsOutputDirectory = _testSession.TestsOutputDirectory;
        data.GroupResults = groupResults.GroupResults;

        foreach (var standardReport in _standardReports)
            await standardReport.WriteReport(data);
    }
}
