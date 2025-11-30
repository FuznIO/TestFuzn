namespace Fuzn.TestFuzn;

public interface IFeatureTest
{
    public string FeatureName { get; }
    public string FeatureId { get; set; }
    public Dictionary<string, string> FeatureMetadata { get; set; }
}