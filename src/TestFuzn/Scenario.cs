using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.InputData;

namespace Fuzn.TestFuzn;

internal class Scenario
{
    public string Name { get; internal set; }
    public string Id { get; internal set; }
    public string Description { get; internal set; }
    internal TestType TestType
    {
        get
        {
            if (SimulationsAction == null)
                return TestType.Feature;
            return TestType.Load;
        }
    }
    internal List<string> Environments { get; set; }
    internal List<string> TagsInternal { get; set; }
    internal Dictionary<string, string> MetadataInternal { get; set; }
    internal Type ContextType { get; set; }
    internal Func<ScenarioContext, Task> BeforeScenario { get; set; }
    internal Func<IterationContext, Task> BeforeIterationAction { get; set; }
    internal Func<IterationContext, Task> AfterIterationAction { get; set; }
    internal Func<ScenarioContext, Task> AfterScenarioAction { get; set; }
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
