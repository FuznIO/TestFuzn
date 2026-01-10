using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

/// <summary>
/// Builds and configures test scenarios with a specific model type for sharing data across steps.
/// </summary>
/// <typeparam name="TModel">The model type used to share data across steps within an iteration.</typeparam>
public class ScenarioBuilder<TModel>
    where TModel : new()
{
    private ITestFrameworkAdapter _testFramework;
    private readonly ITest _test;
    internal Scenario Scenario;
    internal List<Func<Scenario>> IncludeScenarios;
    private Action<AssertInternalState> _assertInternalState;

    internal ScenarioBuilder(object testFramework, 
        ITest test, 
        string name)
    {
        if (testFramework is not ITestFrameworkAdapter adapter)
            throw new ArgumentException("Invalid test framework adapter, must implement ITestFrameworkAdapter.", nameof(testFramework));

        GlobalState.EnsureInitialized(adapter);

        _testFramework = adapter;
        _test = test;
        Scenario = new Scenario(name);
        Scenario.ContextType = typeof(IterationContext<TModel>);
    }

    /// <summary>
    /// Sets a unique identifier for the scenario. Only needed for Load tests. Standard tests inherits the test Id.
    /// Typically used for reporting and tracking to keep the history of scenario executions consistent across scenario name renames.
    /// </summary>
    /// <param name="id">The unique identifier for the scenario.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> Id(string id)
    {
        Scenario.Id = id;
        return this;
    }

    /// <summary>
    /// Registers an action to execute before the scenario starts.
    /// </summary>
    /// <param name="action">The synchronous action to execute before the scenario.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> BeforeScenario(Action<Context> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        Scenario.BeforeScenario = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    /// <summary>
    /// Registers an asynchronous action to execute before the scenario starts.
    /// </summary>
    /// <param name="action">The asynchronous action to execute before the scenario.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> BeforeScenario(Func<Context, Task> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");

        Scenario.BeforeScenario = (context) => {
            return action(context);
        };
        return this;
    }

    /// <summary>
    /// Provides static input data for the scenario iterations.
    /// </summary>
    /// <param name="inputData">The input data objects to use for iterations.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> InputData(params object[] inputData)
    {
        Scenario.InputDataInfo.AddParams(inputData);

        return this;
    }

    /// <summary>
    /// Provides input data for the scenario from an asynchronous function.
    /// </summary>
    /// <param name="action">An asynchronous function that returns a list of input data objects.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> InputDataFromList(Func<Context, Task<List<object>>> action)
    {
        Scenario.InputDataInfo.AddAction(context =>
        {
            return action(context);
        });

        return this;
    }

    /// <summary>
    /// Provides input data for the scenario from a synchronous function.
    /// </summary>
    /// <param name="action">A synchronous function that returns a list of input data objects.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> InputDataFromList(Func<Context, List<object>> action)
    {
        Scenario.InputDataInfo.AddAction((context) =>
        {
            return Task.FromResult(action(context));
        });

        return this;
    }

    /// <summary>
    /// Sets the behavior for how input data is consumed across iterations.
    /// </summary>
    /// <param name="inputDataBehavior">The behavior to use when consuming input data.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> InputDataBehavior(InputDataBehavior inputDataBehavior)
    {
        Scenario.InputDataInfo.InputDataBehavior = inputDataBehavior;
        return this;
    }

    /// <summary>
    /// Registers an asynchronous action to execute before each iteration.
    /// </summary>
    /// <param name="action">The asynchronous action to execute before each iteration.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> BeforeIteration(Func<IterationContext<TModel>, Task> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");
        Scenario.BeforeIterationAction = (context) => {
            return action((IterationContext<TModel>) context);
        };
        return this;
    }

    /// <summary>
    /// Registers a synchronous action to execute before each iteration.
    /// </summary>
    /// <param name="action">The synchronous action to execute before each iteration.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> BeforeIteration(Action<IterationContext<TModel>> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action), "Action cannot be null.");
        Scenario.BeforeIterationAction = (context) => {
            action((IterationContext<TModel>) context);
            return Task.CompletedTask;
        };
        return this;
    }

    /// <summary>
    /// Adds an asynchronous step to the scenario with a name and identifier.
    /// </summary>
    /// <param name="name">The name of the step.</param>
    /// <param name="id">The unique identifier for the step.</param>
    /// <param name="action">The asynchronous action to execute for the step.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
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

    /// <summary>
    /// Adds a synchronous step to the scenario with a name and identifier.
    /// </summary>
    /// <param name="name">The name of the step.</param>
    /// <param name="id">The unique identifier for the step.</param>
    /// <param name="action">The synchronous action to execute for the step.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> Step(string name, string id, Action<IterationContext<TModel>> action)
    {
        Step(name, id, context =>
        {
            action((IterationContext<TModel>) context);
            return Task.CompletedTask;
        });

        return this;
    }

    /// <summary>
    /// Adds an asynchronous step to the scenario with a name.
    /// </summary>
    /// <param name="name">The display name of the step.</param>
    /// <param name="action">The asynchronous action to execute for the step.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> Step(string name, Func<IterationContext<TModel>, Task> action)
    {
        Step(name, null, action);
        return this;
    }

    /// <summary>
    /// Adds a synchronous step to the scenario with a name.
    /// </summary>
    /// <param name="name">The display name of the step.</param>
    /// <param name="action">The synchronous action to execute for the step.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
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

    /// <summary>
    /// Configures the scenario for load testing.
    /// </summary>
    /// <returns>A <see cref="LoadBuilder{TModel}"/> instance for configuring load test parameters.</returns>
    public LoadBuilder<TModel> Load()
    {
        return new LoadBuilder<TModel>(this);
    }

    /// <summary>
    /// Registers a synchronous action to execute after each iteration.
    /// </summary>
    /// <param name="action">The synchronous action to execute after each iteration.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> AfterIteration(Action<IterationContext<TModel>> action)
    {
        Scenario.AfterIterationAction = (context) => {
            action((IterationContext<TModel>) context);
            return Task.CompletedTask;
        };
        return this;
    }

    /// <summary>
    /// Registers an asynchronous action to execute after each iteration.
    /// </summary>
    /// <param name="action">The asynchronous action to execute after each iteration.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> AfterIteration(Func<IterationContext<TModel>, Task> action)
    {
        Scenario.AfterIterationAction = (context) => {
            return action((IterationContext<TModel>) context);
        };
        return this;
    }

    /// <summary>
    /// Registers a synchronous action to execute after the scenario completes.
    /// </summary>
    /// <param name="action">The synchronous action to execute after the scenario.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> AfterScenario(Action<Context> action)
    {
        Scenario.AfterScenarioAction = (context) => {
            action(context);
            return Task.CompletedTask;
        };
        return this;
    }

    /// <summary>
    /// Registers an asynchronous action to execute after the scenario completes.
    /// </summary>
    /// <param name="action">The asynchronous action to execute after the scenario.</param>
    /// <returns>The current <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> AfterScenario(Func<Context, Task> action)
    {
        Scenario.AfterScenarioAction = (context) => {
            return action(context);
        };
        return this;
    }

    internal ScenarioBuilder<TModel> AssertInternalState(Action<AssertInternalState> action)
    {
        _assertInternalState = action;

        return this;
    }

    /// <summary>
    /// Executes the configured scenario. 
    /// For load tests, use Load().IncludeScenario() to execute multiple scenarios in a single test, in parallel.
    /// </summary>
    /// <returns>A task representing the asynchronous scenario execution.</returns>
    public async Task Run()
    {
        var scenarios = new List<Scenario>();
        scenarios.Add(Scenario);
        if (IncludeScenarios != null)
            scenarios.AddRange(IncludeScenarios.Select(scenario => scenario()));

        InheritValuesFromTest(scenarios);

        await new TestRunner(_testFramework, _test, _assertInternalState).Run(scenarios.ToArray());
    }

    private void InheritValuesFromTest(List<Scenario> scenarios)
    {
        var firstScenario = scenarios.First();
        if (string.IsNullOrEmpty(firstScenario.Id))
            firstScenario.Id = _test.TestInfo.Id;
        if (string.IsNullOrEmpty(firstScenario.Name))
            firstScenario.Name = _test.TestInfo.Name;
        if (string.IsNullOrEmpty(firstScenario.Description))
            firstScenario.Description = _test.TestInfo.Description;

        foreach (var scenario in scenarios)
        {
            if (string.IsNullOrEmpty(scenario.Name))
                throw new Exception("Scenario name cannot be null or empty.");
        }
    }
}
