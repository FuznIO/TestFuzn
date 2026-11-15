using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class FixedLoadConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public int Rate { get; }
    public TimeSpan Interval { get; }
    public TimeSpan Duration { get; }
    
    public FixedLoadConfiguration(int rate, TimeSpan interval, TimeSpan duration)
    {
        SimulationGuard.NotNegative(rate, nameof(rate));
        SimulationGuard.Positive(interval, nameof(interval));
        SimulationGuard.Positive(duration, nameof(duration));
        Rate = rate;
        Interval = interval;
        Duration = duration;
    }

    public string GetDescription() => $"Fixed Load - Rate: {Rate}, Interval: {Interval:g}, Duration: {Duration:g} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";

    public ILoadConfiguration Split(int workerIndex, int workerCount, int seed)
    {
        return new FixedLoadConfiguration(LoadSplit.Part(Rate, workerIndex, workerCount, IsWarmup), Interval, Duration) { IsWarmup = IsWarmup };
    }
}
