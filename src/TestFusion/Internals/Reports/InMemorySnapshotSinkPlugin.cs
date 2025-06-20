using TestFusion.Internals.Results.Load;
using TestFusion.Contracts.Sinks;
using TestFusion.Results.Load;

namespace TestFusion.Internals.Reports;

internal class InMemorySnapshotCollectorSinkPlugin : ISinkPlugin
{
    private static readonly object _lock = new object();
    private static readonly Dictionary<string, EvenlySpreadSnapshots> _snapshotsPerScenario = new();

    public Task InitGlobal()
    {
        return Task.CompletedTask;
    }

    public Task WriteMetrics(string testRunId, string scenarioName, ScenarioLoadResult scenarioResult)
    {
        lock (_lock)
        {
            if ( !_snapshotsPerScenario.TryGetValue(scenarioName, out var snapshots))
            {
                snapshots = new EvenlySpreadSnapshots(100);
                _snapshotsPerScenario[scenarioName] = snapshots;
            }

            snapshots.AddSnapshot(scenarioResult);
            return Task.CompletedTask;
        }
    }

    public static IReadOnlyList<ScenarioLoadResult> GetSnapshots(string scenarioName)
    {
        lock (_lock)
        {
            if (_snapshotsPerScenario.TryGetValue(scenarioName, out var snapshots))
                return snapshots.GetSnapshots();

            return new List<ScenarioLoadResult>();
        }
    }

    public static void RemoveSnapshots(string name)
    {
        lock (_lock)
        { 
            _snapshotsPerScenario.Remove(name);
        }
    }

    public Task CleanupGlobal()
    {
        return Task.CompletedTask;
    }
}
