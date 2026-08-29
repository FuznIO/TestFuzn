using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders a time × latency-bucket heatmap — one row per bucket with the slowest on top, one
/// column per sample with the newest at the right — as exactly <c>height</c> lines of exactly
/// <c>width</c> columns: a left label column with the bucket edges and the heat body. Every row
/// goes through <see cref="MarkupText"/>, so all color modes work and
/// <see cref="ColorMode.None"/> output has no escape bytes. Stateless and thread-safe: the
/// samples are read once and never held.
/// <para>
/// <b>Input.</b> <c>bucketCountsPerSample</c> holds one entry per sample, oldest first and
/// newest last (the order of <see cref="LiveMetricsSnapshot.LatencyBucketSeries"/>), each the
/// counts per bucket with the fastest bucket first; <c>bucketLabels</c> names the buckets in
/// that same order, one per bucket, so its count is the bucket count — and when it is empty
/// (a body-only render) the bucket count is the longest sample's length instead, so the
/// buckets still draw, just without labels. The samples are never trusted to be regular: a
/// sample with fewer counts than buckets is padded with zeros and one with more is truncated
/// to the labelled buckets (the extra counts do not join its total), a negative count reads
/// as zero, and a null sample is an empty column. Counts are summed in 64 bits, so
/// int.MaxValue in every bucket cannot overflow. Labels are user text: escaped with
/// <see cref="MarkupParser.Escape"/> and sanitized by <see cref="MarkupText"/>, never parsed
/// as markup. A width or height below 1 renders nothing; the lists themselves cannot be null.
/// </para>
/// <para>
/// <b>Rows.</b> With room for every bucket the slowest takes the top row and the fastest the
/// row <c>bucketCount - 1</c>; the rows below that stay blank, with no label, when the height
/// is greater. When the height is smaller, adjacent buckets merge into one row until the count
/// fits: each merge joins the two adjacent rows whose combined bucket count is the smallest
/// (the pair nearest the middle of the row list when several tie — measured by the pair's
/// centre against the list's centre — and the faster pair on an exact tie), so the buckets
/// halve evenly from the middle outward, with the outermost rows merged last. The exact tie
/// going to the faster pair makes an odd bucket count asymmetric by one step: fifteen
/// buckets at height 14 merge only the two middle ones, at height 8 every pair below the
/// slowest bucket (the fastest has just lost its own row) and the slowest bucket merges at
/// height 7, at height 1 all of them. A merged row's counts are the sum of its members' and
/// its label is its slowest member's.
/// </para>
/// <para>
/// <b>Label column.</b> The labels of the rows shown, right-aligned to the widest one plus a
/// separating space — a label is never truncated: the column grows to the widest label. It is
/// dropped entirely, labels included, when it would leave the body narrower than
/// <see cref="MinimumBodyWidth"/>, and it is absent when every shown label is empty or
/// <see cref="HeatmapOptions.ShowLabels"/> is off. Its style is
/// <see cref="HeatmapOptions.LabelStyle"/>; one the parser does not resolve renders unstyled
/// rather than as literal text, so a bad style can never push a row past the width. The body
/// takes what is left.
/// </para>
/// <para>
/// <b>Columns.</b> The newest <see cref="HeatmapOptions.TimeWindow"/> samples are the window
/// (all of them without a limit). A window that fits the body draws one sample per column, the
/// newest in the rightmost column and older ones to its left, with the unused columns blank on
/// the left. A longer window in <see cref="ColorMode.TrueColor"/> packs two samples per column
/// as half cells, doubling the visible span: ▌ with the older (left) sample's color as the
/// foreground and the newer (right) sample's as the background; a column whose older half is
/// blank draws ▐ in the newer sample's color and one whose newer half is blank draws ▌ in the
/// older's, so a blank half never carries a background color. In every other mode a longer
/// window scrolls, showing the newest <c>bodyWidth</c> samples one per column — the shade and
/// ASCII glyphs carry the intensity, and a half cell can only carry two colors (the 16-color
/// palette has no six-step single-accent ramp for a background).
/// </para>
/// <para>
/// <b>Intensity.</b> A cell shows its sample's count in the row's bucket(s) as a share of that
/// sample's total over every labelled bucket (per sample: a column, or one half of a packed
/// column), weighted by the sample's volume so a quiet second cannot outshine a busy one: the
/// step is <c>ceil(share × volumeFactor × RampSteps)</c> with
/// <c>volumeFactor = min(1, total / reference)</c>, where the reference is the median of the
/// non-empty visible samples' totals (the mean of the two middle ones for an even count) — a
/// median so one burst second cannot dim the rest, and the sample's own total when it is the
/// only non-empty one, so its factor is 1. That is <c>ceil(count × RampSteps / max(total,
/// reference))</c>, computed in integers with the reference kept doubled. Under a steady load
/// every factor is about 1 and the step is the share alone: step 0 (blank) for a share of
/// zero, and steps 1-6 for shares in (0, 1/6], (1/6, 2/6], (2/6, 3/6], (3/6, 4/6], (4/6, 5/6]
/// and (5/6, 1]; a ramp-up brightens as its volume approaches the typical one, and a lone
/// request among busy seconds reads as a faint step 1. A sample whose total is zero (idle,
/// null, all negative) is a blank column in every row. TrueColor draws █ in the palette's six
/// heat stops (<see cref="TerminalPalette.HeatDarkEmberStyle"/> to
/// <see cref="TerminalPalette.HeatWhiteHotStyle"/>): dark ember through the logo's gradient —
/// its start, its midpoint, its end — to white-hot. <see cref="ColorMode.Colors16"/>
/// keeps to the yellow accent with the shade blocks: ░ ▒ ▓ in dark yellow (olive) for steps
/// 1-3, ▓ and █ in bright yellow for 4 and 5, and █ in bold white for 6.
/// <see cref="ColorMode.None"/> and <see cref="ColorMode.Monochrome"/> use the ASCII ramp
/// <c>.:=+*#</c> — the classic density ramp less its hyphen, which reads as a rule rather
/// than a density — with a blank for step 0 and no styling. Equally styled neighbouring cells
/// share one styled run, and a blank cell is never inside one, so a background never paints
/// empty space.
/// </para>
/// </summary>
internal static class HeatmapWidget
{
    /// <summary>
    /// The fewest body columns worth drawing: when the label column would leave fewer, it is
    /// dropped.
    /// </summary>
    public const int MinimumBodyWidth = 4;

    /// <summary>
    /// The number of intensity steps above blank: a cell's step is
    /// <c>ceil(share × volumeFactor × RampSteps)</c> — its count's share of the sample's total,
    /// weighted by the total against the median total of the visible samples, so the top step
    /// takes a whole column at a typical volume and a quiet second stays faint (see the class
    /// summary).
    /// </summary>
    public const int RampSteps = 6;

    private const char FullBlock = '█';
    private const char LeftHalfBlock = '▌';
    private const char RightHalfBlock = '▐';

    // Steps 0-6 without color: blank, then the classic ASCII density ramp less its hyphen.
    private const string AsciiRamp = " .:=+*#";

    // Steps 0-6 in Colors16: the shade blocks in the classic palette's dark and bright yellow,
    // bold white on top. Retheme the 16-color look by tweaking these two arrays together.
    private static readonly char[] ShadeGlyphs = { ' ', '░', '▒', '▓', '▓', '█', '█' };
    private static readonly string?[] ShadeStyles = { null, "olive", "olive", "olive", "yellow", "yellow", "bold white" };

    // Steps 0-6 in TrueColor: blank, then the palette's six heat stops — dark ember through the
    // logo's warm gradient to white-hot. Retheme the gradient in TerminalPalette.
    private static readonly string?[] GradientStyles =
    {
        null,
        TerminalPalette.HeatDarkEmberStyle,
        TerminalPalette.HeatEmberStyle,
        TerminalPalette.HeatOrangeStyle,
        TerminalPalette.HeatLightOrangeStyle,
        TerminalPalette.HeatAmberStyle,
        TerminalPalette.HeatWhiteHotStyle
    };

    // Every packed-cell style by [older step][newer step] — the older's color on the newer's,
    // one color alone when the other half is blank, null when both are — built once so a
    // packed frame allocates no style strings per cell. Declared after GradientStyles, which
    // it reads while the type initializes.
    private static readonly string?[][] HalfCellStyles = BuildHalfCellStyles();

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<IReadOnlyList<int>> bucketCountsPerSample, IReadOnlyList<string> bucketLabels, int width, int height, ColorMode colorMode, HeatmapOptions? options = null)
    {
        if (bucketCountsPerSample == null)
            throw new ArgumentNullException(nameof(bucketCountsPerSample), "Bucket counts per sample cannot be null.");
        if (bucketLabels == null)
            throw new ArgumentNullException(nameof(bucketLabels), "Bucket labels cannot be null.");

        if (width < 1 || height < 1)
            return Array.Empty<RenderedLine>();

        if (options == null)
            options = HeatmapOptions.Default;

        // The labels name the buckets; without any, the samples themselves say how many there are.
        var bucketCount = bucketLabels.Count;
        if (bucketCount == 0)
            bucketCount = LongestSampleLength(bucketCountsPerSample);

        var rows = MergeRows(bucketCount, height);
        var labels = RowLabels(rows, bucketLabels);
        var labelStyle = MarkupText.ResolveStyle(options.LabelStyle);

        var labelWidth = 0;
        if (options.ShowLabels)
        {
            foreach (var label in labels)
                labelWidth = Math.Max(labelWidth, MarkupText.Measure(label));
        }

        var labelColumnWidth = labelWidth == 0 ? 0 : labelWidth + 1;
        if (width - labelColumnWidth < MinimumBodyWidth)
            labelColumnWidth = 0;

        var bodyWidth = width - labelColumnWidth;

        var windowCount = TimeWindowCount(bucketCountsPerSample.Count, options.TimeWindow);
        var samplesPerColumn = colorMode == ColorMode.TrueColor && windowCount > bodyWidth ? 2 : 1;
        var slotSteps = SlotSteps(bucketCountsPerSample, windowCount, rows, bodyWidth * samplesPerColumn);

        var lines = new RenderedLine[height];
        for (var row = 0; row < height; row++)
        {
            // The slowest bucket's row is the top row; rows past the last group are blank.
            var group = rows.Count - 1 - row;
            var markup = new StringBuilder();

            if (labelColumnWidth > 0)
            {
                var label = group >= 0 ? labels[group] : string.Empty;
                markup.Append(' ', labelWidth - MarkupText.Measure(label));
                AppendStyled(markup, label, labelStyle);
                markup.Append(' ');
            }

            AppendBody(markup, slotSteps, group, bodyWidth, samplesPerColumn, colorMode);

            // Every piece measures its declared width by construction; fitting through
            // MarkupText is what sanitizes the label text and pins the row to the width.
            lines[row] = MarkupText.RenderFitted(markup.ToString(), width, colorMode);
        }

        return lines;
    }

    private static int LongestSampleLength(IReadOnlyList<IReadOnlyList<int>> samples)
    {
        var longest = 0;
        foreach (var sample in samples)
        {
            if (sample != null && sample.Count > longest)
                longest = sample.Count;
        }

        return longest;
    }

    // The rows as bucket ranges, fastest first: one per bucket, merged from the middle outward
    // per the class summary until at most height remain. Each merge joins the adjacent pair
    // with the smallest combined bucket count, nearest the middle on a tie (the distance is the
    // doubled offset of the pair's centre from the list's centre, kept in integers), and the
    // scan order gives an exact tie to the faster pair.
    private static List<BucketRange> MergeRows(int bucketCount, int height)
    {
        var rows = new List<BucketRange>(bucketCount);
        for (var bucket = 0; bucket < bucketCount; bucket++)
            rows.Add(new BucketRange(bucket, bucket));

        while (rows.Count > height)
        {
            var best = 0;
            var bestSize = int.MaxValue;
            var bestDistance = int.MaxValue;
            for (var pair = 0; pair + 1 < rows.Count; pair++)
            {
                var size = rows[pair].Count + rows[pair + 1].Count;
                var distance = Math.Abs((2 * pair) + 2 - rows.Count);
                if (size < bestSize || (size == bestSize && distance < bestDistance))
                {
                    best = pair;
                    bestSize = size;
                    bestDistance = distance;
                }
            }

            rows[best] = new BucketRange(rows[best].First, rows[best + 1].Last);
            rows.RemoveAt(best + 1);
        }

        return rows;
    }

    // One escaped label per row, its slowest member's; a missing or null label is empty.
    private static string[] RowLabels(List<BucketRange> rows, IReadOnlyList<string> bucketLabels)
    {
        var labels = new string[rows.Count];
        for (var group = 0; group < rows.Count; group++)
        {
            string? label = null;
            if (rows[group].Last < bucketLabels.Count)
                label = bucketLabels[rows[group].Last];

            if (label == null)
                label = string.Empty;

            labels[group] = MarkupParser.Escape(label);
        }

        return labels;
    }

    // The newest timeWindow samples (all of them without a limit, which a window below 1 also means).
    private static int TimeWindowCount(int sampleCount, int? timeWindow)
    {
        if (timeWindow != null && timeWindow.Value >= 1 && timeWindow.Value < sampleCount)
            return timeWindow.Value;

        return sampleCount;
    }

    // The intensity step of every row for every slot (a column, or half of a packed one), the
    // window's newest sample in the last slot and older ones before it; null for a slot without
    // a sample or whose sample has no counts in the labelled buckets — a blank column. The rows
    // partition the labelled buckets, so the row sums add up to the sample's total. Two passes:
    // the sums and totals first, since the volume reference is the median of the totals of the
    // slots that are drawn, then the steps against it.
    private static int[]?[] SlotSteps(IReadOnlyList<IReadOnlyList<int>> samples, int windowCount, List<BucketRange> rows, int slotCount)
    {
        var sums = new long[]?[slotCount];
        var totals = new long[slotCount];
        var windowStart = samples.Count - windowCount;
        var firstSlot = slotCount - windowCount;
        for (var slot = Math.Max(0, firstSlot); slot < slotCount; slot++)
        {
            var sample = samples[windowStart + (slot - firstSlot)];
            if (sample == null)
                continue;

            var slotSums = new long[rows.Count];
            long total = 0;
            for (var group = 0; group < rows.Count; group++)
            {
                long sum = 0;
                for (var bucket = rows[group].First; bucket <= rows[group].Last && bucket < sample.Count; bucket++)
                {
                    var count = sample[bucket];
                    if (count > 0)
                        sum += count;
                }

                slotSums[group] = sum;
                total += sum;
            }

            if (total == 0)
                continue;

            sums[slot] = slotSums;
            totals[slot] = total;
        }

        var doubledReference = DoubledMedianTotal(totals);

        var steps = new int[]?[slotCount];
        for (var slot = 0; slot < slotCount; slot++)
        {
            var slotSums = sums[slot];
            if (slotSums == null)
                continue;

            var slotSteps = new int[rows.Count];
            for (var group = 0; group < rows.Count; group++)
                slotSteps[group] = Step(slotSums[group], totals[slot], doubledReference);

            steps[slot] = slotSteps;
        }

        return steps;
    }

    // Twice the median of the non-zero totals — the mean of the two middle values for an even
    // count, so it is kept doubled to stay in integers; 0 when no slot has a total.
    private static long DoubledMedianTotal(long[] totals)
    {
        var nonEmpty = new List<long>(totals.Length);
        foreach (var total in totals)
        {
            if (total > 0)
                nonEmpty.Add(total);
        }

        if (nonEmpty.Count == 0)
            return 0;

        nonEmpty.Sort();
        var middle = nonEmpty.Count / 2;
        if (nonEmpty.Count % 2 == 1)
            return 2 * nonEmpty[middle];

        return nonEmpty[middle - 1] + nonEmpty[middle];
    }

    // ceil(count / max(total, reference) × RampSteps), with the reference doubled: 0 for no
    // count, 1 up to a sixth of the larger of the sample's total and the typical one, ..., 6 for
    // the whole of it. count never exceeds total, so the result never exceeds RampSteps.
    private static int Step(long count, long total, long doubledReference)
    {
        if (count <= 0)
            return 0;

        var denominator = Math.Max(2 * total, doubledReference);
        return (int)(((2 * count * RampSteps) + denominator - 1) / denominator);
    }

    private static int StepAt(int[]?[] slotSteps, int slot, int group)
    {
        if (group < 0)
            return 0;

        var steps = slotSteps[slot];
        if (steps == null)
            return 0;

        return steps[group];
    }

    // The body cells as runs of equally styled glyphs; a blank cell is never inside a styled
    // run, so a background never paints empty space.
    private static void AppendBody(StringBuilder markup, int[]?[] slotSteps, int group, int bodyWidth, int samplesPerColumn, ColorMode colorMode)
    {
        string? runStyle = null;
        var run = new StringBuilder();
        for (var column = 0; column < bodyWidth; column++)
        {
            char glyph;
            string? style;
            if (samplesPerColumn == 2)
                HalfCell(StepAt(slotSteps, column * 2, group), StepAt(slotSteps, (column * 2) + 1, group), out glyph, out style);
            else
                Cell(StepAt(slotSteps, column, group), colorMode, out glyph, out style);

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

    private static void Cell(int step, ColorMode colorMode, out char glyph, out string? style)
    {
        switch (colorMode)
        {
            case ColorMode.TrueColor:
                glyph = step == 0 ? ' ' : FullBlock;
                style = GradientStyles[step];
                return;
            case ColorMode.Colors16:
                glyph = ShadeGlyphs[step];
                style = ShadeStyles[step];
                return;
            default:
                glyph = AsciiRamp[step];
                style = null;
                return;
        }
    }

    // Two samples in one TrueColor cell: the older one as the foreground of ▌, the newer one as
    // its background; a blank half gets no color at all — ▐ or ▌ in the other half's color.
    private static void HalfCell(int olderStep, int newerStep, out char glyph, out string? style)
    {
        style = HalfCellStyles[olderStep][newerStep];
        if (olderStep > 0)
            glyph = LeftHalfBlock;
        else if (newerStep > 0)
            glyph = RightHalfBlock;
        else
            glyph = ' ';
    }

    private static string?[][] BuildHalfCellStyles()
    {
        var styles = new string?[RampSteps + 1][];
        for (var older = 0; older <= RampSteps; older++)
        {
            styles[older] = new string?[RampSteps + 1];
            for (var newer = 0; newer <= RampSteps; newer++)
            {
                if (older > 0 && newer > 0)
                    styles[older][newer] = GradientStyles[older] + " on " + GradientStyles[newer];
                else if (older > 0)
                    styles[older][newer] = GradientStyles[older];
                else if (newer > 0)
                    styles[older][newer] = GradientStyles[newer];
            }
        }

        return styles;
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

    // A row's buckets: an inclusive range of bucket indices, fastest first.
    private readonly struct BucketRange
    {
        public int First { get; }
        public int Last { get; }
        public int Count => Last - First + 1;

        public BucketRange(int first, int last)
        {
            First = first;
            Last = last;
        }
    }
}
