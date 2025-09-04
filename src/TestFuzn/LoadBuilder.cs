namespace FuznLabs.TestFuzn;

public class LoadBuilder<TCustomStepContext>
    where TCustomStepContext : new()
{
    private readonly ScenarioBuilder<TCustomStepContext> _scenarioBuilder;

    public LoadBuilder(ScenarioBuilder<TCustomStepContext> scenarioBuilder)
    {
        _scenarioBuilder = scenarioBuilder;
    }

    public ScenarioBuilder<TCustomStepContext> Warmup(Action<Context, SimulationsBuilder> action)
    {
        _scenarioBuilder.Scenario.WarmupAction = (context, simulations) =>
            {
                action(context, simulations);
                return Task.CompletedTask;
            };
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TCustomStepContext> Warmup(Func<Context, SimulationsBuilder, Task> action)
    {
        _scenarioBuilder.Scenario.WarmupAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TCustomStepContext> Simulations(Action<Context, SimulationsBuilder> action)
    {
        _scenarioBuilder.Scenario.SimulationsAction = (context, simulations) =>
            {
                action(context, simulations);
                return Task.CompletedTask;
            };
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TCustomStepContext> Simulations(Func<Context, SimulationsBuilder, Task> action)
    {
        _scenarioBuilder.Scenario.SimulationsAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TCustomStepContext> IncludeScenario<TIncludeScenarioCustomStepContext>(ScenarioBuilder<TIncludeScenarioCustomStepContext> scenarioBuilder)
        where TIncludeScenarioCustomStepContext : StepContext<TIncludeScenarioCustomStepContext>, new()
    {
        if (_scenarioBuilder.IncludeScenarios == null)
            _scenarioBuilder.IncludeScenarios = new();

        _scenarioBuilder.IncludeScenarios.Add(() => scenarioBuilder.Scenario);
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TCustomStepContext> AssertWhileRunning(Action<Context, AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhileRunningAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TCustomStepContext> AssertWhenDone(Action<Context, AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhenDoneAction = action;
        return _scenarioBuilder;
    }
}
