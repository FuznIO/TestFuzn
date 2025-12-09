using System.Reflection;

namespace Fuzn.TestFuzn;

public interface IFeatureTest
{
    object TestFramework { get; set; }
    public MethodInfo TestMethodInfo { get; set; }
    public string FeatureName { get; }
    public string FeatureId { get; set; }
    public Dictionary<string, string> FeatureMetadata { get; set; }
}