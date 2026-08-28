namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One simulation in a <see cref="SimulationPlan"/>: its compact display label, whether it
/// belongs to the warmup segment, and its planned wall-clock duration — null for count-based
/// simulations (OneTimeLoad, count-based FixedConcurrentLoad), whose duration cannot be known
/// up front.
/// </summary>
internal readonly struct SimulationPlanEntry
{
    /// <summary>Compact display label, e.g. "Gradual Load 10→100 rps".</summary>
    public string Label { get; }

    /// <summary>The configured duration, or null when the simulation is count-based (indeterminate).</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Whether the simulation belongs to the warmup segment.</summary>
    public bool IsWarmup { get; }

    public SimulationPlanEntry(string label, TimeSpan? duration, bool isWarmup)
    {
        if (label == null)
            throw new ArgumentNullException(nameof(label), "Label cannot be null.");

        Label = label;
        Duration = duration;
        IsWarmup = isWarmup;
    }
}
