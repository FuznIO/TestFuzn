using System.Globalization;
using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders a multi-row area/line chart of one or more series, newest sample at the right, as
/// exactly <c>height</c> lines of exactly <c>width</c> columns: a left y-axis column, the chart
/// body, and the first series' newest value annotated at the right edge (<c>▶ 80</c>). Every
/// row goes through <see cref="MarkupText"/>, so all color modes work and
/// <see cref="ColorMode.None"/> output has no escape bytes. Stateless and thread-safe: the
/// series are read once and never held.
/// <para>
/// <b>Layout.</b> The axis holds the max label on the top row, the min label on the bottom row
/// and the midpoint (<c>(max + min) / 2</c>) on row <c>(height - 1) / 2</c> from height 3 up —
/// the row the midpoint value itself plots on at either height parity, since a row holds an
/// even number of levels — each right-aligned and followed by a tick (┤ on a label row, │
/// elsewhere); its width is the
/// widest label plus the tick column. Labels come from <see cref="ChartOptions.ValueFormatter"/>
/// ("0.#" invariant by default), escaped with <see cref="MarkupParser.Escape"/> and sanitized
/// by <see cref="MarkupText"/> like any user text; with no finite value in the window they are
/// blank and the axis is the tick column alone. The annotation is " ▶ " plus the first series'
/// newest visible value, drawn in that series' style on the row its value lands on and
/// reserved on every row so the body columns stay aligned; it is omitted when the first series
/// is empty or its newest value is not finite. The body takes what is left. Degradation, in
/// order: when the body would be narrower than <see cref="MinimumBodyWidth"/> the annotation
/// is dropped, then the axis (labels included). Height 1 never has an axis — the labels need
/// two rows — so it is the <see cref="SparklineWidget"/> look (the same four braille or eight
/// block levels) plus the annotation; a height or width below 1 renders nothing.
/// </para>
/// <para>
/// <b>Window.</b> Each series shows its newest <see cref="ChartOptions.TimeWindow"/> samples
/// (all of them without a limit). The body holds one sample per braille sub-column (two per
/// character) or one per block character; a window with at most that many samples is
/// stretched — resampled by linear interpolation so the oldest sample sits in the first column
/// and the newest in the last (a single sample fills the chart flat, and a gap widens to the
/// columns interpolated from it) — while a longer window scrolls, showing the newest samples
/// one per column with the newest at the right edge. All series share that one x-axis: the
/// column positions come from the longest window, and a series with fewer samples is padded
/// with gaps before its first one, so every series' newest sample sits at the right edge and a
/// shorter series occupies only the newest columns. The scale is shared: the smallest and
/// largest finite visible value across every series map to the bottom and top dot, values
/// between scale linearly with the level rounded half away from zero, so negative values work
/// and a present value never renders blank. A window without spread (one sample, or all equal)
/// puts every sample at the middle level — a Line runs across the middle, an Area fills the
/// lower half. NaN and ±Infinity are gaps: not drawn, never part of the scale.
/// </para>
/// <para>
/// <b>Glyphs.</b> <see cref="SparklineGlyphSet.Braille"/> packs two samples per character at
/// four levels per row (the <see cref="SparklineWidget"/> dot layout, stacked);
/// <see cref="SparklineGlyphSet.Blocks"/> draws one sample per character at eight levels per
/// row with ▁▂▃▄▅▆▇█. There is no ASCII set: like the other widgets the chart assumes the box
/// and block glyphs display everywhere, and only braille is the caller's opt-in. An
/// <see cref="ChartSeriesKind.Area"/> series fills every column from the bottom of the chart
/// up to its value; a <see cref="ChartSeriesKind.Line"/> series draws each sample's dot and,
/// in braille, joins consecutive samples — the levels strictly between two neighbours are
/// drawn as a vertical run split between the two columns, each level going to the column of
/// the nearer sample (a tie to the older one) — while in blocks it shows the partial block of
/// the row its value lands on, unjoined. Series draw in list order, later ones on top: a cell
/// any line passes through shows only the line dots (merged across lines) in the last such
/// line's style, so a line stays a thin line against a fill at the cost of that cell's fill;
/// a cell with only fills shows their merged dots in the style of the area owning most of
/// them, painter's order per dot and a tie to the later series, so a later, lower area shows
/// through a taller earlier one once it covers at least half of a cell. Gridlines, when
/// enabled, draw ┈ in the axis style through the empty cells of the label rows. Styles are
/// markup tag words; one the parser does not resolve renders unstyled rather than as literal
/// text, so a bad style can never push a row past the width.
/// </para>
/// </summary>
internal static class ChartWidget
{
    /// <summary>
    /// The fewest body columns worth drawing: when the axis and annotation would leave fewer,
    /// the annotation is dropped, then the axis.
    /// </summary>
    public const int MinimumBodyWidth = 4;

    private const string BlockGlyphs = "▁▂▃▄▅▆▇█";
    private const char GridlineGlyph = '┈';
    private const char AxisTickGlyph = '┤';
    private const char AxisLineGlyph = '│';
    private const string NewestValuePrefix = " ▶ ";
    private const string DefaultValueFormat = "0.#";

    private const int BrailleSamplesPerColumn = 2;
    private const int BrailleLevelsPerRow = 4;
    private const int BlockSamplesPerColumn = 1;
    private const int BlockLevelsPerRow = 8;

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<ChartSeries> series, int width, int height, ColorMode colorMode, SparklineGlyphSet glyphSet = SparklineGlyphSet.Braille, ChartOptions? options = null)
    {
        if (series == null)
            throw new ArgumentNullException(nameof(series), "Series cannot be null.");

        if (width < 1 || height < 1)
            return Array.Empty<RenderedLine>();

        if (options == null)
            options = ChartOptions.Default;

        var formatter = options.ValueFormatter;
        if (formatter == null)
            formatter = FormatDefault;

        var samplesPerColumn = glyphSet == SparklineGlyphSet.Braille ? BrailleSamplesPerColumn : BlockSamplesPerColumn;
        var levelsPerRow = glyphSet == SparklineGlyphSet.Braille ? BrailleLevelsPerRow : BlockLevelsPerRow;
        var levelCount = height * levelsPerRow;

        var windows = new double[series.Count][];
        var styles = new string?[series.Count];
        for (var index = 0; index < series.Count; index++)
        {
            windows[index] = TimeWindowed(series[index].Values, options.TimeWindow);
            styles[index] = ResolveStyle(series[index].Style);
        }

        AlignWindows(windows);

        var axisStyle = ResolveStyle(options.AxisStyle);

        string? newestLabel = null;
        if (options.ShowNewestValue && windows.Length > 0 && windows[0].Length > 0 && double.IsFinite(windows[0][windows[0].Length - 1]))
            newestLabel = FormatLabel(formatter, windows[0][windows[0].Length - 1]);

        var layout = ResolveLayout(windows, width, height, samplesPerColumn, newestLabel, options.ShowAxis, formatter);

        var canvas = new DotCanvas(height, layout.BodyWidth, glyphSet, samplesPerColumn, levelsPerRow);
        var newestLevel = 0;
        for (var index = 0; index < series.Count; index++)
        {
            var levels = Levels(layout.Visible[index], layout.Columns, layout.Scale, levelCount);
            if (index == 0)
                newestLevel = levels[levels.Length - 1];

            if (series[index].Kind == ChartSeriesKind.Line)
                PaintLine(canvas, index, levels, glyphSet);
            else
                PaintArea(canvas, index, levels);
        }

        var annotationRow = -1;
        if (layout.ShowAnnotation)
            annotationRow = newestLevel < 1 ? 0 : height - 1 - ((newestLevel - 1) / levelsPerRow);

        var lines = new RenderedLine[height];
        for (var row = 0; row < height; row++)
        {
            var markup = new StringBuilder();

            if (layout.ShowAxis)
                AppendAxis(markup, layout, row, height, axisStyle);

            AppendBody(markup, canvas, row, layout.BodyWidth, styles, options.ShowGridlines && IsLabelRow(row, height), axisStyle);

            if (layout.ShowAnnotation)
            {
                if (row == annotationRow)
                    AppendStyled(markup, NewestValuePrefix + newestLabel, styles[0]);
                else
                    markup.Append(' ', layout.AnnotationWidth);
            }

            // Every piece measures its declared width by construction; fitting through
            // MarkupText is what sanitizes the label text and pins the row to the width.
            lines[row] = MarkupText.RenderFitted(markup.ToString(), width, colorMode);
        }

        return lines;
    }

    // The newest timeWindow samples of a series (all of them without a limit, which a window
    // below 1 also means); a default ChartSeries, whose values are null, is an empty series.
    private static double[] TimeWindowed(IReadOnlyList<double>? values, int? timeWindow)
    {
        if (values == null || values.Count == 0)
            return Array.Empty<double>();

        var count = values.Count;
        if (timeWindow != null && timeWindow.Value >= 1 && timeWindow.Value < count)
            count = timeWindow.Value;

        var window = new double[count];
        var start = values.Count - count;
        for (var index = 0; index < count; index++)
            window[index] = values[start + index];

        return window;
    }

    // Pads every window shorter than the longest with gaps before its first sample, so all
    // series share one x-axis with their newest samples at the right edge.
    private static void AlignWindows(double[][] windows)
    {
        var longest = 0;
        foreach (var window in windows)
            longest = Math.Max(longest, window.Length);

        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            if (window.Length == longest)
                continue;

            var aligned = new double[longest];
            var padding = longest - window.Length;
            for (var position = 0; position < padding; position++)
                aligned[position] = double.NaN;

            Array.Copy(window, 0, aligned, padding, window.Length);
            windows[index] = aligned;
        }
    }

    // Settles the axis width, the annotation and the body width together. The visible window
    // (and so the scale and the labels) depends on the body width, which depends on the label
    // width: the loop starts without an axis and only ever widens it, so it terminates, and
    // keeps a wider axis than the final labels need rather than oscillating. Dropping the
    // annotation and then the axis is final.
    private static ChartLayout ResolveLayout(double[][] windows, int width, int height, int samplesPerColumn, string? newestLabel, bool showAxisOption, Func<double, string> formatter)
    {
        var showAxis = showAxisOption && height >= 2;
        var showAnnotation = newestLabel != null;
        var annotationWidth = newestLabel == null ? 0 : NewestValuePrefix.Length + MarkupText.Measure(newestLabel);
        var axisWidth = 0;
        var labels = new[] { string.Empty, string.Empty, string.Empty };

        while (true)
        {
            var bodyWidth = width - (showAxis ? axisWidth : 0) - (showAnnotation ? annotationWidth : 0);
            if (bodyWidth < MinimumBodyWidth && showAnnotation)
            {
                showAnnotation = false;
                continue;
            }

            if (bodyWidth < MinimumBodyWidth && showAxis)
            {
                showAxis = false;
                continue;
            }

            var columns = bodyWidth * samplesPerColumn;
            var visible = new double[windows.Length][];
            for (var index = 0; index < windows.Length; index++)
                visible[index] = Newest(windows[index], columns);

            var scale = FiniteRange(visible);

            if (showAxis)
            {
                labels = AxisLabels(scale, formatter, height);
                var labelWidth = 0;
                foreach (var label in labels)
                    labelWidth = Math.Max(labelWidth, MarkupText.Measure(label));

                if (labelWidth + 1 > axisWidth)
                {
                    axisWidth = labelWidth + 1;
                    continue;
                }
            }

            return new ChartLayout(showAxis, showAxis ? axisWidth : 0, showAnnotation, showAnnotation ? annotationWidth : 0, bodyWidth, columns, visible, scale, labels);
        }
    }

    private static double[] Newest(double[] window, int count)
    {
        if (window.Length <= count)
            return window;

        var newest = new double[count];
        Array.Copy(window, window.Length - count, newest, 0, count);
        return newest;
    }

    // Max, mid and min labels, in that order; blank without a finite value to scale by, and
    // the mid label blank below height 3, where it has no row of its own — it must not widen
    // the axis when it is never shown.
    private static string[] AxisLabels(ValueRange scale, Func<double, string> formatter, int height)
    {
        if (!scale.HasFinite)
            return new[] { string.Empty, string.Empty, string.Empty };

        var mid = string.Empty;
        if (height >= 3)
        {
            // Halved endpoints keep the midpoint finite for a spread wider than double.MaxValue.
            mid = FormatLabel(formatter, (scale.Minimum / 2) + (scale.Maximum / 2));
        }

        return new[] { FormatLabel(formatter, scale.Maximum), mid, FormatLabel(formatter, scale.Minimum) };
    }

    private static string FormatLabel(Func<double, string> formatter, double value)
    {
        var text = formatter(value);
        if (text == null)
            return string.Empty;

        return MarkupParser.Escape(text);
    }

    private static string FormatDefault(double value)
    {
        return value.ToString(DefaultValueFormat, CultureInfo.InvariantCulture);
    }

    // A style the parser resolves, or null: an unresolvable tag would render as literal text
    // and widen the row, and a bracket inside the words ("green][blue") would close or open a
    // tag of its own and leak a style past the run it was meant for, so both are dropped up
    // front.
    private static string? ResolveStyle(string? style)
    {
        if (string.IsNullOrEmpty(style))
            return null;

        if (style.IndexOf('[') >= 0 || style.IndexOf(']') >= 0)
            return null;

        if (MarkupText.Measure("[" + style + "]x[/]") != 1)
            return null;

        return style;
    }

    private static ValueRange FiniteRange(double[][] visible)
    {
        var minimum = double.MaxValue;
        var maximum = double.MinValue;
        var hasFinite = false;
        foreach (var values in visible)
        {
            foreach (var value in values)
            {
                if (!double.IsFinite(value))
                    continue;

                hasFinite = true;
                if (value < minimum)
                    minimum = value;
                if (value > maximum)
                    maximum = value;
            }
        }

        if (!hasFinite)
            return new ValueRange(false, 0, 0);

        return new ValueRange(true, minimum, maximum);
    }

    // One level per body column, 0 for a gap: the visible samples stretched across the columns
    // by linear interpolation when there are fewer, or one per column when they fill it.
    private static int[] Levels(double[] visible, int columns, ValueRange scale, int levelCount)
    {
        var levels = new int[columns];
        if (visible.Length == 0)
            return levels;

        if (visible.Length == 1 || columns == 1)
        {
            var level = SampleLevel(visible[visible.Length - 1], scale, levelCount);
            for (var column = 0; column < columns; column++)
                levels[column] = level;

            return levels;
        }

        var lastSample = visible.Length - 1;
        var lastColumn = columns - 1;
        for (var column = 0; column < columns; column++)
        {
            var position = (double)column * lastSample / lastColumn;
            var index = (int)Math.Floor(position);
            var fraction = position - index;

            double value;
            if (index >= lastSample)
                value = visible[lastSample];
            else if (fraction == 0)
                value = visible[index];
            else
                value = (visible[index] * (1 - fraction)) + (visible[index + 1] * fraction);

            levels[column] = SampleLevel(value, scale, levelCount);
        }

        return levels;
    }

    private static int SampleLevel(double value, ValueRange scale, int levelCount)
    {
        if (!double.IsFinite(value))
            return 0;

        var range = scale.Maximum - scale.Minimum;
        if (range <= 0)
            return levelCount / 2;

        // A spread wider than double.MaxValue overflows to infinity; halved endpoints keep every
        // difference finite (see SparklineWidget).
        double position;
        if (double.IsPositiveInfinity(range))
            position = ((value / 2) - (scale.Minimum / 2)) / ((scale.Maximum / 2) - (scale.Minimum / 2));
        else
            position = (value - scale.Minimum) / range;

        if (position < 0)
            position = 0;
        else if (position > 1)
            position = 1;

        return 1 + (int)Math.Round(position * (levelCount - 1), MidpointRounding.AwayFromZero);
    }

    private static void PaintArea(DotCanvas canvas, int seriesIndex, int[] levels)
    {
        for (var column = 0; column < levels.Length; column++)
        {
            if (levels[column] > 0)
                canvas.PaintArea(seriesIndex, column, levels[column]);
        }
    }

    // Each sample's own level plus, in braille, its share of the run joining it to its
    // neighbours: a level strictly between two consecutive samples goes to the column of the
    // nearer sample, a tie to the older column.
    private static void PaintLine(DotCanvas canvas, int seriesIndex, int[] levels, SparklineGlyphSet glyphSet)
    {
        for (var column = 0; column < levels.Length; column++)
        {
            var level = levels[column];
            if (level == 0)
                continue;

            var low = level;
            var high = level;
            if (glyphSet == SparklineGlyphSet.Braille)
            {
                if (column > 0 && levels[column - 1] != 0)
                    ExtendTowards(levels[column - 1], level, includeTies: false, ref low, ref high);

                if (column + 1 < levels.Length && levels[column + 1] != 0)
                    ExtendTowards(levels[column + 1], level, includeTies: true, ref low, ref high);
            }

            canvas.PaintLine(seriesIndex, column, low, high);
        }
    }

    // Widens [low, high] with the levels strictly between the neighbour's level and this
    // column's own that are nearer to this column's (or equally near, when ties are included).
    private static void ExtendTowards(int neighbourLevel, int ownLevel, bool includeTies, ref int low, ref int high)
    {
        var from = Math.Min(neighbourLevel, ownLevel) + 1;
        var to = Math.Max(neighbourLevel, ownLevel) - 1;
        for (var level = from; level <= to; level++)
        {
            var ownDistance = Math.Abs(level - ownLevel);
            var neighbourDistance = Math.Abs(level - neighbourLevel);
            if (ownDistance < neighbourDistance || (includeTies && ownDistance == neighbourDistance))
            {
                low = Math.Min(low, level);
                high = Math.Max(high, level);
            }
        }
    }

    // The top and bottom rows, and from height 3 up the row the midpoint value plots on: the
    // midpoint sits at level 1 + height × levelsPerRow / 2, which the level-to-row mapping
    // (height - 1 - (level - 1) / levelsPerRow) puts on row (height - 1) / 2 for both height
    // parities because levelsPerRow is even — height / 2 would be one row too low when the
    // height is even.
    private static bool IsLabelRow(int row, int height)
    {
        return row == 0 || row == height - 1 || (height >= 3 && row == (height - 1) / 2);
    }

    private static void AppendAxis(StringBuilder markup, ChartLayout layout, int row, int height, string? axisStyle)
    {
        string label;
        if (row == 0)
            label = layout.Labels[0];
        else if (row == height - 1)
            label = layout.Labels[2];
        else if (IsLabelRow(row, height))
            label = layout.Labels[1];
        else
            label = string.Empty;

        var tick = IsLabelRow(row, height) ? AxisTickGlyph : AxisLineGlyph;
        markup.Append(' ', layout.AxisWidth - 1 - MarkupText.Measure(label));
        AppendStyled(markup, label + tick, axisStyle);
    }

    // The body cells as runs of equally styled glyphs; a blank cell is never inside a styled
    // run, so a style with a background or reverse video never paints empty space.
    private static void AppendBody(StringBuilder markup, DotCanvas canvas, int row, int bodyWidth, string?[] styles, bool gridline, string? axisStyle)
    {
        string? runStyle = null;
        var run = new StringBuilder();
        for (var cell = 0; cell < bodyWidth; cell++)
        {
            var glyph = canvas.Compose(row, cell, out var styleIndex);
            string? style = null;
            if (glyph == ' ')
            {
                if (gridline)
                {
                    glyph = GridlineGlyph;
                    style = axisStyle;
                }
            }
            else if (styleIndex >= 0)
            {
                style = styles[styleIndex];
            }

            if (!string.Equals(style, runStyle, StringComparison.Ordinal))
            {
                AppendStyled(markup, run.ToString(), runStyle);
                run.Clear();
                runStyle = style;
            }

            run.Append(glyph);
        }

        AppendStyled(markup, run.ToString(), runStyle);
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

    private readonly struct ValueRange
    {
        public bool HasFinite { get; }
        public double Minimum { get; }
        public double Maximum { get; }

        public ValueRange(bool hasFinite, double minimum, double maximum)
        {
            HasFinite = hasFinite;
            Minimum = minimum;
            Maximum = maximum;
        }
    }

    private sealed class ChartLayout
    {
        public bool ShowAxis { get; }
        public int AxisWidth { get; }
        public bool ShowAnnotation { get; }
        public int AnnotationWidth { get; }
        public int BodyWidth { get; }
        public int Columns { get; }
        public double[][] Visible { get; }
        public ValueRange Scale { get; }
        public string[] Labels { get; }

        public ChartLayout(bool showAxis, int axisWidth, bool showAnnotation, int annotationWidth, int bodyWidth, int columns, double[][] visible, ValueRange scale, string[] labels)
        {
            ShowAxis = showAxis;
            AxisWidth = axisWidth;
            ShowAnnotation = showAnnotation;
            AnnotationWidth = annotationWidth;
            BodyWidth = bodyWidth;
            Columns = columns;
            Visible = visible;
            Scale = scale;
            Labels = labels;
        }
    }

    // The body as cells of eight slots each — in braille the two sub-columns' four dot rows
    // (slots 0-3 left, 4-7 right, bottom-up), in blocks the eight eighths of the one column,
    // bottom-up — painted in two layers. The area layer records per slot which series painted
    // it last; the line layer records per cell the merged slots and the last line series that
    // touched it. Compose resolves a cell to its glyph and style per the class summary.
    private sealed class DotCanvas
    {
        private const int SlotsPerCell = 8;

        // Braille dot bits by slot: left sub-column dots 7, 3, 2, 1 then right sub-column dots
        // 8, 6, 5, 4 of the U+2800 pattern block, each bottom-up.
        private static readonly int[] BrailleSlotBits = { 0x40, 0x04, 0x02, 0x01, 0x80, 0x20, 0x10, 0x08 };

        private readonly int _rows;
        private readonly int _cells;
        private readonly SparklineGlyphSet _glyphSet;
        private readonly int _samplesPerColumn;
        private readonly int _levelsPerRow;
        private readonly int[] _areaOwner;
        private readonly int[] _lineSlots;
        private readonly int[] _lineOwner;

        public DotCanvas(int rows, int cells, SparklineGlyphSet glyphSet, int samplesPerColumn, int levelsPerRow)
        {
            _rows = rows;
            _cells = cells;
            _glyphSet = glyphSet;
            _samplesPerColumn = samplesPerColumn;
            _levelsPerRow = levelsPerRow;
            _areaOwner = new int[rows * cells * SlotsPerCell];
            _lineSlots = new int[rows * cells];
            _lineOwner = new int[rows * cells];
        }

        public void PaintArea(int seriesIndex, int sampleColumn, int level)
        {
            for (var current = 1; current <= level; current++)
            {
                Locate(sampleColumn, current, out var cellIndex, out var slot);
                _areaOwner[(cellIndex * SlotsPerCell) + slot] = seriesIndex + 1;
            }
        }

        // In braille each level is its own dot; in blocks a level is the partial block from
        // the bottom of its row, so every lower slot of that row is set with it.
        public void PaintLine(int seriesIndex, int sampleColumn, int lowLevel, int highLevel)
        {
            for (var current = lowLevel; current <= highLevel; current++)
            {
                Locate(sampleColumn, current, out var cellIndex, out var slot);
                var firstSlot = _glyphSet == SparklineGlyphSet.Braille ? slot : 0;
                for (var filled = firstSlot; filled <= slot; filled++)
                    _lineSlots[cellIndex] |= 1 << filled;

                _lineOwner[cellIndex] = seriesIndex + 1;
            }
        }

        public char Compose(int row, int cell, out int styleIndex)
        {
            var cellIndex = (row * _cells) + cell;
            if (_lineOwner[cellIndex] != 0)
            {
                styleIndex = _lineOwner[cellIndex] - 1;
                return Glyph(_lineSlots[cellIndex]);
            }

            var mask = 0;
            var bestOwner = 0;
            var bestCount = 0;
            var firstSlot = cellIndex * SlotsPerCell;
            for (var slot = 0; slot < SlotsPerCell; slot++)
            {
                var owner = _areaOwner[firstSlot + slot];
                if (owner == 0)
                    continue;

                mask |= 1 << slot;

                var count = 0;
                for (var other = 0; other < SlotsPerCell; other++)
                {
                    if (_areaOwner[firstSlot + other] == owner)
                        count++;
                }

                if (count > bestCount || (count == bestCount && owner > bestOwner))
                {
                    bestCount = count;
                    bestOwner = owner;
                }
            }

            styleIndex = bestOwner - 1;
            return Glyph(mask);
        }

        private void Locate(int sampleColumn, int level, out int cellIndex, out int slot)
        {
            var cell = sampleColumn / _samplesPerColumn;
            var subColumn = sampleColumn % _samplesPerColumn;
            var rowFromBottom = (level - 1) / _levelsPerRow;
            var row = _rows - 1 - rowFromBottom;

            cellIndex = (row * _cells) + cell;
            slot = (subColumn * _levelsPerRow) + ((level - 1) % _levelsPerRow);
        }

        private char Glyph(int mask)
        {
            if (mask == 0)
                return ' ';

            if (_glyphSet == SparklineGlyphSet.Braille)
            {
                var bits = 0;
                for (var slot = 0; slot < SlotsPerCell; slot++)
                {
                    if ((mask & (1 << slot)) != 0)
                        bits |= BrailleSlotBits[slot];
                }

                return (char)(0x2800 | bits);
            }

            // Block slots are always filled bottom-up, so the count is the fill level.
            var filled = 0;
            for (var slot = 0; slot < SlotsPerCell; slot++)
            {
                if ((mask & (1 << slot)) != 0)
                    filled++;
            }

            return BlockGlyphs[filled - 1];
        }
    }
}
