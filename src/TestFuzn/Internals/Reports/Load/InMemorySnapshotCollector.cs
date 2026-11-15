using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Reports.Load;

internal class InMemorySnapshotCollector
{
    private const int MaxSnapshots = 100;

    private readonly object _lock = new object();
    private readonly Dictionary<string, EvenlySpreadSnapshots> _snapshotsPerScenario = new();

    public Task WriteStats(ScenarioLoadResult scenarioResult, StatsState ok)
    {
        lock (_lock)
        {
            if (!_snapshotsPerScenario.TryGetValue(scenarioResult.ScenarioName, out var snapshots))
            {
                snapshots = new EvenlySpreadSnapshots(MaxSnapshots);
                _snapshotsPerScenario[scenarioResult.ScenarioName] = snapshots;
            }

            snapshots.AddSnapshot(new LoadSnapshot(DateTime.UtcNow, scenarioResult, ok));
            return Task.CompletedTask;
        }
    }

    public IReadOnlyList<LoadSnapshot> GetSnapshots(string scenarioName)
    {
        lock (_lock)
        {
            if (_snapshotsPerScenario.TryGetValue(scenarioName, out var snapshots))
                return snapshots.GetSnapshots();

            return new List<LoadSnapshot>();
        }
    }
}
