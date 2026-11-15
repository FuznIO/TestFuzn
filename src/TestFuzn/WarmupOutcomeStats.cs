namespace Fuzn.TestFuzn;

/// <summary>
/// Provides the count of the ok or the failed warmup iterations.
/// </summary>
public class WarmupOutcomeStats
{
    /// <summary>
    /// Gets the number of warmup iterations.
    /// </summary>
    public int Count { get; }

    internal WarmupOutcomeStats(int count)
    {
        Count = count;
    }
}
