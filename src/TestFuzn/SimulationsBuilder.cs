using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

namespace Fuzn.TestFuzn;

/// <summary>
/// Builds and configures load simulation patterns for load tests.
/// </summary>
public class SimulationsBuilder
{
    private readonly Scenario _scenario;
    private readonly bool _isWarmup;

    internal SimulationsBuilder(Scenario scenario, bool isWarmup)
    {
        _scenario = scenario;
        _isWarmup = isWarmup;
    }

    /// <summary>
    /// Starts a number of iterations all at once.
    /// </summary>
    /// <param name="count">The number of iterations to start. Must be 0 or more.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder OneTimeLoad(int count)
    {
        AddSimulation(new OneTimeLoadConfiguration(count));
        return this;
    }

    /// <summary>
    /// Changes the number of iterations started per second evenly from a start rate to an end rate over a duration.
    /// An end rate below the start rate ramps the load down.
    /// </summary>
    /// <param name="startRate">Iterations started per second at the start. Must be 0 or more.</param>
    /// <param name="endRate">Iterations started per second at the end. Must be 0 or more.</param>
    /// <param name="duration">How long the change takes. Must be more than 0.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder GradualLoadIncrease(int startRate, int endRate, TimeSpan duration)
    {
        AddSimulation(new GradualLoadIncreaseConfiguration(startRate, endRate, duration));
        return this;
    }

    /// <summary>
    /// Starts a fixed number of iterations per second, spread evenly, for a duration.
    /// </summary>
    /// <param name="rate">Iterations started per second. Must be 0 or more; 0 is a pause.</param>
    /// <param name="duration">How long the load runs. Must be more than 0.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder FixedLoad(int rate, TimeSpan duration)
    {
        return FixedLoad(rate, TimeSpan.FromSeconds(1), duration);
    }

    /// <summary>
    /// Starts a fixed number of iterations per interval, spread evenly, for a duration.
    /// The load stops when the duration ends, also partway through an interval.
    /// </summary>
    /// <param name="rate">Iterations started per interval. Must be 0 or more; 0 is a pause.</param>
    /// <param name="interval">The length of each interval. Must be more than 0.</param>
    /// <param name="duration">How long the load runs. Must be more than 0.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder FixedLoad(int rate, TimeSpan interval, TimeSpan duration)
    {
        AddSimulation(new FixedLoadConfiguration(rate, interval, duration));
        return this;
    }

    /// <summary>
    /// Keeps a fixed number of iterations running at the same time (virtual users) for a duration.
    /// A new iteration starts as soon as one finishes.
    /// </summary>
    /// <param name="count">The number of iterations running at the same time. Must be 0 or more.</param>
    /// <param name="duration">How long the load runs. Must be more than 0.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder FixedConcurrentLoad(int count, TimeSpan duration)
    {
        AddSimulation(new FixedConcurrentLoadConfiguration(count, duration));
        return this;
    }

    /// <summary>
    /// Starts a random number of iterations each second, between a minimum and a maximum, spread evenly over the second.
    /// </summary>
    /// <param name="minRate">The fewest iterations started in a second. Must be 0 or more and not more than <paramref name="maxRate"/>.</param>
    /// <param name="maxRate">The most iterations started in a second. Must be 0 or more.</param>
    /// <param name="duration">How long the load runs. Must be more than 0.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder RandomLoadPerSecond(int minRate, int maxRate, TimeSpan duration)
    {
        AddSimulation(new RandomLoadPerSecondConfiguration(minRate, maxRate, duration));
        return this;
    }

    /// <summary>
    /// Starts no iterations for a duration.
    /// </summary>
    /// <param name="duration">How long the pause lasts. Must be 0 or more.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
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
