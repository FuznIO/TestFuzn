namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class LiveStepMetrics
{
    public string Name { get; init; } = string.Empty;

    public int OkCount { get; init; }

    public int FailedCount { get; init; }

    public int SkippedCount { get; init; }

    public double PerSecond { get; init; }

    public double AveragePerSecond { get; init; }

    public LiveStats Ok { get; init; } = LiveStats.Empty;

    public LiveStats Failed { get; init; } = LiveStats.Empty;

    public IReadOnlyList<LiveStepMetrics> Steps { get; init; } = Array.Empty<LiveStepMetrics>();
}
