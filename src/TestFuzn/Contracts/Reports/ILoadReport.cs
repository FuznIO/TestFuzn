namespace FuznLabs.TestFuzn.Contracts.Reports;

public interface ILoadReport
{
    Task WriteReport(LoadReportData loadReportData);
}
