namespace Fuzn.TestFuzn.Internals.Logging;

/// <summary>
/// Lightweight scope state for scenario/step context, avoiding dictionary allocations in the hot path.
/// </summary>
internal readonly struct LoggingScopeState
{
    public string Scenario { get; }
    public int Step { get; }

    public LoggingScopeState(string scenario, int step)
    {
        Scenario = scenario;
        Step = step;
    }
}
