namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The optional parts of a <see cref="ChartWidget"/> render — the y-axis and its label format,
/// the newest-value annotation, gridlines and the sample window. <see cref="Default"/> is what
/// a render without options gets: an axis in the dim style with "0.#" labels, the annotation,
/// no gridlines and an unlimited window. Immutable once constructed, so an instance can be
/// shared across renders and threads.
/// </summary>
internal sealed class ChartOptions
{
    /// <summary>The options a render without any gets.</summary>
    public static readonly ChartOptions Default = new ChartOptions();

    /// <summary>
    /// Formats the y-axis labels and the newest-value annotation, so a layout can pass its own
    /// count or duration format. Null formats with "0.#" (culture-invariant). The output is
    /// user text to the widget: escaped, sanitized and measured, never parsed as markup. A
    /// formatter that throws is the caller's bug and propagates.
    /// </summary>
    public Func<double, string>? ValueFormatter { get; init; }

    /// <summary>
    /// Whether the left y-axis (max / mid / min labels with their tick column) is drawn. Even
    /// when set the axis needs a height of at least 2 rows and is dropped when the body would
    /// be narrower than <see cref="ChartWidget.MinimumBodyWidth"/>.
    /// </summary>
    public bool ShowAxis { get; init; } = true;

    /// <summary>
    /// Whether the first series' newest value is annotated at the right edge (<c>▶ 80</c>).
    /// Dropped before the axis when the width is short, and never drawn when the first series
    /// is empty or its newest sample is not finite.
    /// </summary>
    public bool ShowNewestValue { get; init; } = true;

    /// <summary>Whether dotted gridlines (┈) run through the empty cells of the label rows.</summary>
    public bool ShowGridlines { get; init; }

    /// <summary>
    /// How many of the newest samples of every series are visible at most, or null (and any
    /// value below 1) for no limit beyond what the width can hold. The window is measured in
    /// samples — for the dashboard's 1 Hz series that is seconds.
    /// </summary>
    public int? TimeWindow { get; init; }

    /// <summary>
    /// Markup tag words for the axis labels, ticks and gridlines; "dim" unless set, null for
    /// unstyled.
    /// </summary>
    public string? AxisStyle { get; init; } = "dim";
}
