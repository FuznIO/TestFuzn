using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides load test statistics for a specific step for assertions.
/// </summary>
public class AssertStepStats
{
    /// <summary>
    /// Gets the name of the step.
    /// </summary>
    public string StepName { get; }

    /// <summary>
    /// Gets the statistics for successful executions of the step.
    /// </summary>
    public AssertStats Ok { get; }

    /// <summary>
    /// Gets the statistics for failed executions of the step.
    /// </summary>
    public AssertStats Failed { get; }

    internal AssertStepStats(StepLoadResult stepResult)
    {
        StepName = stepResult.Name;
        Ok = new AssertStats(stepResult.Ok);
        Failed = new AssertStats(stepResult.Failed);
    }
}
