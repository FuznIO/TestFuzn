namespace Fuzn.TestFuzn;

/// <summary>
/// Implement on input data types to expose selected properties as structured key-value
/// pairs in the XML test report. Useful when the input data type has many properties
/// and only a few are meaningful for reporting.
/// </summary>
public interface IReportableInputData
{
    KeyValueList ToReportProperties();
}
