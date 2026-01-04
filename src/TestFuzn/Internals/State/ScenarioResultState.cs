using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Internals.State;

internal class ScenarioResultState
{
    public Dictionary<string, ScenarioLoadCollector> LoadCollectors = new();
    public Dictionary<string, ScenarioStandardResult> StandardCollectors = new();
}
