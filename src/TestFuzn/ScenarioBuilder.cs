using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn;

public class ScenarioBuilder<TStepContext>
    where TStepContext : IterationContext
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
        Scenario.ContextType = typeof(TStepContext);
    }

    public ScenarioBuilder<TStepContext> Id(string id)
    {
        Scenario.Id = id;
        return this;
    }

    public ScenarioBuilder<TStepContext> Metadata(string key, string value)
    {
        if (Scenario.MetadataInternal == null)
            Scenario.MetadataInternal = new();
        if (Scenario.MetadataInternal.ContainsKey(key))
            throw new ArgumentException($"Meta key '{key}' already exists in the scenario. Meta keys must be unique.", nameof(key));
        Scenario.MetadataInternal.Add(key, value);
        return this;
    }

    public ScenarioBuilder<TStepContext> InitScenario(Action<Context> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        Scenario.InitScenario = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> InitScenario(Func<Context, Task> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        Scenario.InitScenario = (context) => {
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

    public ScenarioBuilder<TStepContext> InitIteration(Func<TStepContext, Task> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");
        Scenario.InitIterationAction = (context) => {
            return action((TStepContext) context);
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> InitIteration(Action<TStepContext> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");
        Scenario.InitIterationAction = (context) => {
            action((TStepContext) context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> Step(string name, string id, Func<TStepContext, Task> action)
    {
        EnsureStepNameIsUnique(name);

        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        var step = new Step();
        step.ContextType = typeof(TStepContext);
        step.Name = name;
        step.Id = id;
        step.Action = context => action((TStepContext) context);
        Scenario.Steps.Add(step);

        return this;
    }

    public ScenarioBuilder<TStepContext> Step(string name, string id, Action<TStepContext> action)
    {
        Step(name, id, context =>
        {
            action((TStepContext) context);
            return Task.CompletedTask;
        });

        return this;
    }

    public ScenarioBuilder<TStepContext> Step(string name, Func<TStepContext, Task> action)
    {
        Step(name, null, action);
        return this;
    }

    public ScenarioBuilder<TStepContext> Step(string name, Action<TStepContext> action)
    {
        Step(name, null, action);
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

    public ScenarioBuilder<TStepContext> CleanupIteration(Action<TStepContext> action)
    {
        Scenario.CleanupIterationAction = (context) => {
            action((TStepContext) context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> CleanupIteration(Func<TStepContext, Task> action)
    {
        Scenario.CleanupIterationAction = (context) => {
            return action((TStepContext) context);
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> CleanupScenario(Action<Context> action)
    {
        Scenario.CleanupScenarioAction = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TStepContext> CleanupScenario(Func<Context, Task> action)
    {
        Scenario.CleanupScenarioAction = (context) => {
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
