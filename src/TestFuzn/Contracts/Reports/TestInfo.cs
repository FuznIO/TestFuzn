namespace Fuzn.TestFuzn.Contracts.Reports;

internal class TestInfo
{
    public string Name { get; set; }
    public string FullName { get; set; }
    public string Id { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
