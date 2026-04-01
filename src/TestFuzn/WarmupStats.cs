namespace Fuzn.TestFuzn;

/// <summary>
/// Provides warmup phase statistics for assertions during warmup.
/// </summary>
public class WarmupStats
{
    /// <summary>
    /// Gets the number of successful warmup iterations.
    /// </summary>
    public int OkCount { get; }

    /// <summary>
    /// Gets the number of failed warmup iterations.
    /// </summary>
    public int FailedCount { get; }

    /// <summary>
    /// Gets the total number of warmup iterations (OkCount + FailedCount).
    /// </summary>
    public int TotalCount => OkCount + FailedCount;

    /// <summary>
    /// Gets the rate of successful warmup iterations (0.0 to 1.0).
    /// </summary>
    public double OkRate => TotalCount == 0 ? 0 : (double) OkCount / TotalCount;

    /// <summary>
    /// Gets the rate of failed warmup iterations (0.0 to 1.0).
    /// </summary>
    public double FailedRate => TotalCount == 0 ? 0 : (double) FailedCount / TotalCount;

    /// <summary>
    /// Gets the elapsed time since the warmup phase started.
    /// </summary>
    public TimeSpan Duration { get; }

    internal WarmupStats(int okCount, int failedCount, TimeSpan elapsed)
    {
        OkCount = okCount;
        FailedCount = failedCount;
        Duration = elapsed;
    }
}
