using System.Reflection;

namespace Fuzn.TestFuzn;

public interface IFeatureTest
{
    object TestFramework { get; set; }
    public MethodInfo TestMethodInfo { get; set; }
    public FeatureInfo Feature { get; }
    public FeatureTestInfo Test { get; set; }
}