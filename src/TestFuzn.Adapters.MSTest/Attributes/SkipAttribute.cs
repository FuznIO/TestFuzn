namespace Fuzn.TestFuzn;

/// <summary>
/// Marks a test method or class to be skipped during test execution.
/// Skipped tests will not be executed and will be reported as skipped in the test results.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class SkipAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the reason for skipping the test.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkipAttribute"/> class.
    /// </summary>
    /// <param name="reason">The reason for skipping the test. Optional.</param>
    public SkipAttribute(string reason = "")
    {
        Reason = reason;
    }
}
