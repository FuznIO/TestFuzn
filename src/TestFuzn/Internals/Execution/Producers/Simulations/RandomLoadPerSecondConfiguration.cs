namespace FuznLabs.TestFuzn.Internals.Execution.Producers.Simulations;

internal class RandomLoadPerSecondConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public int MinRate { get; }
    public int MaxRate { get; }
    public TimeSpan Duration { get; }

    public RandomLoadPerSecondConfiguration(int minRate, int maxRate, TimeSpan duration)
    {
        MinRate = minRate;
        MaxRate = maxRate;
        Duration = duration;
    }

    public string GetDescription() => $"Random Load pr Second - Min rate: {MinRate}, Max rate: {MaxRate}, Duration: {Duration:g} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";
}
