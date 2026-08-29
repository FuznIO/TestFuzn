namespace Fuzn.TestFuzn.Contracts.Results.Load;

/// <summary>
/// The response-time distribution of the successful requests recorded during one live-view
/// interval — the requests since the previous force-refreshed result — as opposed to the
/// cumulative <see cref="Stats"/>. Immutable: built once when an interval closes and shared by
/// every result created until the next interval closes. <see cref="BucketCounts"/> has one
/// entry per <see cref="LatencyBuckets"/> bucket, in bucket order, and the entries sum to
/// <see cref="RequestCount"/>. <see cref="Empty"/> stands in when the interval had no
/// successful requests or interval tracking is off.
///
/// Rounding convention: the three percentiles are HdrHistogram's highest-equivalent values at
/// 3 significant digits (the same convention as the cumulative <see cref="Stats"/>), while
/// <see cref="BucketCounts"/> places every recorded value by its lowest-equivalent value. A
/// percentile that lands exactly on a bucket's upper bound can therefore read up to 0.1 % above
/// the bound and name the NEXT bucket — never map a percentile onto a bucket index and expect
/// the observations behind it to sit in that bucket.
/// </summary>
internal sealed class IntervalLatency
{
    /// <summary>An interval with no requests: zero count, zero percentiles, every bucket count zero.</summary>
    public static readonly IntervalLatency Empty = new IntervalLatency(0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, new int[LatencyBuckets.Count]);

    private readonly IReadOnlyList<int> _bucketCounts;

    /// <summary>Successful requests recorded during the interval.</summary>
    public int RequestCount { get; }

    /// <summary>The median response time of the interval's requests; zero when the interval is empty.</summary>
    public TimeSpan ResponseTimeMedian { get; }

    /// <summary>The 95th-percentile response time of the interval's requests; zero when the interval is empty.</summary>
    public TimeSpan ResponseTimePercentile95 { get; }

    /// <summary>The 99th-percentile response time of the interval's requests; zero when the interval is empty.</summary>
    public TimeSpan ResponseTimePercentile99 { get; }

    /// <summary>
    /// How many of the interval's requests fell into each <see cref="LatencyBuckets"/> bucket, in
    /// bucket order — always <see cref="LatencyBuckets.Count"/> entries, never changing after
    /// construction: a read-only wrapper over a private copy, so no cast reaches a mutable array.
    /// </summary>
    public IReadOnlyList<int> BucketCounts => _bucketCounts;

    /// <summary>
    /// <paramref name="bucketCounts"/> must hold exactly <see cref="LatencyBuckets.Count"/>
    /// non-negative entries; they are copied, so the caller's array can be reused afterwards.
    /// </summary>
    public IntervalLatency(int requestCount, TimeSpan responseTimeMedian, TimeSpan responseTimePercentile95, TimeSpan responseTimePercentile99, IReadOnlyList<int> bucketCounts)
    {
        if (requestCount < 0)
            throw new ArgumentOutOfRangeException(nameof(requestCount), requestCount, "Request count cannot be negative.");
        if (bucketCounts == null)
            throw new ArgumentNullException(nameof(bucketCounts), "Bucket counts cannot be null.");
        if (bucketCounts.Count != LatencyBuckets.Count)
            throw new ArgumentException($"Bucket counts must have exactly {LatencyBuckets.Count} entries, one per latency bucket, but has {bucketCounts.Count}.", nameof(bucketCounts));

        var copy = new int[LatencyBuckets.Count];
        for (var index = 0; index < copy.Length; index++)
        {
            if (bucketCounts[index] < 0)
                throw new ArgumentOutOfRangeException(nameof(bucketCounts), bucketCounts[index], $"Bucket count at index {index} cannot be negative.");

            copy[index] = bucketCounts[index];
        }

        _bucketCounts = Array.AsReadOnly(copy);
        RequestCount = requestCount;
        ResponseTimeMedian = responseTimeMedian;
        ResponseTimePercentile95 = responseTimePercentile95;
        ResponseTimePercentile99 = responseTimePercentile99;
    }
}
