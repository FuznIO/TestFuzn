namespace TestFusion.Internals.Producers.Simulations;

internal class OneTimeLoadConfiguration : ILoadConfiguration
{
    public int Count { get; private set; }

    public OneTimeLoadConfiguration(int count)
    {
        Count = count;
    }

    public string GetDescription() => $"One Time Load - Count: {Count}";
}
