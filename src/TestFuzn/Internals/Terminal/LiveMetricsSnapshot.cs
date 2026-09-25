using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class LiveMetricsSnapshot
{
    public string ScenarioName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public LoadTestPhase Phase { get; init; }

    public TimeSpan Duration { get; init; }

    public DateTime Timestamp { get; init; }

    public TestPhasesLayout.PhaseTimes PhaseTimes { get; init; }

    public bool HasWarmup { get; init; }

    public IReadOnlyList<SimulationInfo> Simulations { get; init; } = Array.Empty<SimulationInfo>();

    public bool IsCompleted { get; init; }

    public TestStatus Status { get; init; }

    public string? StatusDetail { get; init; }

    public Exception? AssertWhileWarmingUpException { get; init; }

    public Exception? AssertWhileRunningException { get; init; }

    public Exception? AssertWhenDoneException { get; init; }

    public int RequestCountOk { get; init; }

    public int RequestCountFailed { get; init; }

    public int WarmupRequestCountOk { get; init; }

    public int WarmupRequestCountFailed { get; init; }

    public int RequestsPerSecond { get; init; }

    public LiveStats Ok { get; init; } = LiveStats.Empty;

    public LiveStats Failed { get; init; } = LiveStats.Empty;

    public IReadOnlyList<LiveMetricsSample> Samples { get; init; } = Array.Empty<LiveMetricsSample>();

    public IReadOnlyList<double> RequestsPerSecondSeries { get; init; } = Array.Empty<double>();

    public IReadOnlyList<LiveErrorEntry> Errors { get; init; } = Array.Empty<LiveErrorEntry>();

    public IReadOnlyList<LiveStepMetrics> Steps { get; init; } = Array.Empty<LiveStepMetrics>();
}
