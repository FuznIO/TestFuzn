namespace Fuzn.TestFuzn.Internals.Terminal;

internal readonly struct LiveMetricsSample
{
    public DateTime Timestamp { get; }

    public int OkDelta { get; }

    public int FailedDelta { get; }

    public double RequestsPerSecond { get; }

    public LiveMetricsSample(DateTime timestamp, int okDelta, int failedDelta, double requestsPerSecond)
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
    }
}
