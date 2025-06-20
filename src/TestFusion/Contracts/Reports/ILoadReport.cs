namespace TestFusion.Contracts.Reports;

public interface ILoadReport
{
    Task WriteReport(LoadReportData loadReportData);
}
