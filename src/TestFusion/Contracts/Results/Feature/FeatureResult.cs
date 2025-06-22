using System.Collections.Concurrent;

namespace TestFusion.Contracts.Results.Feature;

public class FeatureResult(string name)
{
    public string Name { get; set; } = name;
    public ConcurrentDictionary<string, ScenarioFeatureResult> ScenarioResults { get; } = new();
}