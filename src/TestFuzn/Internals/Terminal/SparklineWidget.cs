namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders a single-line sparkline of recent samples, newest at the right, exactly the given
/// width. With <see cref="SparklineGlyphSet.Braille"/> each character holds two samples at four
/// vertical levels each, so the line windows the last 2×width samples; with
/// <see cref="SparklineGlyphSet.Blocks"/> one character is one sample at eight levels
/// (▁▂▃▄▅▆▇█), windowing the last width samples. Older samples are not drawn (the caller's
/// ring buffer is the state — the widget windows, it does not downsample) and fewer samples
/// leave blank columns at the left. Levels scale linearly between the smallest and largest
/// finite sample in the window, so negative values work, and a present sample never renders
/// blank — the smallest shows the lowest glyph. A window with no spread (all samples equal, or
/// a single sample) renders flat at the middle level (▄, or two braille dots). NaN samples
/// render as blank columns (a gap); positive infinity clamps to the top level and negative
/// infinity to the bottom level, and neither takes part in the min/max scale. The optional
/// style is markup tag words (e.g. "green") applied to the whole line; without one the line is
/// plain text in every color mode. Stateless and thread-safe — the values are read once and
/// never held.
/// </summary>
internal static class SparklineWidget
{
    private const string BlockGlyphs = "▁▂▃▄▅▆▇█";

    // Braille dot bits for a column filled bottom-up to level 1-4: the left column uses dots
    // 7, 3, 2, 1 of the U+2800 pattern block and the right column dots 8, 6, 5, 4.
    private static readonly int[] LeftColumnBits = { 0x00, 0x40, 0x44, 0x46, 0x47 };
    private static readonly int[] RightColumnBits = { 0x00, 0x80, 0xA0, 0xB0, 0xB8 };

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<double> values, int width, ColorMode colorMode, SparklineGlyphSet glyphSet = SparklineGlyphSet.Braille, string? style = null)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values), "Values cannot be null.");

        if (width < 1)
            return Array.Empty<RenderedLine>();

        var samplesPerCharacter = glyphSet == SparklineGlyphSet.Braille ? 2 : 1;
        var levelCount = glyphSet == SparklineGlyphSet.Braille ? 4 : 8;
        var columnCount = width * samplesPerCharacter;
        var windowStart = Math.Max(0, values.Count - columnCount);

        var (minimum, maximum) = FiniteRange(values, windowStart);

        // One level per column, left to right; leading columns without a sample stay 0 (blank).
        var levels = new int[columnCount];
        var firstSampleColumn = columnCount - (values.Count - windowStart);
        for (var index = windowStart; index < values.Count; index++)
            levels[firstSampleColumn + (index - windowStart)] = SampleLevel(values[index], minimum, maximum, levelCount);

        var glyphs = glyphSet == SparklineGlyphSet.Braille ? RenderBrailleGlyphs(levels, width) : RenderBlockGlyphs(levels);

        if (string.IsNullOrEmpty(style))
            return new[] { new RenderedLine(glyphs, width) };

        return new[] { MarkupText.RenderTruncated("[" + style + "]" + glyphs + "[/]", width, colorMode) };
    }

    private static (double Minimum, double Maximum) FiniteRange(IReadOnlyList<double> values, int windowStart)
    {
        var minimum = double.MaxValue;
        var maximum = double.MinValue;
        var hasFinite = false;
        for (var index = windowStart; index < values.Count; index++)
        {
            var value = values[index];
            if (!double.IsFinite(value))
                continue;

            hasFinite = true;
            if (value < minimum)
                minimum = value;
            if (value > maximum)
                maximum = value;
        }

        if (!hasFinite)
            return (0, 0);

        return (minimum, maximum);
    }

    private static int SampleLevel(double value, double minimum, double maximum, int levelCount)
    {
        if (double.IsNaN(value))
            return 0;
        if (double.IsPositiveInfinity(value))
            return levelCount;
        if (double.IsNegativeInfinity(value))
            return 1;

        var range = maximum - minimum;
        if (range <= 0)
            return levelCount / 2;

        // A spread wider than double.MaxValue overflows to infinity, which would turn the
        // normalized position into NaN and drop the largest sample to the bottom level. Halved
        // endpoints keep every difference finite, at precision far beyond what a handful of
        // levels can resolve.
        double position;
        if (double.IsPositiveInfinity(range))
            position = ((value / 2) - (minimum / 2)) / ((maximum / 2) - (minimum / 2));
        else
            position = (value - minimum) / range;

        return 1 + (int)Math.Round(position * (levelCount - 1), MidpointRounding.AwayFromZero);
    }

    private static string RenderBlockGlyphs(int[] levels)
    {
        var glyphs = new char[levels.Length];
        for (var index = 0; index < levels.Length; index++)
            glyphs[index] = levels[index] == 0 ? ' ' : BlockGlyphs[levels[index] - 1];

        return new string(glyphs);
    }

    private static string RenderBrailleGlyphs(int[] levels, int width)
    {
        var glyphs = new char[width];
        for (var index = 0; index < width; index++)
        {
            var bits = LeftColumnBits[levels[index * 2]] | RightColumnBits[levels[(index * 2) + 1]];
            glyphs[index] = bits == 0 ? ' ' : (char)(0x2800 | bits);
        }

        return new string(glyphs);
    }
}
