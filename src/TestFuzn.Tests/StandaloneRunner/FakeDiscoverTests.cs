using System.Reflection;
using Fuzn.TestFuzn.StandaloneRunner;

namespace Fuzn.TestFuzn.Tests.StandaloneRunner;

/// <summary>
/// Test discovery that answers a hand-made list instead of scanning an assembly, so the runner
/// core can be driven over tests that never run: every test it makes names
/// <see cref="NotATestClass"/> as its class, which the test runner rejects right after the
/// startup banner — before the process-wide default session, which the test suite's own tests
/// resolve in parallel, is touched.
/// </summary>
internal sealed class FakeDiscoverTests : DiscoverTests
{
    private readonly List<DiscoveredTest> _tests;

    public FakeDiscoverTests(params string[] testNames)
    {
        _tests = testNames.Select(Test).ToList();
    }

    /// <summary>The assemblies discovery was asked for, in order.</summary>
    public List<Assembly> Assemblies { get; } = new List<Assembly>();

    /// <summary>A discovered test with the given full name whose class is <see cref="NotATestClass"/>.</summary>
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

/// <summary>A class that is not a test class: the runner rejects it for not implementing <see cref="ITest"/>.</summary>
internal sealed class NotATestClass
{
    public void Run()
    {
    }
}
