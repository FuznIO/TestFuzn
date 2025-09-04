namespace Fuzn.TestFuzn;

public class LoadBuilder<TStepContext>
    where TStepContext : BaseStepContext
{
    private readonly ScenarioBuilder<TStepContext> _scenarioBuilder;

    public LoadBuilder(ScenarioBuilder<TStepContext> scenarioBuilder)
    {
        _scenarioBuilder = scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> Warmup(Action<Context, SimulationsBuilder> action)
    {
        _scenarioBuilder.Scenario.WarmupAction = (context, simulations) =>
            {
                action(context, simulations);
                return Task.CompletedTask;
            };
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> Warmup(Func<Context, SimulationsBuilder, Task> action)
    {
        _scenarioBuilder.Scenario.WarmupAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> Simulations(Action<Context, SimulationsBuilder> action)
    {
        _scenarioBuilder.Scenario.SimulationsAction = (context, simulations) =>
            {
                action(context, simulations);
                return Task.CompletedTask;
            };
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> Simulations(Func<Context, SimulationsBuilder, Task> action)
    {
        _scenarioBuilder.Scenario.SimulationsAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> IncludeScenario<TIncludeScenarioStepContext>(ScenarioBuilder<TIncludeScenarioStepContext> scenarioBuilder)
        where TIncludeScenarioStepContext : BaseStepContext, new()
    {
        if (_scenarioBuilder.IncludeScenarios == null)
            _scenarioBuilder.IncludeScenarios = new();

        _scenarioBuilder.IncludeScenarios.Add(() => scenarioBuilder.Scenario);
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> AssertWhileRunning(Action<Context, AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhileRunningAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> AssertWhenDone(Action<Context, AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhenDoneAction = action;
        return _scenarioBuilder;
    }
}
