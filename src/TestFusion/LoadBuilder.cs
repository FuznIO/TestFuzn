using TestFusion.Internals.Producers.Simulations;

namespace TestFusion;

public class LoadBuilder<TStepContext>
    where TStepContext : StepContext, new()
{
    private readonly ScenarioBuilder<TStepContext> _scenarioBuilder;

    public LoadBuilder(ScenarioBuilder<TStepContext> scenarioBuilder)
    {
        _scenarioBuilder = scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> OneTimeLoad(int count)
    {
        _scenarioBuilder.Scenario.SimulationsInternal.Add(new OneTimeLoadConfiguration(count));
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> GradualLoadIncrease(int startRate, int endRate, TimeSpan duration)
    {
        _scenarioBuilder.Scenario.SimulationsInternal.Add(new GradualLoadIncreaseConfiguration(startRate, endRate, duration));
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> FixedLoad(int rate, TimeSpan duration)
    {
        return FixedLoad(rate, duration, duration);
    }

    public ScenarioBuilder<TStepContext> FixedLoad(int rate, TimeSpan interval, TimeSpan duration)
    {
        _scenarioBuilder.Scenario.SimulationsInternal.Add(new FixedLoadConfiguration(rate, interval, duration));
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> FixedConcurrentLoad(int count, TimeSpan duration)
    {
        _scenarioBuilder.Scenario.SimulationsInternal.Add(new FixedConcurrentLoadConfiguration(count, duration));
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> RandomLoadPerSecond(int minRate, int maxRate, TimeSpan duration)
    {
        _scenarioBuilder.Scenario.SimulationsInternal.Add(new RandomLoadPerSecondConfiguration(minRate, maxRate, duration));
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> Pause(TimeSpan duration)
    {
        _scenarioBuilder.Scenario.SimulationsInternal.Add(new PauseLoadConfiguration(duration));
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

    public ScenarioBuilder<TStepContext> AssertWhileRunning(Action<AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhileRunningAction = action;
        return _scenarioBuilder;
    }

    public ScenarioBuilder<TStepContext> AssertWhenDone(Action<AssertScenarioStats> action)
    {
        _scenarioBuilder.Scenario.AssertWhenDoneAction = action;
        return _scenarioBuilder;
    }
}
