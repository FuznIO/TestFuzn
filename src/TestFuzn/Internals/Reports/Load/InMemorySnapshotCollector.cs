using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Reports.Load;

internal class InMemorySnapshotCollector
{
    private readonly object _lock = new object();
    private readonly Dictionary<string, EvenlySpreadSnapshots> _snapshotsPerScenario = new();

    public Task WriteStats(ScenarioLoadResult scenarioResult)
    {
        var key = GetKey(scenarioResult.ScenarioName);

        lock (_lock)
        {
            if ( !_snapshotsPerScenario.TryGetValue(key, out var snapshots))
            {
                snapshots = new EvenlySpreadSnapshots(100);
                _snapshotsPerScenario[key] = snapshots;
            }

            snapshots.AddSnapshot(scenarioResult);
            return Task.CompletedTask;
        }
    }

    public IReadOnlyList<ScenarioLoadResult> GetSnapshots(string scenarioName)
    {
        lock (_lock)
        {
            if (_snapshotsPerScenario.TryGetValue(GetKey(scenarioName), out var snapshots))
                return snapshots.GetSnapshots();

            return new List<ScenarioLoadResult>();
        }
    }

    private static string GetKey(string scenarioName)
    {
        return $"{scenarioName}";
    }
}
