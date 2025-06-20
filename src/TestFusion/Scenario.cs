using TestFusion.Internals.InputData;

namespace TestFusion;

public class Scenario
{
    public string Name { get; internal set; }
    public Guid ScenarioId { get; internal set; } = Guid.NewGuid();

    internal Type ContextType { get; set; }
    internal Func<Context, Task> Init { get; set; }
    internal Func<StepContext, Task> CleanupAfterEachIteration { get; set; }
    internal Func<Context, Task> CleanupAfterScenario { get; set; }
    internal InputDataInfo InputDataInfo { get; private set; } = new InputDataInfo();
    internal List<BaseStep> Steps { get; } = new List<BaseStep>();
    internal Func<Context, SimulationsBuilder, Task> Simulations;
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
