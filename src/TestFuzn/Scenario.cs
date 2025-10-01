using Fuzn.TestFuzn.Internals.InputData;

namespace Fuzn.TestFuzn;

public class Scenario
{
    public string Name { get; internal set; }
    public string Id { get; internal set; }
    public List<string> TagsInternal { get; internal set; }
    internal Dictionary<string, string> MetadataInternal { get; set; }

    internal Type ContextType { get; set; }
    internal Func<Context, Task> InitScenario { get; set; }
    internal Func<IterationContext, Task> InitIterationAction { get; set; }
    internal Func<IterationContext, Task> CleanupIterationAction { get; set; }
    internal Func<Context, Task> CleanupScenarioAction { get; set; }
    internal InputDataInfo InputDataInfo { get; private set; } = new();
    internal List<Step> Steps { get; } = new();
    internal Func<Context, SimulationsBuilder, Task> WarmupAction;
    internal Func<Context, SimulationsBuilder, Task> SimulationsAction;
    internal List<ILoadConfiguration> SimulationsInternal { get; } = new();
    internal Action<Context, AssertScenarioStats>? AssertWhileRunningAction;
    internal Action<Context, AssertScenarioStats>? AssertWhenDoneAction;

    public Scenario()
    {

    }
    public Scenario(string name)
    {
        Name = name;
        SimulationsInternal = new();
    }
}
