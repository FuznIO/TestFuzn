namespace Fuzn.TestFuzn;

/// <summary>
/// Represents the status of a test step.
/// </summary>
public enum StepStatus
{
    /// <summary>
    /// The step completed successfully.
    /// </summary>
    Passed,

    /// <summary>
    /// The step failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The step was skipped.
    /// </summary>
    Skipped
}