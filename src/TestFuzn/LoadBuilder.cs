using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn;

public class LoadBuilder<TModel>
    where TModel : new()
{
    private readonly ScenarioBuilder<TModel> _scenarioBuilder;

    public LoadBuilder(ScenarioBuilder<TModel> scenarioBuilder)
    {
        _scenarioBuilder = scenarioBuilder;
    }

    public ScenarioBuilder<TModel> Warmup(Action<Context, SimulationsBuilder> action)
    {
        _scenarioBuilder.Scenario.WarmupAction = (context, simulations) =>
            {
                action(context, simulations);
                return Task.CompletedTask;
            };
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TModel> Warmup(Func<Context, SimulationsBuilder, Task> action)
    {
        _scenarioBuilder.Scenario.WarmupAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TModel> Simulations(Action<Context, SimulationsBuilder> action)
    {
        _scenarioBuilder.Scenario.SimulationsAction = (context, simulations) =>
            {
                action(context, simulations);
                return Task.CompletedTask;
            };
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TModel> Simulations(Func<Context, SimulationsBuilder, Task> action)
    {
        _scenarioBuilder.Scenario.SimulationsAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TModel> IncludeScenario<TIncludeScenarioModel>(ScenarioBuilder<TIncludeScenarioModel> scenarioBuilder)
        where TIncludeScenarioModel : new()
    {
        if (_scenarioBuilder.IncludeScenarios == null)
            _scenarioBuilder.IncludeScenarios = new();

        _scenarioBuilder.IncludeScenarios.Add(() => scenarioBuilder.Scenario);
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TModel> AssertWhileRunning(Action<Context, AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhileRunningAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TModel> AssertWhenDone(Action<Context, AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhenDoneAction = action;
        return _scenarioBuilder;
    }
}
