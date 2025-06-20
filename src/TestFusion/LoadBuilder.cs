namespace TestFusion;

public class LoadBuilder<TStepContext>
    where TStepContext : StepContext, new()
{
    private readonly ScenarioBuilder<TStepContext> _scenarioBuilder;

    public LoadBuilder(ScenarioBuilder<TStepContext> scenarioBuilder)
    {
        _scenarioBuilder = scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> Simulations(Action<Context, SimulationsBuilder> action)
    {
        _scenarioBuilder.Scenario.Simulations = (context, simulations) =>
            {
                action(context, simulations);
                return Task.CompletedTask;
            };
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> Simulations(Func<Context, SimulationsBuilder, Task> action)
    {
        _scenarioBuilder.Scenario.Simulations = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> IncludeScenario<TIncludeScenarioStepContext>(ScenarioBuilder<TIncludeScenarioStepContext> scenarioBuilder)
        where TIncludeScenarioStepContext : StepContext, new()
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
