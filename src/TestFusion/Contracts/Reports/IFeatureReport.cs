namespace TestFusion.Contracts.Reports;

public interface IFeatureReport
{
    Task WriteReport(FeatureReportData featureReportData);
}
