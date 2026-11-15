using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class OneTimeLoadConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public int Count { get; private set; }

    public OneTimeLoadConfiguration(int count)
    {
        SimulationGuard.NotNegative(count, nameof(count));
        Count = count;
    }

    public string GetDescription() => $"One Time Load - Count: {Count} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";

    public ILoadConfiguration Split(int workerIndex, int workerCount, int seed)
    {
        return new OneTimeLoadConfiguration(LoadSplit.Part(Count, workerIndex, workerCount, IsWarmup)) { IsWarmup = IsWarmup };
    }
}
