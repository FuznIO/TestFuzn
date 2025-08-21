namespace FuznLabs.TestFuzn.Internals.Execution.Producers.Simulations;

internal class GradualLoadIncreaseConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public int StartRate { get; }
    public int EndRate { get; }
    public TimeSpan Duration { get; }

    public GradualLoadIncreaseConfiguration(int startRate, int endRate, TimeSpan duration)
    {
        StartRate = startRate;
        EndRate = endRate;
        Duration = duration;
    }

    public string GetDescription() => $"Gradual Load - Start rate: {StartRate}, End rate: {EndRate}, Duration: {Duration:g} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";
}
