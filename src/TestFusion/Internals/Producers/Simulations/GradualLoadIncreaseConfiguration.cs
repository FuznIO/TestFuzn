namespace TestFusion.Internals.Producers.Simulations;

internal class GradualLoadIncreaseConfiguration : ILoadConfiguration
{
    public int StartRate { get; }
    public int EndRate { get; }
    public TimeSpan Duration { get; }

    public GradualLoadIncreaseConfiguration(int startRate, int endRate, TimeSpan duration)
    {
        StartRate = startRate;
        EndRate = endRate;
        Duration = duration;
    }

    public string GetDescription() => $"Gradual Load - Start rate: {StartRate}, End rate: {EndRate}, Duration: {Duration:g}";
}
