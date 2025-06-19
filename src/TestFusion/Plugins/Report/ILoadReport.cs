namespace TestFusion.Plugins.Report;

public interface ILoadReport
{
    Task WriteReport(LoadReportData loadReportData);
}
