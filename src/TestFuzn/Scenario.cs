using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals.InputData;

namespace Fuzn.TestFuzn;

public class Scenario
{
    public string Name { get; internal set; }
    public string Id { get; internal set; }
    internal TestType TestType
    {
        get
        {
            if (SimulationsAction == null)
                return TestType.Feature;
            return TestType.Load;
        }
    }
    internal ScenarioRunMode RunMode { get; set; } = ScenarioRunMode.Execute;
    internal List<string> Environments { get; set; }
    internal List<string> TagsInternal { get; set; }
    internal Dictionary<string, string> MetadataInternal { get; set; }
    internal Type ContextType { get; set; }
    internal Func<ScenarioContext, Task> InitScenario { get; set; }
    internal Func<IterationContext, Task> InitIterationAction { get; set; }
    internal Func<IterationContext, Task> CleanupIterationAction { get; set; }
    internal Func<ScenarioContext, Task> CleanupScenarioAction { get; set; }
    internal InputDataInfo InputDataInfo { get; private set; } = new();
    internal List<Step> Steps { get; } = new();
    internal Func<ScenarioContext, SimulationsBuilder, Task> WarmupAction;
    internal Func<ScenarioContext, SimulationsBuilder, Task> SimulationsAction;
    internal List<ILoadConfiguration> SimulationsInternal { get; } = new();
    internal Action<ScenarioContext, AssertScenarioStats>? AssertWhileRunningAction;
    internal Action<ScenarioContext, AssertScenarioStats>? AssertWhenDoneAction;

    public Scenario()
    {

    }
    public Scenario(string name)
    {
        Name = name;
        SimulationsInternal = new();
    }
}
