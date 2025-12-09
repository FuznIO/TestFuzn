namespace Fuzn.TestFuzn.Contracts.Reports;

internal class TestSuiteInfo
{
    public string Name { get; set; }
    public string Id { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}