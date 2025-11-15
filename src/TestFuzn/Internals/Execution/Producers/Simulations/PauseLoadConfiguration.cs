namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal class PauseLoadConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public TimeSpan Duration { get; }

    public PauseLoadConfiguration(TimeSpan duration)
    {
        Duration = duration;
    }

    public string GetDescription() => $"Pause Load - Duration: {Duration:g} {DescriptionHelper.AddWarmupIfWarmup(IsWarmup)}";
}
