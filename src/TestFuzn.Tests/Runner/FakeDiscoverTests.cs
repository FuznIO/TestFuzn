using System.Reflection;
using Fuzn.TestFuzn.Runner;

namespace Fuzn.TestFuzn.Tests.Runner;

internal sealed class FakeDiscoverTests : DiscoverTests
{
    private readonly List<DiscoveredTest> _tests;

    public FakeDiscoverTests(params string[] testNames)
    {
        _tests = testNames.Select(Test).ToList();
    }

    public List<Assembly> Assemblies { get; } = new List<Assembly>();

    public static DiscoveredTest Test(string name)
    {
        return new DiscoveredTest
        {
            Name = name,
            Class = typeof(NotATestClass),
            Method = typeof(NotATestClass).GetMethod(nameof(NotATestClass.Run))!
        };
    }

    public override List<DiscoveredTest> GetTests(Assembly assembly)
    {
        Assemblies.Add(assembly);
        return _tests.ToList();
    }
}

internal sealed class NotATestClass
{
    public void Run()
    {
    }
}
