using TestFusion.Internals.Producers.Simulations;

namespace TestFusion;

public class SimulationsBuilder
{
    private readonly Scenario _scenario;

    public SimulationsBuilder(Scenario scenario)
    {
        _scenario = scenario;
    }

    public SimulationsBuilder OneTimeLoad(int count)
    {
        _scenario.SimulationsInternal.Add(new OneTimeLoadConfiguration(count));
        return this;
    }

    public SimulationsBuilder GradualLoadIncrease(int startRate, int endRate, TimeSpan duration)
    {
        _scenario.SimulationsInternal.Add(new GradualLoadIncreaseConfiguration(startRate, endRate, duration));
        return this;
    }

    public SimulationsBuilder FixedLoad(int rate, TimeSpan duration)
    {
        return FixedLoad(rate, duration, duration);
    }

    public SimulationsBuilder FixedLoad(int rate, TimeSpan interval, TimeSpan duration)
    {
        _scenario.SimulationsInternal.Add(new FixedLoadConfiguration(rate, interval, duration));
        return this;
    }

    public SimulationsBuilder FixedConcurrentLoad(int count, TimeSpan duration)
    {
        _scenario.SimulationsInternal.Add(new FixedConcurrentLoadConfiguration(count, duration));
        return this;
    }

    public SimulationsBuilder RandomLoadPerSecond(int minRate, int maxRate, TimeSpan duration)
    {
        _scenario.SimulationsInternal.Add(new RandomLoadPerSecondConfiguration(minRate, maxRate, duration));
        return this;
    }

    public SimulationsBuilder Pause(TimeSpan duration)
    {
        _scenario.SimulationsInternal.Add(new PauseLoadConfiguration(duration));
        return this;
    }
}