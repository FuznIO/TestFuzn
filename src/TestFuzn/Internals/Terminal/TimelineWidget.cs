using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders the plan's phase timeline as one bar of exactly <c>width</c> columns — one segment
/// per <see cref="TimelineSegment"/>, in order, filled with the logo gradient up to the run's
/// position and dim beyond it, a ▼ marker on the position and each segment's label inside it
/// when it fits — plus, only when some label did not fit, a legend line of those labels under
/// their segments. A render is thus one or two lines, every one exactly <c>width</c> columns;
/// callers reserve two rows or check the count. Every line goes through
/// <see cref="MarkupText"/>, so all color modes work and <see cref="ColorMode.None"/> output
/// has no escape bytes. No segments render one blank line and a width below 1 renders nothing;
/// the list itself cannot be null. Stateless and thread-safe: the segments are read once and
/// never held.
/// <para>
/// <b>Columns.</b> A determinate segment (one with a duration) takes a share of the columns in
/// proportion to its duration, apportioned by the largest remainder so the shares add up to the
/// width exactly — equal shares when every duration is zero, a negative duration counting as
/// zero. An indeterminate segment (a null duration: a count-based simulation, whose length time
/// cannot tell) takes <see cref="IndeterminateSegmentWidth"/> columns off the top, hatched, and
/// the determinate segments share what is left. Every segment keeps at least one column: a
/// determinate segment rounded to none takes one from the widest, and the hatched segments
/// shrink to <c>(width − determinate count) / indeterminate count</c> columns each when their
/// fixed width would leave the determinate segments fewer than one apiece. With no determinate
/// segment the hatched ones share the whole width equally. A width below the segment count
/// cannot show every segment: the first <c>width</c> segments take one column each and the
/// rest are left out entirely — bar, position and legend.
/// </para>
/// <para>
/// <b>Position.</b> <c>positionFraction</c> is the run's progress as a fraction of the total
/// determinate duration, 0..1; a value outside clamps, and null, NaN and ±Infinity are no
/// position at all — no marker, nothing filled. The elapsed determinate time it stands for is
/// walked through the determinate segments in order (a segment is current while the elapsed
/// time is below its cumulative end, so at an exact boundary the next one is current, and a
/// zero-duration segment is never current) and the marker sits on the current segment's column
/// <c>floor(share of the segment elapsed × its columns)</c>; at the end, a fraction of 1, it
/// sits on the last determinate column. Every determinate column before the marker is filled
/// and every one after it is beyond. Hatched segments are skipped by the walk and never filled:
/// the widget cannot know when a count-based simulation ends, so the marker moves across the
/// determinate columns only. With no determinate duration at all there is no marker either.
/// </para>
/// <para>
/// <b>Labels.</b> A label is drawn inside its segment, centred with a bare space on each side
/// (<see cref="LabelPadding"/>), when it fits — its width plus the padding at most the segment's
/// columns, the spare column of an odd difference going right. Its characters take the filled,
/// beyond or hatched style of the columns they sit on, so a label the position is crossing is
/// lit up to the marker, and the marker replaces whatever is on its column, a label's character
/// included. A label that does not fit goes to the legend line: written dim, starting under its
/// segment's first column, or <see cref="LegendGap"/> columns after the previous legend label
/// when that is further right; a legend wider than the width is cut with an ellipsis (the bar
/// never is). An empty label is neither drawn nor listed. Labels are user text: control-
/// sanitized (each control character is one space, \r\n one), escaped and measured, never
/// parsed as markup.
/// </para>
/// <para>
/// <b>Glyphs.</b> <see cref="ColorMode.TrueColor"/> and <see cref="ColorMode.Colors16"/> draw
/// a measurement segment as █ and a warmup segment as the lighter ▓, the filled columns in
/// colour — in TrueColor the logo gradient, <see cref="TerminalPalette.LogoGradientStart"/> at
/// the bar's left edge to <see cref="TerminalPalette.LogoGradientEnd"/> at its right,
/// interpolated per column across the whole bar so that progress reveals the gradient; in
/// Colors16 the single bright-yellow accent — and the columns beyond the position dim.
/// <see cref="ColorMode.None"/> and <see cref="ColorMode.Monochrome"/>, with no colour to tell
/// the two apart, draw <c>#</c> (measurement) and <c>=</c> (warmup) up to the position and
/// <c>-</c> beyond it, Monochrome dimming the beyond columns. A hatched segment is ▒, dim, in
/// every mode, and the marker ▼ is bold in the filled colour of its column. Label padding is
/// bare; every other column shares one styled run with its equally styled neighbours.
/// </para>
/// </summary>
internal static class TimelineWidget
{
    /// <summary>The columns a hatched (indeterminate) segment takes while the width allows it.</summary>
    public const int IndeterminateSegmentWidth = 8;

    /// <summary>The fewest columns between two labels on the legend line.</summary>
    public const int LegendGap = 2;

    /// <summary>The bare columns on each side of a label drawn inside its segment.</summary>
    public const int LabelPadding = 1;

    private const char FilledCell = '█';
    private const char WarmupCell = '▓';
    private const char HatchedCell = '▒';
    private const char MarkerGlyph = '▼';
    private const char AsciiFilledCell = '#';
    private const char AsciiWarmupCell = '=';
    private const char AsciiBeyondCell = '-';

    // The single warm accent for Colors16 mode: bright yellow, the logo's and the heatmap's.
    private const string Colors16Accent = "yellow";
    private const string MarkerDecoration = "bold";

    /// <summary>
    /// Renders the bar and, when some label did not fit inside its segment, the legend line
    /// under it — one or two lines of exactly <paramref name="width"/> columns (see the class
    /// summary).
    /// </summary>
    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<TimelineSegment> segments, double? positionFraction, int width, ColorMode colorMode)
    {
        if (segments == null)
            throw new ArgumentNullException(nameof(segments), "Segments cannot be null.");

        if (width < 1)
            return Array.Empty<RenderedLine>();

        if (segments.Count == 0)
            return new[] { new RenderedLine(new string(' ', width), width) };

        // A width below the segment count draws the first width segments, one column each.
        var drawnCount = Math.Min(segments.Count, width);
        var columns = ColumnCounts(segments, drawnCount, width);
        var starts = new int[drawnCount];
        for (var index = 1; index < drawnCount; index++)
            starts[index] = starts[index - 1] + columns[index - 1];

        var markerColumn = MarkerColumn(segments, drawnCount, columns, starts, positionFraction);

        var texts = new string[width];
        var styles = new string?[width];
        var legend = new List<LegendEntry>();
        for (var index = 0; index < drawnCount; index++)
        {
            var segment = segments[index];
            var isHatched = segment.Duration == null;
            for (var column = starts[index]; column < starts[index] + columns[index]; column++)
            {
                var isFilled = !isHatched && markerColumn >= 0 && column < markerColumn;
                texts[column] = CellGlyph(isHatched, segment.IsWarmup, isFilled, colorMode).ToString();
                styles[column] = CellStyle(isHatched, isFilled, column, width, colorMode);
            }

            var label = SanitizedLabel(segment.Label);
            if (label.Length == 0)
                continue;

            if (label.Length + (2 * LabelPadding) <= columns[index])
                PlaceLabel(texts, styles, label, starts[index], columns[index]);
            else
                legend.Add(new LegendEntry(starts[index], label));
        }

        if (markerColumn >= 0)
        {
            texts[markerColumn] = MarkerGlyph.ToString();
            styles[markerColumn] = MarkerStyle(markerColumn, width, colorMode);
        }

        var lines = new List<RenderedLine>(2);
        lines.Add(MarkupText.RenderFitted(BarMarkup(texts, styles), width, colorMode));
        if (legend.Count > 0)
            lines.Add(MarkupText.RenderFitted(LegendMarkup(legend), width, colorMode));

        return lines;
    }

    // The columns of each drawn segment, adding up to the width: one each when the width is
    // below the segment count; equal shares when no segment is determinate; else the fixed
    // hatched width per indeterminate segment (shrunk to leave the determinate segments a column
    // apiece) and the rest apportioned among the determinate segments by duration.
    private static int[] ColumnCounts(IReadOnlyList<TimelineSegment> segments, int drawnCount, int width)
    {
        var columns = new int[drawnCount];
        if (drawnCount < segments.Count)
        {
            Array.Fill(columns, 1);
            return columns;
        }

        var indeterminateCount = 0;
        var weights = new List<double>(drawnCount);
        for (var index = 0; index < drawnCount; index++)
        {
            var duration = segments[index].Duration;
            if (duration == null)
                indeterminateCount++;
            else
                weights.Add(DurationTicks(duration.Value));
        }

        if (weights.Count == 0)
        {
            var equal = new double[drawnCount];
            Array.Fill(equal, 1);
            return Apportion(equal, width);
        }

        var hatchedWidth = IndeterminateSegmentWidth;
        if (indeterminateCount > 0 && width - (indeterminateCount * hatchedWidth) < weights.Count)
            hatchedWidth = (width - weights.Count) / indeterminateCount;

        var shares = Apportion(weights.ToArray(), width - (indeterminateCount * hatchedWidth));
        EnsureOneEach(shares);

        var nextShare = 0;
        for (var index = 0; index < drawnCount; index++)
        {
            if (segments[index].Duration == null)
                columns[index] = hatchedWidth;
            else
                columns[index] = shares[nextShare++];
        }

        return columns;
    }

    // A duration's weight: its ticks, a negative one counting as zero.
    private static double DurationTicks(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            return 0;

        return duration.Ticks;
    }

    // Largest-remainder apportionment of total columns by weight: each weight's floor share,
    // then the columns left over to the largest fractional remainders, an earlier weight first
    // on a tie. Equal weights when they sum to zero.
    private static int[] Apportion(double[] weights, int total)
    {
        var sum = 0.0;
        foreach (var weight in weights)
            sum += weight;

        if (sum <= 0)
        {
            Array.Fill(weights, 1);
            sum = weights.Length;
        }

        var shares = new int[weights.Length];
        var remainders = new double[weights.Length];
        var assigned = 0;
        for (var index = 0; index < weights.Length; index++)
        {
            var exact = weights[index] / sum * total;
            shares[index] = (int)Math.Floor(exact);
            remainders[index] = exact - shares[index];
            assigned += shares[index];
        }

        var taken = new bool[weights.Length];
        for (var leftover = total - assigned; leftover > 0; leftover--)
        {
            var best = -1;
            for (var index = 0; index < weights.Length; index++)
            {
                if (!taken[index] && (best < 0 || remainders[index] > remainders[best]))
                    best = index;
            }

            if (best < 0)
            {
                Array.Fill(taken, false);
                leftover++;
                continue;
            }

            shares[best]++;
            taken[best] = true;
        }

        return shares;
    }

    // Gives every share rounded to nothing one column from the widest share (the first when
    // tied), while one has a column to spare.
    private static void EnsureOneEach(int[] shares)
    {
        for (var index = 0; index < shares.Length; index++)
        {
            if (shares[index] > 0)
                continue;

            var widest = 0;
            for (var candidate = 1; candidate < shares.Length; candidate++)
            {
                if (shares[candidate] > shares[widest])
                    widest = candidate;
            }

            if (shares[widest] <= 1)
                return;

            shares[widest]--;
            shares[index] = 1;
        }
    }

    // The marker's column for the position, or -1 for none: the elapsed determinate time walked
    // through the determinate segments (see the class summary), the last determinate column
    // past the end.
    private static int MarkerColumn(IReadOnlyList<TimelineSegment> segments, int drawnCount, int[] columns, int[] starts, double? positionFraction)
    {
        if (positionFraction == null || !double.IsFinite(positionFraction.Value))
            return -1;

        var fraction = positionFraction.Value;
        if (fraction < 0)
            fraction = 0;
        else if (fraction > 1)
            fraction = 1;

        var total = 0.0;
        for (var index = 0; index < drawnCount; index++)
        {
            var duration = segments[index].Duration;
            if (duration != null)
                total += DurationTicks(duration.Value);
        }

        if (total <= 0)
            return -1;

        var elapsed = fraction * total;
        var cumulativeEnd = 0.0;
        var lastDeterminateColumn = -1;
        for (var index = 0; index < drawnCount; index++)
        {
            var duration = segments[index].Duration;
            if (duration == null)
                continue;

            var ticks = DurationTicks(duration.Value);
            var cumulativeStart = cumulativeEnd;
            cumulativeEnd += ticks;
            lastDeterminateColumn = starts[index] + columns[index] - 1;
            if (elapsed >= cumulativeEnd)
                continue;

            var offset = (int)Math.Floor((elapsed - cumulativeStart) / ticks * columns[index]);
            if (offset < 0)
                offset = 0;
            else if (offset >= columns[index])
                offset = columns[index] - 1;

            return starts[index] + offset;
        }

        return lastDeterminateColumn;
    }

    private static char CellGlyph(bool isHatched, bool isWarmup, bool isFilled, ColorMode colorMode)
    {
        if (isHatched)
            return HatchedCell;

        if (colorMode == ColorMode.TrueColor || colorMode == ColorMode.Colors16)
            return isWarmup ? WarmupCell : FilledCell;

        if (!isFilled)
            return AsciiBeyondCell;

        return isWarmup ? AsciiWarmupCell : AsciiFilledCell;
    }

    // Filled columns carry the mode's accent — the gradient at the column in TrueColor, yellow
    // in Colors16, nothing without colour — and hatched or beyond columns are dim.
    private static string? CellStyle(bool isHatched, bool isFilled, int column, int width, ColorMode colorMode)
    {
        if (isHatched || !isFilled)
            return TerminalPalette.SecondaryStyle;

        return AccentStyle(column, width, colorMode);
    }

    private static string MarkerStyle(int column, int width, ColorMode colorMode)
    {
        var accent = AccentStyle(column, width, colorMode);
        if (accent == null)
            return MarkerDecoration;

        return MarkerDecoration + " " + accent;
    }

    private static string? AccentStyle(int column, int width, ColorMode colorMode)
    {
        switch (colorMode)
        {
            case ColorMode.TrueColor:
                return TerminalPalette.LogoGradientStyle(GradientPosition(column, width));
            case ColorMode.Colors16:
                return Colors16Accent;
            default:
                return null;
        }
    }

    // The column's place along the gradient: the left edge at 0, the right edge at 1.
    private static double GradientPosition(int column, int width)
    {
        if (width <= 1)
            return 0;

        return (double)column / (width - 1);
    }

    // The label centred in its segment between bare padding columns, each character escaped
    // and keeping the style of the column it lands on.
    private static void PlaceLabel(string[] texts, string?[] styles, string label, int start, int columns)
    {
        var first = start + ((columns - label.Length - (2 * LabelPadding)) / 2);
        for (var padding = 0; padding < LabelPadding; padding++)
        {
            texts[first + padding] = " ";
            styles[first + padding] = null;
            texts[first + LabelPadding + label.Length + padding] = " ";
            styles[first + LabelPadding + label.Length + padding] = null;
        }

        for (var index = 0; index < label.Length; index++)
            texts[first + LabelPadding + index] = MarkupParser.Escape(label[index].ToString());
    }

    // The label as one column per character: every control character (including a \r\n pair)
    // is one space, so the width is the character count; null is empty.
    private static string SanitizedLabel(string? label)
    {
        if (label == null)
            return string.Empty;

        return MarkupText.SanitizeControlCharacters(label);
    }

    // The columns as runs of equally styled text.
    private static string BarMarkup(string[] texts, string?[] styles)
    {
        var markup = new StringBuilder();
        string? runStyle = null;
        var run = new StringBuilder();
        for (var column = 0; column < texts.Length; column++)
        {
            if (!string.Equals(styles[column], runStyle, StringComparison.Ordinal))
            {
                AppendStyled(markup, run.ToString(), runStyle);
                run.Clear();
                runStyle = styles[column];
            }

            run.Append(texts[column]);
        }

        AppendStyled(markup, run.ToString(), runStyle);
        return markup.ToString();
    }

    // The legend labels dim, each under its segment's first column or LegendGap columns after
    // the previous label when that is further right; fitting cuts an overrun with an ellipsis.
    private static string LegendMarkup(List<LegendEntry> legend)
    {
        var markup = new StringBuilder();
        var used = 0;
        for (var index = 0; index < legend.Count; index++)
        {
            var earliest = index == 0 ? 0 : used + LegendGap;
            var start = Math.Max(earliest, legend[index].Column);
            markup.Append(' ', start - used);
            AppendStyled(markup, MarkupParser.Escape(legend[index].Label), TerminalPalette.SecondaryStyle);
            used = start + legend[index].Label.Length;
        }

        return markup.ToString();
    }

    private static void AppendStyled(StringBuilder markup, string text, string? style)
    {
        if (text.Length == 0)
            return;

        if (string.IsNullOrEmpty(style))
        {
            markup.Append(text);
            return;
        }

        markup.Append('[').Append(style).Append(']').Append(text).Append("[/]");
    }

    // A label that did not fit inside its segment, with the segment's first column.
    private readonly struct LegendEntry
    {
        public int Column { get; }
        public string Label { get; }

        public LegendEntry(int column, string label)
        {
            Column = column;
            Label = label;
        }
    }
}
