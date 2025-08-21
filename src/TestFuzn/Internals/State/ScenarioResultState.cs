using FuznLabs.TestFuzn.Contracts.Results.Feature;
using FuznLabs.TestFuzn.Internals.Results.Load;

namespace FuznLabs.TestFuzn.Internals.State;

internal class ScenarioResultState
{
    public Dictionary<string, ScenarioLoadCollector> LoadCollectors = new();
    public Dictionary<string, ScenarioFeatureResult> FeatureCollectors = new();
}
