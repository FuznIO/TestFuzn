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

        _scenarioBuilder.IncludeScenarios.Add(() => scenarioBuilder.Scenario);
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
