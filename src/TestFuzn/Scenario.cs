using FuznLabs.TestFuzn.Internals.InputData;

namespace FuznLabs.TestFuzn;

public class Scenario
{
    public string Name { get; internal set; }
    public Guid ScenarioId { get; internal set; } = Guid.NewGuid();

    internal Type ContextType { get; set; }
    internal Func<Context, Task> Init { get; set; }
    internal Func<StepContext, Task> CleanupAfterEachIterationAction { get; set; }
    internal Func<Context, Task> CleanupAfterScenarioAction { get; set; }
    internal InputDataInfo InputDataInfo { get; private set; } = new InputDataInfo();
    internal List<BaseStep> Steps { get; } = new List<BaseStep>();
    internal Func<Context, SimulationsBuilder, Task> WarmupAction;
    internal Func<Context, SimulationsBuilder, Task> SimulationsAction;
    internal List<ILoadConfiguration> SimulationsInternal { get; } = new List<ILoadConfiguration>();
    internal Action<Context, AssertScenarioStats>? AssertWhileRunningAction;
    internal Action<Context, AssertScenarioStats>? AssertWhenDoneAction;

    public Scenario()
    {

    }
    public Scenario(string name)
    {
        Name = name;
        SimulationsInternal = new List<ILoadConfiguration>();
    }
}
