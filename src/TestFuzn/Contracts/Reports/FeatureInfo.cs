namespace Fuzn.TestFuzn.Contracts.Reports;

public class FeatureInfo
{
    public string Name { get; internal set; }
    public string Id { get; internal set; }
    public Dictionary<string, string> Metadata { get; internal set; }
}