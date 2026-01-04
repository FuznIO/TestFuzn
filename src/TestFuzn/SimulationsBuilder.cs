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
    /// Configures a one-time load that executes a fixed number of iterations.
    /// </summary>
    /// <param name="count">The total number of iterations to execute.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder OneTimeLoad(int count)
    {
        AddSimulation(new OneTimeLoadConfiguration(count));
        return this;
    }

    /// <summary>
    /// Configures a gradual load increase from a starting rate to an ending rate over a duration.
    /// </summary>
    /// <param name="startRate">The starting number of requests per second.</param>
    /// <param name="endRate">The ending number of requests per second.</param>
    /// <param name="duration">The duration over which to increase the load.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder GradualLoadIncrease(int startRate, int endRate, TimeSpan duration)
    {
        AddSimulation(new GradualLoadIncreaseConfiguration(startRate, endRate, duration));
        return this;
    }

    /// <summary>
    /// Configures a fixed load that maintains a constant rate for a duration.
    /// </summary>
    /// <param name="rate">The number of requests per second to maintain.</param>
    /// <param name="duration">The duration to maintain the load.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder FixedLoad(int rate, TimeSpan duration)
    {
        return FixedLoad(rate, duration, duration);
    }

    /// <summary>
    /// Configures a fixed load that maintains a constant rate with a custom interval.
    /// </summary>
    /// <param name="rate">The number of requests per interval to maintain.</param>
    /// <param name="interval">The interval at which to inject requests.</param>
    /// <param name="duration">The total duration to maintain the load.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder FixedLoad(int rate, TimeSpan interval, TimeSpan duration)
    {
        AddSimulation(new FixedLoadConfiguration(rate, interval, duration));
        return this;
    }

    /// <summary>
    /// Configures a fixed concurrent load that maintains a constant number of concurrent users.
    /// </summary>
    /// <param name="count">The number of concurrent users to maintain.</param>
    /// <param name="duration">The duration to maintain the concurrent load.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder FixedConcurrentLoad(int count, TimeSpan duration)
    {
        AddSimulation(new FixedConcurrentLoadConfiguration(count, duration));
        return this;
    }

    /// <summary>
    /// Configures a random load that varies the request rate between minimum and maximum values.
    /// </summary>
    /// <param name="minRate">The minimum number of requests per second.</param>
    /// <param name="maxRate">The maximum number of requests per second.</param>
    /// <param name="duration">The duration to run the random load.</param>
    /// <returns>The current <see cref="SimulationsBuilder"/> instance for method chaining.</returns>
    public SimulationsBuilder RandomLoadPerSecond(int minRate, int maxRate, TimeSpan duration)
    {
        AddSimulation(new RandomLoadPerSecondConfiguration(minRate, maxRate, duration));
        return this;
    }

    /// <summary>
    /// Configures a pause in the load test for a specified duration.
    /// </summary>
    /// <param name="duration">The duration to pause.</param>
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
