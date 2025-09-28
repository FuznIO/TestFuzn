using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn;

public class ScenarioBuilder<TModel>
    where TModel : new()
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
        Scenario.ContextType = typeof(IterationContext<TModel>);
    }

    public ScenarioBuilder<TModel> Id(string id)
    {
        Scenario.Id = id;
        return this;
    }

    public ScenarioBuilder<TModel> Metadata(string key, string value)
    {
        if (Scenario.MetadataInternal == null)
            Scenario.MetadataInternal = new();
        if (Scenario.MetadataInternal.ContainsKey(key))
            throw new ArgumentException($"Meta key '{key}' already exists in the scenario. Meta keys must be unique.", nameof(key));
        Scenario.MetadataInternal.Add(key, value);
        return this;
    }

    public ScenarioBuilder<TModel> InitScenario(Action<Context> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        Scenario.InitScenario = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TModel> InitScenario(Func<Context, Task> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        Scenario.InitScenario = (context) => {
            return action(context);
        };
        return this;
    }

    public ScenarioBuilder<TModel> InputData(params object[] inputData)
    {
        Scenario.InputDataInfo.AddParams(inputData);

        return this;
    }

    public ScenarioBuilder<TModel> InputDataFromList(Func<Context, Task<List<object>>> action)
    {
        Scenario.InputDataInfo.AddAction(context =>
        {
            return action(context);
        });

        return this;
    }

    public ScenarioBuilder<TModel> InputDataFromList(Func<Context, List<object>> action)
    {
        Scenario.InputDataInfo.AddAction((context) =>
        {
            return Task.FromResult(action(context));
        });

        return this;
    }

    public ScenarioBuilder<TModel> InputDataBehavior(InputDataBehavior inputDataBehavior)
    {
        Scenario.InputDataInfo.InputDataBehavior = inputDataBehavior;
        return this;
    }

    public ScenarioBuilder<TModel> InitIteration(Func<IterationContext<TModel>, Task> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");
        Scenario.InitIterationAction = (context) => {
            return action((IterationContext<TModel>) context);
        };
        return this;
    }

    public ScenarioBuilder<TModel> InitIteration(Action<IterationContext<TModel>> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");
        Scenario.InitIterationAction = (context) => {
            action((IterationContext<TModel>) context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TModel> Step(string name, string id, Func<IterationContext<TModel>, Task> action)
    {
        EnsureStepNameIsUnique(name);

        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        var step = new Step();
        step.ContextType = typeof(TModel);
        step.Name = name;
        step.Id = id;
        step.Action = context => action((IterationContext<TModel>) context);
        Scenario.Steps.Add(step);

        return this;
    }

    public ScenarioBuilder<TModel> Step(string name, string id, Action<IterationContext<TModel>> action)
    {
        Step(name, id, context =>
        {
            action((IterationContext<TModel>) context);
            return Task.CompletedTask;
        });

        return this;
    }

    public ScenarioBuilder<TModel> Step(string name, Func<IterationContext<TModel>, Task> action)
    {
        Step(name, null, action);
        return this;
    }

    public ScenarioBuilder<TModel> Step(string name, Action<IterationContext<TModel>> action)
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

    public LoadBuilder<TModel> Load()
    {
        return new LoadBuilder<TModel>(this);
    }

    public ScenarioBuilder<TModel> CleanupIteration(Action<IterationContext<TModel>> action)
    {
        Scenario.CleanupIterationAction = (context) => {
            action((IterationContext<TModel>) context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TModel> CleanupIteration(Func<IterationContext<TModel>, Task> action)
    {
        Scenario.CleanupIterationAction = (context) => {
            return action((IterationContext<TModel>) context);
        };
        return this;
    }

    public ScenarioBuilder<TModel> CleanupScenario(Action<Context> action)
    {
        Scenario.CleanupScenarioAction = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    public ScenarioBuilder<TModel> CleanupScenario(Func<Context, Task> action)
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
