namespace Fuzn.TestFuzn.Contracts.Reports;

internal interface IFeatureReport
{
    Task WriteReport(FeatureReportData featureReportData);
}
