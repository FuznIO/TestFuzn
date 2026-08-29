namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One per-second entry in the live metrics ring buffer: what changed between two consecutive
/// 1 Hz snapshots of a scenario's load collector. <see cref="OkDelta"/> and
/// <see cref="FailedDelta"/> are the request-count increases over the interval that ended at
/// <see cref="Timestamp"/>, counted across warmup and measurement together so the series stays
/// continuous through the warmup-to-measurement transition. <see cref="RequestsPerSecond"/> is
/// the current-interval rate — the combined delta divided by the interval's actual length in
/// seconds, so a late or early tick still yields an honest rate. <see cref="ResponseTimePercentile95"/>
/// is the per-interval Ok p95 reported by the snapshot that closed the interval (its
/// IntervalLatency's ResponseTimePercentile95 — the p95 of just the requests recorded since the
/// previous force-refreshed snapshot, which in the 1 Hz wiring is exactly this sample's
/// interval), carried per sample so a p95-over-time sparkline reacts to latency shifts within
/// one tick.
/// </summary>
internal readonly struct LiveMetricsSample
{
    /// <summary>The time of the snapshot that closed this interval.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Requests that completed successfully during this interval (warmup + measurement).</summary>
    public int OkDelta { get; }

    /// <summary>Requests that failed during this interval (warmup + measurement).</summary>
    public int FailedDelta { get; }

    /// <summary>The combined ok + failed delta divided by the interval length in seconds.</summary>
    public double RequestsPerSecond { get; }

    /// <summary>The 95th-percentile response time of just the successful requests recorded during this interval.</summary>
    public TimeSpan ResponseTimePercentile95 { get; }

    public LiveMetricsSample(DateTime timestamp, int okDelta, int failedDelta, double requestsPerSecond, TimeSpan responseTimePercentile95)
    {
        if (okDelta < 0)
            throw new ArgumentOutOfRangeException(nameof(okDelta), okDelta, "Ok delta cannot be negative.");
        if (failedDelta < 0)
            throw new ArgumentOutOfRangeException(nameof(failedDelta), failedDelta, "Failed delta cannot be negative.");
        if (requestsPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(requestsPerSecond), requestsPerSecond, "Requests per second cannot be negative.");

        Timestamp = timestamp;
        OkDelta = okDelta;
        FailedDelta = failedDelta;
        RequestsPerSecond = requestsPerSecond;
        ResponseTimePercentile95 = responseTimePercentile95;
    }
}
