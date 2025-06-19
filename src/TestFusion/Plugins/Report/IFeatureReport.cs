namespace TestFusion.Plugins.Report;

public interface IFeatureReport
{
    Task WriteReport(FeatureReportData featureReportData);
}
