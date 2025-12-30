using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Contracts.Results.Feature;

internal class GroupResult(string name, string id, Dictionary<string, string> metadata)
{
    public string Name { get; set; } = name;
    public string Id { get; set; } = id;
    public Dictionary<string, string> Metadata { get; set; } = metadata;

    public ConcurrentDictionary<string, ScenarioFeatureResult> TestResults { get; } = new();

    public ScenarioStatus Status
    {
        get
        {
            if (TestResults.Values.Any(x => x.Status == ScenarioStatus.Failed))
                return ScenarioStatus.Failed;

            if (TestResults.Values.All(s => s.Status == ScenarioStatus.Skipped))
                return ScenarioStatus.Skipped;

            return ScenarioStatus.Passed;
        }
    }
}