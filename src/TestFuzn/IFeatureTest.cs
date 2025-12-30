using System.Reflection;

namespace Fuzn.TestFuzn;

public interface IFeatureTest
{
    object TestFramework { get; set; }
    public MethodInfo TestMethodInfo { get; set; }
    public GroupInfo Group { get; }
    public FeatureTestInfo Test { get; set; }
}