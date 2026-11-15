namespace Fuzn.TestFuzn.Internals.Terminal;

internal readonly struct LiveMetricsSample
{
    public DateTime Timestamp { get; }

    public int OkDelta { get; }

    public int FailedDelta { get; }

    public double PerSecond { get; }

    public LiveMetricsSample(DateTime timestamp, int okDelta, int failedDelta, double perSecond)
    {
        if (okDelta < 0)
            throw new ArgumentOutOfRangeException(nameof(okDelta), okDelta, "Ok delta cannot be negative.");
        if (failedDelta < 0)
            throw new ArgumentOutOfRangeException(nameof(failedDelta), failedDelta, "Failed delta cannot be negative.");
        if (perSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(perSecond), perSecond, "Iterations per second cannot be negative.");

        Timestamp = timestamp;
        OkDelta = okDelta;
        FailedDelta = failedDelta;
        PerSecond = perSecond;
    }
}
