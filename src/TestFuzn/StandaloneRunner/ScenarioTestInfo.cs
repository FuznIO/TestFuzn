using System.Reflection;

namespace Fuzn.TestFuzn.StandaloneRunner;

internal class DiscoveredTest
{
    public string Name { get; set; }
    public Type Class { get; set; }
    public MethodInfo Method { get; set; }
}