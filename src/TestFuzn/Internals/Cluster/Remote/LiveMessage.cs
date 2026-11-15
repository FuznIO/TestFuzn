using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

// Controller to attached clients on testfuzn.run.<runId>.live every tick, and the run summary stored with
// the report. The stats are the merged ones, so a client that joins late has correct totals at once.
internal class LiveMessage
{
    public string RunId { get; set; } = "";
    public DateTime ControllerUtc { get; set; }
    public string State { get; set; } = "";
    public string? Result { get; set; }
    public string? Error { get; set; }
    public List<LiveScenario> Scenarios { get; set; } = new();
    public List<WorkerSummary> Workers { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

internal class LiveScenario
{
    public ScenarioStatsState Stats { get; set; } = new();
    public ScenarioTimesState Times { get; set; } = new();
}
