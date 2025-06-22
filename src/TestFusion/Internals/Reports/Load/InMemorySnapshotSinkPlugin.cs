using TestFusion.Internals.Results.Load;
using TestFusion.Contracts.Sinks;
using TestFusion.Contracts.Results.Load;

namespace TestFusion.Internals.Reports.Load;

internal class InMemorySnapshotCollectorSinkPlugin : ISinkPlugin
{
    private static readonly object _lock = new object();
    private static readonly Dictionary<string, EvenlySpreadSnapshots> _snapshotsPerScenario = new();

    public Task InitGlobal()
    {
        return Task.CompletedTask;
    }

    public Task WriteMetrics(string testRunId, string featureName, ScenarioLoadResult scenarioResult)
    {
        var key = GetKey(featureName, scenarioResult.ScenarioName);

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

    public static IReadOnlyList<ScenarioLoadResult> GetSnapshots(string featureName, string scenarioName)
    {
        lock (_lock)
        {
            if (_snapshotsPerScenario.TryGetValue(GetKey(featureName, scenarioName), out var snapshots))
                return snapshots.GetSnapshots();

            return new List<ScenarioLoadResult>();
        }
    }

    public static void RemoveSnapshots(string featureName, string scenarioName)
    {
        lock (_lock)
        { 
            _snapshotsPerScenario.Remove(GetKey(featureName, scenarioName));
        }
    }

    public Task CleanupGlobal()
    {
        return Task.CompletedTask;
    }

    private static string GetKey(string featureName, string scenarioName)
    {
        return $"{featureName}_{scenarioName}";
    }
}
