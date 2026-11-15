using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class GradualLoadIncreaseConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public int StartRate { get; }
    public int EndRate { get; }
    public TimeSpan Duration { get; }

    public GradualLoadIncreaseConfiguration(int startRate, int endRate, TimeSpan duration)
    {
        SimulationGuard.NotNegative(startRate, nameof(startRate));
        SimulationGuard.NotNegative(endRate, nameof(endRate));
        SimulationGuard.Positive(duration, nameof(duration));
        StartRate = startRate;
        EndRate = endRate;
        Duration = duration;
    }

    public string GetDescription() => $"Gradual Load - Start rate: {StartRate}, End rate: {EndRate}, Duration: {Duration:g} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";

    public ILoadConfiguration Split(int workerIndex, int workerCount, int seed)
    {
        return new GradualLoadIncreaseConfiguration(
            LoadSplit.Part(StartRate, workerIndex, workerCount, IsWarmup),
            LoadSplit.Part(EndRate, workerIndex, workerCount, IsWarmup),
            Duration) { IsWarmup = IsWarmup };
    }
}
