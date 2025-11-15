namespace Fuzn.TestFuzn.Contracts.Reports;

public interface IFeatureReport
{
    Task WriteReport(FeatureReportData featureReportData);
}
