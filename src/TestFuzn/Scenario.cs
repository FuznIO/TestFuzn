using FuznLabs.TestFuzn.Internals.InputData;

namespace FuznLabs.TestFuzn;

public class Scenario
{
    public string Name { get; internal set; }
    public Guid ScenarioId { get; internal set; } = Guid.NewGuid();

    internal Type ContextType { get; set; }
    internal Func<Context, Task> Init { get; set; }
    internal Func<BaseStepContext, Task> CleanupAfterEachIterationAction { get; set; }
    internal Func<Context, Task> CleanupAfterScenarioAction { get; set; }
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
