using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Contracts.Sinks;

// What a sink gets on every write: the cumulative result (the report's numbers) and what happened since
// the previous write (what a live chart shows). A cumulative p95 flattens as a run goes on and hides spikes.
internal class SinkStats
{
    public string TestRunId { get; init; } = "";
    public string GroupName { get; init; } = "";
    public string TestName { get; init; } = "";
    public ScenarioLoadResult Scenario { get; init; } = null!;

    // One point for the scenario and one per step, over the interval since the previous write.
    public IReadOnlyList<IntervalPoint> Points { get; init; } = Array.Empty<IntervalPoint>();

    // One row per load machine: the workers of a cluster run, or this machine for a local run.
    public IReadOnlyList<WorkerSummary> Generators { get; init; } = Array.Empty<WorkerSummary>();
}

internal class IntervalPoint
{
    // "Scenario" or "Scenario > Step > Sub step".
    public string Path { get; init; } = "";
    public string Phase { get; init; } = "";

    // Time since the measurement started, at the end of the interval.
    public long TimeMs { get; init; }
    public int OkCount { get; init; }
    public int FailedCount { get; init; }

    // Of the successful samples in the interval; null when the interval had none.
    public double? MeanMs { get; init; }
    public double? P50Ms { get; init; }
    public double? P90Ms { get; init; }
    public double? P95Ms { get; init; }
    public double? P99Ms { get; init; }

    public double? VirtualUsers { get; init; }

    public Dictionary<int, long>? StatusCodes { get; set; }
}
