namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class LiveStepMetrics
{
    public string Name { get; init; } = string.Empty;

    public int RequestCountOk { get; init; }

    public int RequestCountFailed { get; init; }

    public int SkippedCount { get; init; }

    public double RequestsPerSecond { get; init; }

    public double AverageRequestsPerSecond { get; init; }

    public LiveStats Ok { get; init; } = LiveStats.Empty;

    public LiveStats Failed { get; init; } = LiveStats.Empty;

    public IReadOnlyList<LiveStepMetrics> Steps { get; init; } = Array.Empty<LiveStepMetrics>();
}
