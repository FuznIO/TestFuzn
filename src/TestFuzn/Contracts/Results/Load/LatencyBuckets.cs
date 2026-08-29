namespace Fuzn.TestFuzn.Contracts.Results.Load;

/// <summary>
/// The fixed, log-spaced response-time buckets an <see cref="IntervalLatency"/> distributes its
/// requests over: fourteen buckets bounded above by 1, 2, 5, 10, 20, 50, 100, 200 and 500 ms
/// and 1, 2, 5, 10 and 30 s, plus an open-ended fifteenth bucket for everything slower than
/// 30 s. Every bound is inclusive and each bucket starts where the previous one ends, so a
/// response time belongs to the first bucket whose bound is at or above it
/// (<see cref="IndexOf"/>). The bounds never change at runtime, so bucket counts from different
/// intervals, steps and scenarios line up entry for entry — the rows of a latency heatmap.
/// </summary>
internal static class LatencyBuckets
{
    /// <summary>
    /// The inclusive upper bound of every bucket but the open-ended last one, ascending — a
    /// read-only wrapper, so no cast reaches a mutable array.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> UpperBounds = Array.AsReadOnly(new[]
    {
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromMilliseconds(2),
        TimeSpan.FromMilliseconds(5),
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromMilliseconds(20),
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    });

    /// <summary>The number of buckets: one per upper bound plus the open-ended last bucket.</summary>
    public const int Count = 15;

    /// <summary>
    /// The index of the bucket a response time belongs to: the first bucket whose upper bound is
    /// at or above the value (zero maps to the first bucket), or the last bucket when the value
    /// is above every bound.
    /// </summary>
    public static int IndexOf(TimeSpan responseTime)
    {
        for (var index = 0; index < UpperBounds.Count; index++)
        {
            if (responseTime <= UpperBounds[index])
                return index;
        }

        return UpperBounds.Count;
    }
}
