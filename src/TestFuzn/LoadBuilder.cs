using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn;

/// <summary>
/// Builds and configures load testing parameters for a scenario.
/// </summary>
/// <typeparam name="TModel">The model type used to share data across steps within an iteration.</typeparam>
public class LoadBuilder<TModel>
    where TModel : new()
{
    private readonly ScenarioBuilder<TModel> _scenarioBuilder;

    internal LoadBuilder(ScenarioBuilder<TModel> scenarioBuilder)
    {
        _scenarioBuilder = scenarioBuilder;
    }

    /// <summary>
    /// Configures a warmup phase for the load test using a synchronous action.
    /// For these simulations no stats will be recorded, AssertWhileRunning, AssertWhenDone and sinks will not be called.
    /// </summary>
    /// <param name="action">The synchronous action that configures warmup simulations.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> Warmup(Action<Context, SimulationsBuilder> action)
    {
        _scenarioBuilder.Scenario.WarmupAction = (context, simulations) =>
            {
                action(context, simulations);
                return Task.CompletedTask;
            };
        return _scenarioBuilder;
    }

    /// <summary>
    /// Configures a warmup phase for the load test using an asynchronous action.
    /// For these simulations no stats will be recorded, AssertWhileRunning, AssertWhenDone and sinks will not be called.
    /// </summary>
    /// <param name="action">The asynchronous action that configures warmup simulations.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> Warmup(Func<Context, SimulationsBuilder, Task> action)
    {
        _scenarioBuilder.Scenario.WarmupAction = action;
        return _scenarioBuilder;
    }

    /// <summary>
    /// Configures the load simulations for the test using a synchronous action.
    /// </summary>
    /// <param name="action">The synchronous action that configures load simulations.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> Simulations(Action<Context, SimulationsBuilder> action)
    {
        _scenarioBuilder.Scenario.SimulationsAction = (context, simulations) =>
            {
                action(context, simulations);
                return Task.CompletedTask;
            };
        return _scenarioBuilder;
    }

    /// <summary>
    /// Configures the load simulations for the test using an asynchronous action.
    /// </summary>
    /// <param name="action">The asynchronous action that configures load simulations.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> Simulations(Func<Context, SimulationsBuilder, Task> action)
    {
        _scenarioBuilder.Scenario.SimulationsAction = action;
        return _scenarioBuilder;
    }

    /// <summary>
    /// Includes another scenario in the load test execution.
    /// </summary>
    /// <typeparam name="TIncludeScenarioModel">The model type of the included scenario.</typeparam>
    /// <param name="scenarioBuilder">The scenario builder to include.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> IncludeScenario<TIncludeScenarioModel>(ScenarioBuilder<TIncludeScenarioModel> scenarioBuilder)
        where TIncludeScenarioModel : new()
    {
        if (_scenarioBuilder.IncludeScenarios == null)
            _scenarioBuilder.IncludeScenarios = new();

        scenarioBuilder.Scenario.IsUsed = true;
        _scenarioBuilder.IncludeScenarios.Add(() => scenarioBuilder.Scenario);
        return _scenarioBuilder;
    }

    /// <summary>
    /// Registers an action that runs once before the load starts, also when the load runs on several workers
    /// (it then runs on the controller). Use it to create shared test data; read the returned data with
    /// <see cref="Context.OnceData{T}"/>. The data must be JSON-serializable.
    /// </summary>
    /// <typeparam name="TData">The type of the data the action returns.</typeparam>
    /// <param name="action">The asynchronous action that creates the data.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> BeforeScenarioOnce<TData>(Func<Context, Task<TData>> action)
    {
        if (action == null)
            throw _scenarioBuilder.BuildFailed(new ArgumentNullException(nameof(action), "Action cannot be null."));

        _scenarioBuilder.Scenario.BeforeScenarioOnceAction = async context => await action(context);
        return _scenarioBuilder;
    }

    /// <summary>
    /// Registers an action that runs once before the load starts, also when the load runs on several workers
    /// (it then runs on the controller). Read the returned data with <see cref="Context.OnceData{T}"/>.
    /// The data must be JSON-serializable.
    /// </summary>
    /// <typeparam name="TData">The type of the data the action returns.</typeparam>
    /// <param name="action">The synchronous action that creates the data.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> BeforeScenarioOnce<TData>(Func<Context, TData> action)
    {
        if (action == null)
            throw _scenarioBuilder.BuildFailed(new ArgumentNullException(nameof(action), "Action cannot be null."));

        _scenarioBuilder.Scenario.BeforeScenarioOnceAction = context => Task.FromResult<object?>(action(context));
        return _scenarioBuilder;
    }

    /// <summary>
    /// Registers an action that runs once after the load has ended, also after a stop, so data created in
    /// BeforeScenarioOnce is cleaned up. When the load runs on several workers it runs on the controller.
    /// </summary>
    /// <param name="action">The asynchronous cleanup action.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> AfterScenarioOnce(Func<Context, Task> action)
    {
        if (action == null)
            throw _scenarioBuilder.BuildFailed(new ArgumentNullException(nameof(action), "Action cannot be null."));

        _scenarioBuilder.Scenario.AfterScenarioOnceAction = context => action(context);
        return _scenarioBuilder;
    }

    /// <summary>
    /// Registers an action that runs once after the load has ended, also after a stop, so data created in
    /// BeforeScenarioOnce is cleaned up. When the load runs on several workers it runs on the controller.
    /// </summary>
    /// <param name="action">The synchronous cleanup action.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> AfterScenarioOnce(Action<Context> action)
    {
        if (action == null)
            throw _scenarioBuilder.BuildFailed(new ArgumentNullException(nameof(action), "Action cannot be null."));

        _scenarioBuilder.Scenario.AfterScenarioOnceAction = context =>
        {
            action(context);
            return Task.CompletedTask;
        };
        return _scenarioBuilder;
    }

    /// <summary>
    /// Registers an assertion action to run after each warmup iteration.
    /// If an assertion throws an exception, the load test will be stopped and marked as failed.
    /// </summary>
    /// <param name="action">The action that performs assertions on warmup statistics.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> AssertWhileWarmingUp(Action<Context, WarmupStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhileWarmingUpAction = action;
        return _scenarioBuilder;
    }

    /// <summary>
    /// Registers an assertion action to run periodically while the load test is running.
    /// If an assertion fails, the load test will be stopped and marked as failed.
    /// </summary>
    /// <param name="action">The action that performs assertions on scenario statistics.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> AssertWhileRunning(Action<Context, AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhileRunningAction = action;
        return _scenarioBuilder;
    }

    /// <summary>
    /// Registers an assertion action to run after the load test completes.
    /// If an assertion fails, the load test will be marked as failed.
    /// </summary>
    /// <param name="action">The action that performs assertions on final scenario statistics.</param>
    /// <returns>The parent <see cref="ScenarioBuilder{TModel}"/> instance for method chaining.</returns>
    public ScenarioBuilder<TModel> AssertWhenDone(Action<Context, AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhenDoneAction = action;
        return _scenarioBuilder;
    }
}
