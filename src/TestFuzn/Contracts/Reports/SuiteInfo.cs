namespace Fuzn.TestFuzn.Contracts.Reports;

internal class SuiteInfo
{
    public string Name { get; set; }
    public string Id { get; set; }
    public KeyValueList Metadata { get; set; } = new();
}