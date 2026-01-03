using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Reports.Load;

internal class InMemorySnapshotCollectorSinkPlugin : ISinkPlugin
{
    private static readonly object _lock = new object();
    private static readonly Dictionary<string, EvenlySpreadSnapshots> _snapshotsPerScenario = new();

    public Task InitSuite()
    {
        return Task.CompletedTask;
    }

    public Task WriteStats(string testRunId, string featureName, ScenarioLoadResult scenarioResult)
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

    public Task CleanupSuite()
    {
        return Task.CompletedTask;
    }

    private static string GetKey(string featureName, string scenarioName)
    {
        return $"{featureName}_{scenarioName}";
    }
}
