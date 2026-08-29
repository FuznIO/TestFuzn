namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// How <see cref="ChartWidget"/> draws a <see cref="ChartSeries"/>: filled from the bottom of
/// the chart up to each value, or as the value line alone.
/// </summary>
internal enum ChartSeriesKind
{
    /// <summary>
    /// Filled from the bottom of the chart up to each sample's value — the sparkline look
    /// carried over several rows.
    /// </summary>
    Area,

    /// <summary>
    /// The value line alone: each sample's dot, with consecutive samples joined so the line
    /// stays connected across a jump (braille only — the block glyphs have no thin line, so
    /// there a Line shows each sample's partial block in the row its value lands on).
    /// </summary>
    Line
}
