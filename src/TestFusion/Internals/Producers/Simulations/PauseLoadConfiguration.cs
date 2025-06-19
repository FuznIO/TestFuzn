namespace TestFusion.Internals.Producers.Simulations;

internal class PauseLoadConfiguration : ILoadConfiguration
{
    public TimeSpan Duration { get; }

    public PauseLoadConfiguration(TimeSpan duration)
    {
        Duration = duration;
    }

    public string GetDescription() => $"Pause Load - Duration: {Duration:g}";
}
