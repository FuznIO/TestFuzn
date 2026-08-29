namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One series for <see cref="ChartWidget"/>: its values oldest first (newest last, the same
/// order as the sparkline series on <see cref="LiveMetricsSnapshot"/>), an optional markup
/// style and whether it draws as an area or a line. The values are read once per render and
/// never held; a value that is not finite (NaN, ±Infinity) is a gap. The series of one chart
/// share its x-axis by their newest sample: a series with fewer samples than the longest one
/// is drawn as if it had gaps before its first sample, so it occupies only the newest columns.
/// The style is markup tag words (e.g. "green" or "bold #ff8800") applied to the series'
/// glyphs and, for the first series, to its newest-value annotation; without one the series
/// renders unstyled in every color mode. A <c>default(ChartSeries)</c> has null values and
/// renders as an empty series; the constructor never accepts null.
/// </summary>
internal readonly struct ChartSeries
{
    /// <summary>The samples, oldest first and newest last; null only on a default instance, which is an empty series.</summary>
    public IReadOnlyList<double>? Values { get; }

    /// <summary>Markup tag words for the series' glyphs, or null for unstyled.</summary>
    public string? Style { get; init; }

    /// <summary>Area (filled to the bottom) unless set to <see cref="ChartSeriesKind.Line"/>.</summary>
    public ChartSeriesKind Kind { get; init; }

    public ChartSeries(IReadOnlyList<double> values)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values), "Values cannot be null.");

        Values = values;
    }
}
