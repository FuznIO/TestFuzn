namespace Fuzn.TestFuzn.Contracts.Reports;

internal interface ILoadReport
{
    Task WriteReport(LoadReportData loadReportData);
}
