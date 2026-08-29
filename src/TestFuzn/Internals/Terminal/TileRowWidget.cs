using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Lays a row of <see cref="StatTile"/>s out as rounded boxes of equal width across exactly
/// <c>width</c> columns — header-less <see cref="PanelWidget"/> boxes, <see cref="Gap"/>
/// column apart, each holding the lines <see cref="StatTileWidget"/> renders at the box's inner
/// width — as exactly <c>height</c> lines of exactly <c>width</c> columns, where the height is
/// the tallest tile's line count plus the two border rows; a shorter tile pads blank inner
/// lines at the bottom, so every box in the row is the same height. The boxes share the width
/// equally and the columns left over (fewer than the tile count) widen the first boxes by one
/// each, so the row always spans the full width with no trailing gap. A tile that does not fit
/// its box degrades as <see cref="StatTileWidget"/> documents — its trend, then its delta, then
/// its gauge — and when a box would be narrower than its tile's
/// <see cref="StatTileWidget.MeasureMinimumWidth"/> (the label or the value line whole) tiles
/// are dropped from the right, one at a time, until every remaining tile fits, the boxes
/// re-sharing the width each time; a value is never truncated. No tiles (null entries are
/// skipped), a width below <see cref="MinimumBoxWidth"/>, or not even the first tile fitting
/// on its own renders nothing. Stateless and thread-safe.
/// </summary>
internal static class TileRowWidget
{
    /// <summary>Columns between two boxes.</summary>
    public const int Gap = 1;

    /// <summary>The narrowest box: the borders and their padding around one content column.</summary>
    public const int MinimumBoxWidth = PanelWidget.ContentOverhead + 1;

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<StatTile> tiles, int width, ColorMode colorMode, SparklineGlyphSet glyphSet = SparklineGlyphSet.Braille)
    {
        if (tiles == null)
            throw new ArgumentNullException(nameof(tiles), "Tiles cannot be null.");

        var present = new List<StatTile>(tiles.Count);
        foreach (var tile in tiles)
        {
            if (tile != null)
                present.Add(tile);
        }

        if (present.Count == 0 || width < MinimumBoxWidth)
            return Array.Empty<RenderedLine>();

        var minimumWidths = new int[present.Count];
        for (var index = 0; index < present.Count; index++)
            minimumWidths[index] = StatTileWidget.MeasureMinimumWidth(present[index]);

        for (var count = present.Count; count >= 1; count--)
        {
            var boxWidths = BoxWidths(count, width);
            if (boxWidths == null || !AllFit(minimumWidths, boxWidths))
                continue;

            return RenderRow(present, boxWidths, width, colorMode, glyphSet);
        }

        return Array.Empty<RenderedLine>();
    }

    // The widths of count equal boxes across the width, the leftover columns widening the first
    // boxes by one each; null when the boxes would be narrower than the minimum.
    private static int[]? BoxWidths(int count, int width)
    {
        var available = width - ((count - 1) * Gap);
        if (available < count * MinimumBoxWidth)
            return null;

        var baseWidth = available / count;
        var leftover = available - (baseWidth * count);

        var widths = new int[count];
        for (var index = 0; index < count; index++)
            widths[index] = baseWidth + (index < leftover ? 1 : 0);

        return widths;
    }

    private static bool AllFit(int[] minimumWidths, int[] boxWidths)
    {
        for (var index = 0; index < boxWidths.Length; index++)
        {
            if (minimumWidths[index] > boxWidths[index] - PanelWidget.ContentOverhead)
                return false;
        }

        return true;
    }

    // Every tile at its inner width, padded to the tallest, boxed, and the boxes joined row by
    // row: the box widths and the gaps add up to the width by construction.
    private static IReadOnlyList<RenderedLine> RenderRow(List<StatTile> tiles, int[] boxWidths, int width, ColorMode colorMode, SparklineGlyphSet glyphSet)
    {
        var count = boxWidths.Length;
        var contents = new IReadOnlyList<RenderedLine>[count];
        var height = 0;
        for (var index = 0; index < count; index++)
        {
            contents[index] = StatTileWidget.Render(tiles[index], boxWidths[index] - PanelWidget.ContentOverhead, colorMode, glyphSet);
            height = Math.Max(height, contents[index].Count);
        }

        var boxes = new IReadOnlyList<RenderedLine>[count];
        for (var index = 0; index < count; index++)
        {
            var lines = new List<RenderedLine>(height);
            lines.AddRange(contents[index]);
            while (lines.Count < height)
                lines.Add(BlankLine);

            boxes[index] = PanelWidget.Render(null, lines, boxWidths[index], colorMode);
        }

        var gap = new string(' ', Gap);
        var rows = new RenderedLine[height + 2];
        for (var row = 0; row < rows.Length; row++)
        {
            var text = new StringBuilder();
            for (var index = 0; index < count; index++)
            {
                if (index > 0)
                    text.Append(gap);

                text.Append(boxes[index][row].Text);
            }

            rows[row] = new RenderedLine(text.ToString(), width);
        }

        return rows;
    }
}
