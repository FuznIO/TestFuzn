using System.Reflection;

namespace Fuzn.TestFuzn;

public interface IFeatureTest
{
    object TestFramework { get; set; }
    public MethodInfo TestMethodInfo { get; set; }
    public TestInfo TestInfo { get; set; }
}