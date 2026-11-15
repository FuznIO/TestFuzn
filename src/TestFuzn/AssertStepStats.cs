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
    /// Gets the number of times the step ran.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the statistics of the ok step runs.
    /// </summary>
    public AssertStats Ok { get; }

    /// <summary>
    /// Gets the statistics of the failed step runs.
    /// </summary>
    public AssertStats Failed { get; }

    /// <summary>
    /// Gets the share of ok step runs (0.0 to 1.0).
    /// </summary>
    public double OkRate { get; }

    /// <summary>
    /// Gets the share of failed step runs (0.0 to 1.0).
    /// </summary>
    public double FailedRate { get; }

    internal AssertStepStats(StepLoadResult stepResult)
    {
        StepName = stepResult.Name;
        Ok = new AssertStats(stepResult.Ok);
        Failed = new AssertStats(stepResult.Failed);
        Count = Ok.Count + Failed.Count;
        OkRate = Count == 0 ? 0 : (double)Ok.Count / Count;
        FailedRate = Count == 0 ? 0 : (double)Failed.Count / Count;
    }
}
