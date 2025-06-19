using TestFusion.Internals;
using TestFusion.Plugins.TestFrameworkProviders;

namespace TestFusion;

public class ScenarioBuilder<TStepContext>
    where TStepContext : StepContext, new()
{
    private ITestFrameworkProvider _testFramework;
    private readonly IFeatureTest _featureTest;
    internal Scenario Scenario;
    internal List<Func<Scenario>> IncludeScenarios;

    public ScenarioBuilder(ITestFrameworkProvider testFramework, 
        IFeatureTest featureTest, 
        string name)
    {
        _testFramework = testFramework;
        _featureTest = featureTest;
        Scenario = new Scenario(name);
        Scenario.ContextType = typeof(TStepContext);
    }

    public ScenarioBuilder<TStepContext> Init(Action<Context> action)
    {
        Scenario.Init = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> Init(Func<Context, Task> action)
    {
        Scenario.Init = (context) => {
            return action(context);
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> InputData(params object[] inputData)
    {
        Scenario.InputDataInfo.AddParams(inputData);

        return this;
    }

    public ScenarioBuilder<TStepContext> InputDataFromList(Func<Context, Task<List<object>>> action)
    {
        Scenario.InputDataInfo.AddAction(context =>
        {
            return action(context);
        });

        return this;
    }

    public ScenarioBuilder<TStepContext> InputDataFromList(Func<Context, List<object>> action)
    {
        Scenario.InputDataInfo.AddAction((context) =>
        {
            return Task.FromResult(action(context));
        });

        return this;
    }

    public ScenarioBuilder<TStepContext> InputDataBehavior(InputDataBehavior inputDataBehavior)
    {
        Scenario.InputDataInfo.InputDataBehavior = inputDataBehavior;
        return this;
    }

    public ScenarioBuilder<TStepContext> Step(string name, Func<TStepContext, Task> action)
    {
        EnsureStepNameIsUnique(name);

        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        var step = new Step<TStepContext>();
        step.Name = name;
        step.Action = context => action((TStepContext) context);
        Scenario.Steps.Add(step);

        return this;
    }

    public ScenarioBuilder<TStepContext> Step(string name, Action<TStepContext> action)
    {
        EnsureStepNameIsUnique(name);

        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        var step = new Step<TStepContext>();
        step.Name = name;
        step.Action = context =>
        {
            action((TStepContext) context);
            return Task.CompletedTask;
        };
        Scenario.Steps.Add(step);

        return this;
    }

    public ScenarioBuilder<TStepContext> Step(Step<TStepContext> step)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step), "Step cannot be null.");

        EnsureStepNameIsUnique(step.Name);

        if (step.Action == null)
            throw new ArgumentException("Step action cannot be null.", nameof(step));

        Scenario.Steps.Add(step);

        return this;
    }

    private void EnsureStepNameIsUnique(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException($"Step name cannot be null or empty.");

        if (Scenario.Steps.Any(s => s.Name == name))
            throw new InvalidOperationException($"A step with the name '{name}' already exists in the scenario.");
    }

    public LoadBuilder<TStepContext> Load()
    {
        return new LoadBuilder<TStepContext>(this);
    }

    public ScenarioBuilder<TStepContext> Pause(TimeSpan timespan)
    {
        var step = new Step<TStepContext>();
        step.Name = "Pause";
        step.Action = async (context) => await Task.Delay(timespan);

        Scenario.Steps.Add(step);
        return this;
    }

    public ScenarioBuilder<TStepContext> CleanupAfterEachIteration(Action<TStepContext> action)
    {
        Scenario.CleanupAfterEachIteration = (context) => {
            action((TStepContext) context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> CleanupAfterEachIteration(Func<TStepContext, Task> action)
    {
        Scenario.CleanupAfterEachIteration = (context) => {
            return action((TStepContext) context);
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> CleanupAfterScenario(Action<Context> action)
    {
        Scenario.CleanupAfterScenario = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> CleanupAfterScenario(Func<Context, Task> action)
    {
        Scenario.CleanupAfterScenario = (context) => {
            return action(context);
        };
        return this;
    }

    public async Task Run()
    {
        var scenarios = new List<Scenario>();
        scenarios.Add(Scenario);
        if (IncludeScenarios != null)
            scenarios.AddRange(IncludeScenarios.Select(scenario => scenario()));

        await new ScenarioTestRunner(_testFramework, _featureTest).Run(scenarios.ToArray());
    }
}
