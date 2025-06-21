namespace TestFusion.Internals.Execution.Producers.Simulations;

internal class FixedLoadConfiguration : ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public int Rate { get; }
    public TimeSpan Interval { get; }
    public TimeSpan Duration { get; }
    
    public FixedLoadConfiguration(int rate, TimeSpan interval, TimeSpan duration)
    {
        Rate = rate;
        Interval = interval;
        Duration = duration;
    }

    public string GetDescription() => $"Fixed Load - Rate: {Rate}, Interval: {Interval:g}, Duration: {Duration:g}";
}
