namespace Fuzn.TestFuzn.Contracts.Reports;

internal interface IStandardReport
{
    Task WriteReport(StandardReportData standardReportData);
}
