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
    internal List<ILoadConfiguration> SimulationsInternal { get; } = new List<ILoadConfiguration>();
    internal Action<AssertScenarioStats>? AssertWhileRunningAction;
    internal Action<AssertScenarioStats>? AssertWhenDoneAction;

    public Scenario()
    {

    }
    public Scenario(string name)
    {
        Name = name;
        SimulationsInternal = new List<ILoadConfiguration>();
    }
}
