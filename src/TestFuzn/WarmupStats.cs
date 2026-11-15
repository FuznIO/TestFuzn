namespace Fuzn.TestFuzn;

/// <summary>
/// Provides warmup phase statistics for assertions during warmup.
/// </summary>
public class WarmupStats
{
    /// <summary>
    /// Gets the number of warmup iterations.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the counts of the ok warmup iterations.
    /// </summary>
    public WarmupOutcomeStats Ok { get; }

    /// <summary>
    /// Gets the counts of the failed warmup iterations.
    /// </summary>
    public WarmupOutcomeStats Failed { get; }

    /// <summary>
    /// Gets the share of ok warmup iterations (0.0 to 1.0).
    /// </summary>
    public double OkRate => Count == 0 ? 0 : (double) Ok.Count / Count;

    /// <summary>
    /// Gets the share of failed warmup iterations (0.0 to 1.0).
    /// </summary>
    public double FailedRate => Count == 0 ? 0 : (double) Failed.Count / Count;

    /// <summary>
    /// Gets the time since the warmup phase started.
    /// </summary>
    public TimeSpan Elapsed { get; }

    internal WarmupStats(int okCount, int failedCount, TimeSpan elapsed)
    {
        Ok = new WarmupOutcomeStats(okCount);
        Failed = new WarmupOutcomeStats(failedCount);
        Count = okCount + failedCount;
        Elapsed = elapsed;
    }
}
