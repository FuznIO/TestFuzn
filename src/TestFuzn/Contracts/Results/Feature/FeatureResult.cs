using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Contracts.Results.Feature;

public class FeatureResult(string name, string id, Dictionary<string, string> metadata)
{
    public string Name { get; set; } = name;
    public string Id { get; set; } = id;
    public Dictionary<string, string> Metadata { get; set; } = metadata;

    public ConcurrentDictionary<string, ScenarioFeatureResult> ScenarioResults { get; } = new();

    public bool Passed()
    {
        if (ScenarioResults.Any(x => x.Value.Status == ScenarioStatus.Failed))
            return false;
        return true;
    }
}