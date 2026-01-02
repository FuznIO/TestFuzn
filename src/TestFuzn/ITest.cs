using System.Reflection;

namespace Fuzn.TestFuzn;

/// <summary>
/// Interface that must be implemented by test classes to integrate with TestFuzn.
/// </summary>
public interface ITest
{
    /// <summary>
    /// Gets or sets the test framework adapter used to execute the test.
    /// </summary>
    object TestFramework { get; set; }

    /// <summary>
    /// Gets or sets the method info for the test method being executed.
    /// </summary>
    public MethodInfo TestMethodInfo { get; set; }

    /// <summary>
    /// Gets or sets the test information including metadata, tags, and identifiers.
    /// </summary>
    public TestInfo TestInfo { get; set; }
}