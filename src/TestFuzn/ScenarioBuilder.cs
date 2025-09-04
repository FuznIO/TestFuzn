using FuznLabs.TestFuzn.Internals;
using FuznLabs.TestFuzn.Contracts.Adapters;

namespace FuznLabs.TestFuzn;

public class ScenarioBuilder<TCustomStepContext>
    where TCustomStepContext : new()
{
    private ITestFrameworkAdapter _testFramework;
    private readonly IFeatureTest _featureTest;
    internal Scenario Scenario;
    internal List<Func<Scenario>> IncludeScenarios;

    public ScenarioBuilder(ITestFrameworkAdapter testFramework, 
        IFeatureTest featureTest, 
        string name)
    {
        _testFramework = testFramework;
        _featureTest = featureTest;
        Scenario = new Scenario(name);
        Scenario.ContextType = typeof(TCustomStepContext);
    }

    public ScenarioBuilder<TCustomStepContext> Init(Action<Context> action)
    {
        Scenario.Init = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TCustomStepContext> Init(Func<Context, Task> action)
    {
        Scenario.Init = (context) => {
            return action(context);
        };
        return this;
    }

    public ScenarioBuilder<TCustomStepContext> InputData(params object[] inputData)
    {
        Scenario.InputDataInfo.AddParams(inputData);

        return this;
    }

    public ScenarioBuilder<TCustomStepContext> InputDataFromList(Func<Context, Task<List<object>>> action)
    {
        Scenario.InputDataInfo.AddAction(context =>
        {
            return action(context);
        });

        return this;
    }

    public ScenarioBuilder<TCustomStepContext> InputDataFromList(Func<Context, List<object>> action)
    {
        Scenario.InputDataInfo.AddAction((context) =>
        {
            return Task.FromResult(action(context));
        });

        return this;
    }

    public ScenarioBuilder<TCustomStepContext> InputDataBehavior(InputDataBehavior inputDataBehavior)
    {
        Scenario.InputDataInfo.InputDataBehavior = inputDataBehavior;
        return this;
    }

    public ScenarioBuilder<TCustomStepContext> Step(string name, Func<StepContext<TCustomStepContext>, Task> action)
    {
        EnsureStepNameIsUnique(name);

        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        var step = new Step<StepContext<TCustomStepContext>>();
        step.Name = name;
        step.Action = context => action((StepContext<TCustomStepContext>) context);
        Scenario.Steps.Add(step);

        return this;
    }

    public ScenarioBuilder<TCustomStepContext> Step(string name, Action<StepContext<TCustomStepContext>> action)
    {
        EnsureStepNameIsUnique(name);

        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        var step = new Step<TCustomStepContext>();
        step.Name = name;
        step.Action = context =>
        {
            action((StepContext<TCustomStepContext>) context);
            return Task.CompletedTask;
        };
        Scenario.Steps.Add(step);

        return this;
    }

    public ScenarioBuilder<TCustomStepContext> Step(Step<TCustomStepContext> step)
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

    public LoadBuilder<TCustomStepContext> Load()
    {
        return new LoadBuilder<TCustomStepContext>(this);
    }

    public ScenarioBuilder<TCustomStepContext> CleanupAfterEachIteration(Action<StepContext<TCustomStepContext>> action)
    {
        Scenario.CleanupAfterEachIterationAction = (context) => {
            action((StepContext<TCustomStepContext>) context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TCustomStepContext> CleanupAfterEachIteration(Func<StepContext<TCustomStepContext>, Task> action)
    {
        Scenario.CleanupAfterEachIterationAction = (context) => {
            return action((StepContext<TCustomStepContext>) context);
        };
        return this;
    }

    public ScenarioBuilder<TCustomStepContext> CleanupAfterScenario(Action<Context> action)
    {
        Scenario.CleanupAfterScenarioAction = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TCustomStepContext> CleanupAfterScenario(Func<Context, Task> action)
    {
        Scenario.CleanupAfterScenarioAction = (context) => {
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
