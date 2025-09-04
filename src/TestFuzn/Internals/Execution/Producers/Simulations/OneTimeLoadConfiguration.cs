namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class OneTimeLoadConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public int Count { get; private set; }

    public OneTimeLoadConfiguration(int count)
    {
        Count = count;
    }

    public string GetDescription() => $"One Time Load - Count: {Count} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";
}
