using FuznLabs.TestFuzn.Internals.Execution.Producers.Simulations;

namespace FuznLabs.TestFuzn;

public class SimulationsBuilder
{
    private readonly Scenario _scenario;
    private readonly bool _isWarmup;

    public SimulationsBuilder(Scenario scenario, bool isWarmup)
    {
        _scenario = scenario;
        _isWarmup = isWarmup;
    }

    public SimulationsBuilder OneTimeLoad(int count)
    {
        AddSimulation(new OneTimeLoadConfiguration(count));
        return this;
    }

    public SimulationsBuilder GradualLoadIncrease(int startRate, int endRate, TimeSpan duration)
    {
        AddSimulation(new GradualLoadIncreaseConfiguration(startRate, endRate, duration));
        return this;
    }

    public SimulationsBuilder FixedLoad(int rate, TimeSpan duration)
    {
        return FixedLoad(rate, duration, duration);
    }

    public SimulationsBuilder FixedLoad(int rate, TimeSpan interval, TimeSpan duration)
    {
        AddSimulation(new FixedLoadConfiguration(rate, interval, duration));
        return this;
    }

    public SimulationsBuilder FixedConcurrentLoad(int count, TimeSpan duration)
    {
        AddSimulation(new FixedConcurrentLoadConfiguration(count, duration));
        return this;
    }

    public SimulationsBuilder RandomLoadPerSecond(int minRate, int maxRate, TimeSpan duration)
    {
        AddSimulation(new RandomLoadPerSecondConfiguration(minRate, maxRate, duration));
        return this;
    }

    public SimulationsBuilder Pause(TimeSpan duration)
    {
        AddSimulation(new PauseLoadConfiguration(duration));
        return this;
    }

    private void AddSimulation(ILoadConfiguration loadConfiguration)
    {   
        loadConfiguration.IsWarmup = _isWarmup;
        _scenario.SimulationsInternal.Add(loadConfiguration);   
    }
}
