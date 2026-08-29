namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One segment of a <see cref="TimelineWidget"/> bar — a simulation of the plan as the timeline
/// shows it: its <see cref="Label"/>, drawn inside the segment when it fits and on the legend
/// line below when it does not; its planned <see cref="Duration"/>, which sizes the segment in
/// proportion to the others, or null for a count-based simulation whose length time cannot
/// tell — an indeterminate segment, drawn hatched at a fixed width; and whether it belongs to
/// the warmup phase, which draws it in the lighter warmup texture. The layout maps a
/// <see cref="SimulationPlanEntry"/> straight onto one (label, duration, warmup), so the widget
/// never reads the live model. The label is user text to the widget: control-sanitized,
/// escaped and measured, never parsed as markup, so brackets render literally. A
/// <c>default(TimelineSegment)</c> has a null label and no duration and draws as an unlabelled
/// indeterminate segment; the constructor never accepts a null label.
/// </summary>
internal readonly struct TimelineSegment
{
    /// <summary>The segment's name, e.g. "warmup 10 rps" or "steady 80 rps"; null only on a default instance, which is unlabelled.</summary>
    public string? Label { get; }

    /// <summary>The planned duration, or null when the simulation is count-based (indeterminate).</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Whether the segment belongs to the warmup phase.</summary>
    public bool IsWarmup { get; }

    public TimelineSegment(string label, TimeSpan? duration, bool isWarmup)
    {
        if (label == null)
            throw new ArgumentNullException(nameof(label), "Label cannot be null.");

        Label = label;
        Duration = duration;
        IsWarmup = isWarmup;
    }
}
