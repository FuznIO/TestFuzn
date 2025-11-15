namespace Fuzn.TestFuzn.Contracts.Reports;

public interface ILoadReport
{
    Task WriteReport(LoadReportData loadReportData);
}
