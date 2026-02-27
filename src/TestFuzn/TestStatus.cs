namespace Fuzn.TestFuzn;

/// <summary>
/// Represents the status of a test.
/// </summary>
public enum TestStatus
{
    /// <summary>
    /// The test completed successfully.
    /// </summary>
    Passed,

    /// <summary>
    /// The test failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The test was skipped.
    /// </summary>
    Skipped
}