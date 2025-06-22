using TestFusion.Contracts.Results.Feature;
using TestFusion.Internals.Results.Load;

namespace TestFusion.Internals.State;

internal class ScenarioResultState
{
    public Dictionary<string, ScenarioLoadCollector> LoadCollectors = new();
    public Dictionary<string, ScenarioFeatureResult> FeatureCollectors = new();
}
