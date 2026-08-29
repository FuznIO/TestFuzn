namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The optional parts of a <see cref="HeatmapWidget"/> render — the label column and its style,
/// and the sample window. <see cref="Default"/> is what a render without options gets: labels in
/// the dim style and an unlimited window. Immutable once constructed, so an instance can be
/// shared across renders and threads.
/// </summary>
internal sealed class HeatmapOptions
{
    /// <summary>The options a render without any gets.</summary>
    public static readonly HeatmapOptions Default = new HeatmapOptions();

    /// <summary>
    /// Whether the left label column (the bucket edges, one per row) is drawn. Even when set it
    /// is dropped when it would leave the body narrower than
    /// <see cref="HeatmapWidget.MinimumBodyWidth"/>, and it is absent when every shown label is
    /// empty.
    /// </summary>
    public bool ShowLabels { get; init; } = true;

    /// <summary>Markup tag words for the labels; "dim" unless set, null for unstyled.</summary>
    public string? LabelStyle { get; init; } = "dim";

    /// <summary>
    /// How many of the newest samples are visible at most, or null (and any value below 1) for
    /// no limit beyond what the width can hold. The window is measured in samples — for the
    /// dashboard's 1 Hz series that is seconds — the same knob as
    /// <see cref="ChartOptions.TimeWindow"/>, so a chart and a heatmap can cover one span.
    /// </summary>
    public int? TimeWindow { get; init; }
}
