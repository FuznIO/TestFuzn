using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders the inside of one <see cref="StatTile"/> — the lines <see cref="TileRowWidget"/>
/// boxes — as lines of exactly <c>width</c> columns, the tile's inner width (its box width
/// less <see cref="PanelWidget.ContentOverhead"/>). Every line goes through
/// <see cref="MarkupText"/>, so all color modes work, <see cref="ColorMode.None"/> output has
/// no escape bytes, and the tile's texts are escaped and control-sanitized. Nothing is ever
/// truncated — a value cut from the right reads as a smaller number — so a tile that cannot
/// show its label and its value whole renders nothing instead (see the optional parts below).
/// Stateless and thread-safe: the trend is read once and never held.
/// <para>
/// <b>Anatomy.</b> Line 1 is the label, dim, with the delta right-aligned on the same row when
/// there is one: ▲ (a change at zero or above) or ▼ (below), a space and its text — in
/// <see cref="TerminalPalette.OkStyle"/> when the change is in the good direction
/// (<see cref="StatTileDelta.UpIsGood"/> agrees with the sign), in <see cref="TerminalPalette.FailedStyle"/>
/// when it is not, unstyled at zero; a NaN change draws nothing. Line 2 is the value, bold and
/// in the state's colour (<see cref="TerminalPalette.StateStyle"/> — the default foreground for
/// Neutral), with the unit dim after a space. Line 3, when the tile has a gauge, is the gauge;
/// the last line, when it has a trend, is a one-row <see cref="SparklineWidget"/> over the
/// whole width in the caller's glyph set, in the state's colour or dim for Neutral (an empty
/// trend is a blank row, so a tile keeps its height while its samples arrive). A tile thus
/// renders two, three or four lines.
/// </para>
/// <para>
/// <b>Gauge.</b> <c>▕████▌···░░▏ 500 ms</c>: the left cap ▕, a bar of
/// <c>width − 2 − (limit text width + 1)</c> cells, the right cap ▏ and, after a space, the
/// limit text. The bar spans 0 to the reading a full bar stands for — the limit for a
/// maximum, <c>Limit / WarningFraction</c> for a minimum with a band to size by (see
/// <see cref="StatTileGauge"/>) — and the fill is the current reading's share of that,
/// clamped to 0..1: full cells █ and one partial cell of eighths (▎▍▌▋▊▉ for two to seven,
/// rounded half away from zero; eight eighths become a full cell and a remainder below two
/// eighths draws nothing, since the one-eighth glyph is the right cap's and would read as a
/// doubled cap) in the state's colour; the rest of the track is dim — · outside the warning
/// band, ░ inside it. The band runs from cell <c>round(WarningFraction × bar width)</c>, kept
/// to at least the last cell while the fraction is between 0 and 1, to the end of the bar; a
/// fraction at 1 or above (or NaN) draws no band and one at 0 or below shades the whole track.
/// For a maximum a reading past the limit fills the bar and replaces its last cell with ▶, the
/// overshoot marker, while a reading at the limit exactly is a full bar without one; for a
/// minimum a reading past the top of the band is simply a full bar — the band is the
/// evaluator's warning zone above the limit, so a fill ending below it is a breach. A limit
/// that is not finite or not positive, or a NaN reading, gives no scale: the track renders
/// empty. The caps and the track share one dim run, and an empty bar is a single dim span.
/// </para>
/// <para>
/// <b>Optional parts.</b> Each optional part is shown when it fits at this width and left out
/// when it does not, independently of the others — the parts are independent signals, not a
/// hierarchy, so a gauge one column short never costs the tile its delta or its trend. The
/// trend needs <see cref="MinimumTrendWidth"/> columns, the delta needs the label, a gap of
/// <see cref="DeltaGap"/> and its own text on one row, and the gauge needs its caps,
/// <see cref="MinimumGaugeBarWidth"/> bar cells and its limit text. The tile itself needs
/// <see cref="MeasureMinimumWidth"/> columns — the wider of the label and the value line —
/// and below that, or below width 1, renders nothing.
/// </para>
/// </summary>
internal static class StatTileWidget
{
    /// <summary>The fewest sparkline columns worth drawing for the trend.</summary>
    public const int MinimumTrendWidth = 4;

    /// <summary>The fewest bar cells worth drawing for the gauge.</summary>
    public const int MinimumGaugeBarWidth = 4;

    /// <summary>The fewest columns between the label and the delta on the first line.</summary>
    public const int DeltaGap = 2;

    private const string UpArrow = "▲";
    private const string DownArrow = "▼";

    private const char LeftCap = '▕';
    private const char RightCap = '▏';
    private const char FilledCell = '█';
    private const char OvershootMarker = '▶';
    private const char EmptyCell = '·';
    private const char WarningCell = '░';

    // Two to seven eighths of a cell, for the partial cell at the end of the fill; one eighth
    // (▏) is the right cap's glyph and is never drawn as a fill.
    private const string PartialCells = "▎▍▌▋▊▉";
    private const int MinimumPartialEighths = 2;
    private const int EighthsPerCell = 8;

    // The two caps around the bar.
    private const int GaugeCapCount = 2;

    private const string ValueStyle = "bold";

    /// <summary>
    /// The narrowest width the tile renders at with every optional part left out: the wider of
    /// its label and its value line (value, a space and the unit). <see cref="TileRowWidget"/>
    /// decides with it how many tiles fit a row.
    /// </summary>
    public static int MeasureMinimumWidth(StatTile tile)
    {
        if (tile == null)
            throw new ArgumentNullException(nameof(tile), "Tile cannot be null.");

        return new TileContent(tile).MinimumWidth;
    }

    public static IReadOnlyList<RenderedLine> Render(StatTile tile, int width, ColorMode colorMode, SparklineGlyphSet glyphSet = SparklineGlyphSet.Braille)
    {
        if (tile == null)
            throw new ArgumentNullException(nameof(tile), "Tile cannot be null.");

        if (width < 1)
            return Array.Empty<RenderedLine>();

        var content = new TileContent(tile);
        if (content.MinimumWidth > width)
            return Array.Empty<RenderedLine>();

        var trend = tile.Trend;
        var gauge = tile.Gauge;

        // Each optional part is shown exactly when it fits, independently of the others.
        var showTrend = trend != null && width >= MinimumTrendWidth;
        var showDelta = content.HasDelta && content.LabelWidth + DeltaGap + content.DeltaWidth <= width;
        var showGauge = gauge != null && content.GaugeOverhead + MinimumGaugeBarWidth <= width;

        var lines = new List<RenderedLine>(4);
        lines.Add(RenderLabelLine(content, width, showDelta, colorMode));
        lines.Add(MarkupText.RenderFitted(content.ValueMarkup, width, colorMode));

        if (showGauge && gauge != null)
            lines.Add(RenderGaugeLine(content, gauge.Value, width, colorMode));

        if (showTrend && trend != null)
            lines.Add(SparklineWidget.Render(trend, width, colorMode, glyphSet, content.TrendStyle)[0]);

        return lines;
    }

    // The label at the left and, when shown, the delta at the right edge; the gap between them
    // is whatever the width leaves, at least DeltaGap by the fit check.
    private static RenderedLine RenderLabelLine(TileContent content, int width, bool showDelta, ColorMode colorMode)
    {
        var markup = Styled(TerminalPalette.SecondaryStyle, content.Label);
        if (showDelta)
            markup += new string(' ', width - content.LabelWidth - content.DeltaWidth) + content.DeltaMarkup;

        return MarkupText.RenderFitted(markup, width, colorMode);
    }

    private static RenderedLine RenderGaugeLine(TileContent content, StatTileGauge gauge, int width, ColorMode colorMode)
    {
        var barWidth = width - content.GaugeOverhead;
        var fill = FillGlyphs(gauge, barWidth);

        var track = new StringBuilder();
        var warningStart = WarningStart(gauge.WarningFraction, barWidth);
        for (var cell = fill.Length; cell < barWidth; cell++)
            track.Append(cell >= warningStart ? WarningCell : EmptyCell);

        track.Append(RightCap);
        if (content.LimitWidth > 0)
            track.Append(' ').Append(content.LimitText);

        string markup;
        if (fill.Length == 0)
            markup = Styled(TerminalPalette.SecondaryStyle, LeftCap + track.ToString());
        else
            markup = Styled(TerminalPalette.SecondaryStyle, LeftCap.ToString()) + Styled(content.StateStyle, fill) + Styled(TerminalPalette.SecondaryStyle, track.ToString());

        return MarkupText.RenderFitted(markup, width, colorMode);
    }

    // The filled cells of the bar, one glyph per cell: empty without a scale, for a maximum the
    // whole bar ending in the overshoot marker past the limit, else the full cells and the
    // partial one.
    private static string FillGlyphs(StatTileGauge gauge, int barWidth)
    {
        if (!double.IsFinite(gauge.Limit) || gauge.Limit <= 0 || double.IsNaN(gauge.Current))
            return string.Empty;

        var isMinimum = gauge.Comparison == ThresholdComparison.GreaterThanOrEqualTo;
        if (!isMinimum && gauge.Current > gauge.Limit)
            return new string(FilledCell, barWidth - 1) + OvershootMarker;

        var fraction = gauge.Current / BarScale(gauge, isMinimum);
        if (fraction < 0)
            fraction = 0;
        else if (fraction > 1)
            fraction = 1;

        var cells = fraction * barWidth;
        var fullCount = (int)Math.Floor(cells);
        var eighths = (int)Math.Round((cells - fullCount) * EighthsPerCell, MidpointRounding.AwayFromZero);
        if (eighths == EighthsPerCell)
        {
            fullCount++;
            eighths = 0;
        }

        if (fullCount >= barWidth)
            return new string(FilledCell, barWidth);

        if (eighths < MinimumPartialEighths)
            return new string(FilledCell, fullCount);

        return new string(FilledCell, fullCount) + PartialCells[eighths - MinimumPartialEighths];
    }

    // The reading a full bar stands for: the limit, or for a minimum with a band to size by,
    // the top of the evaluator's warning zone, Limit / WarningFraction.
    private static double BarScale(StatTileGauge gauge, bool isMinimum)
    {
        if (isMinimum && gauge.WarningFraction > 0 && gauge.WarningFraction < 1)
            return gauge.Limit / gauge.WarningFraction;

        return gauge.Limit;
    }

    // The first cell of the warning band: past the bar for no band, 0 for the whole track,
    // else the rounded position kept to at least the last cell.
    private static int WarningStart(double warningFraction, int barWidth)
    {
        if (double.IsNaN(warningFraction) || warningFraction >= 1)
            return barWidth;

        if (warningFraction <= 0)
            return 0;

        var start = (int)Math.Round(warningFraction * barWidth, MidpointRounding.AwayFromZero);
        return Math.Min(start, barWidth - 1);
    }

    private static string Styled(string? style, string text)
    {
        if (text.Length == 0)
            return string.Empty;

        if (string.IsNullOrEmpty(style))
            return text;

        return "[" + style + "]" + text + "[/]";
    }

    private static string TextOf(string? text)
    {
        if (text == null)
            return string.Empty;

        return text;
    }

    // The tile's texts escaped and measured once, with the markup of the parts whose look does
    // not depend on the width.
    private sealed class TileContent
    {
        public string Label { get; }
        public int LabelWidth { get; }
        public string ValueMarkup { get; }
        public int ValueLineWidth { get; }
        public bool HasDelta { get; }
        public string DeltaMarkup { get; }
        public int DeltaWidth { get; }
        public string LimitText { get; }
        public int LimitWidth { get; }
        public int GaugeOverhead { get; }
        public string? StateStyle { get; }
        public string TrendStyle { get; }

        public int MinimumWidth => Math.Max(LabelWidth, ValueLineWidth);

        public TileContent(StatTile tile)
        {
            StateStyle = TerminalPalette.StateStyle(tile.State);

            TrendStyle = TerminalPalette.SecondaryStyle;
            if (StateStyle != null)
                TrendStyle = StateStyle;

            Label = MarkupParser.Escape(TextOf(tile.Label));
            LabelWidth = MarkupText.Measure(Label);

            var value = MarkupParser.Escape(TextOf(tile.Value));
            var valueWidth = MarkupText.Measure(value);
            var unit = MarkupParser.Escape(TextOf(tile.Unit));
            var unitWidth = MarkupText.Measure(unit);

            var valueStyle = ValueStyle;
            if (StateStyle != null)
                valueStyle = ValueStyle + " " + StateStyle;

            ValueMarkup = Styled(valueStyle, value);
            ValueLineWidth = valueWidth;
            if (unitWidth > 0)
            {
                var separator = valueWidth > 0 ? " " : string.Empty;
                ValueMarkup += separator + Styled(TerminalPalette.SecondaryStyle, unit);
                ValueLineWidth += separator.Length + unitWidth;
            }

            DeltaMarkup = string.Empty;
            var delta = tile.Delta;
            if (delta != null && !double.IsNaN(delta.Value.Change))
            {
                var change = delta.Value.Change;
                var text = MarkupParser.Escape(TextOf(delta.Value.Text));
                var textWidth = MarkupText.Measure(text);
                var arrow = change < 0 ? DownArrow : UpArrow;

                var deltaText = arrow;
                DeltaWidth = 1;
                if (textWidth > 0)
                {
                    deltaText = arrow + " " + text;
                    DeltaWidth += 1 + textWidth;
                }

                string? deltaStyle = null;
                if (change != 0)
                    deltaStyle = (change > 0) == delta.Value.UpIsGood ? TerminalPalette.OkStyle : TerminalPalette.FailedStyle;

                DeltaMarkup = Styled(deltaStyle, deltaText);
                HasDelta = true;
            }

            LimitText = string.Empty;
            var gauge = tile.Gauge;
            if (gauge != null)
            {
                LimitText = MarkupParser.Escape(TextOf(gauge.Value.LimitText));
                LimitWidth = MarkupText.Measure(LimitText);
                GaugeOverhead = GaugeCapCount;
                if (LimitWidth > 0)
                    GaugeOverhead += 1 + LimitWidth;
            }
        }
    }
}
