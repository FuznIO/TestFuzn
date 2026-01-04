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

    public Task WriteStats(string testRunId, string groupName, string testName, ScenarioLoadResult scenarioResult)
    {
        var key = GetKey(groupName, testName, scenarioResult.ScenarioName);

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

    public static IReadOnlyList<ScenarioLoadResult> GetSnapshots(string groupName, string testName, string scenarioName)
    {
        lock (_lock)
        {
            if (_snapshotsPerScenario.TryGetValue(GetKey(groupName, testName, scenarioName), out var snapshots))
                return snapshots.GetSnapshots();

            return new List<ScenarioLoadResult>();
        }
    }

    public static void RemoveSnapshots(string groupName, string testName, string scenarioName)
    {
        lock (_lock)
        {
            _snapshotsPerScenario.Remove(GetKey(groupName, testName, scenarioName));
        }
    }

    public Task CleanupSuite()
    {
        return Task.CompletedTask;
    }

    private static string GetKey(string groupName, string testName, string scenarioName)
    {
        return $"{groupName}_{testName}_{scenarioName}";
    }
}
