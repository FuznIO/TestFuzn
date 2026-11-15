using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class MetricsTableLayout
{
    internal const string DurationsTitle = "Response Times";

    private const string TitleStyle = "bold";
    private const int TableGap = 4;

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    internal static readonly TableColumn[] CountColumns =
    {
        HeaderColumn("Metric"),
        NumberColumn("Count"),
        NumberColumn("Per Second")
    };

    internal static readonly TableColumn[] DurationColumns =
    {
        HeaderColumn("Metric"),
        NumberColumn("Min"),
        NumberColumn("Mean"),
        NumberColumn("Max"),
        NumberColumn("StdDev"),
        NumberColumn("Median"),
        NumberColumn("P75"),
        NumberColumn("P95"),
        NumberColumn("P99")
    };

    private static readonly int[][][] DurationColumnTiers =
    {
        new[] { new[] { 1, 2, 3, 4, 5, 6, 7, 8 } },
        new[] { new[] { 1, 2, 3, 4 }, new[] { 5, 6, 7, 8 } },
        new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 }, new[] { 7, 8 } }
    };

    public static IReadOnlyList<RenderedLine> Render(string countTitle, IReadOnlyList<IReadOnlyList<string?>> countRows, IReadOnlyList<IReadOnlyList<string?>> durationRows, int innerWidth, ColorMode colorMode, bool allowOverflow = true)
    {
        var countWidth = BlockWidth(countTitle, CountColumns, countRows);
        var durationsWidth = BlockWidth(DurationsTitle, DurationColumns, durationRows);
        if (!allowOverflow && countWidth > innerWidth)
            countWidth = innerWidth;

        if (countWidth + TableGap + durationsWidth <= innerWidth)
        {
            var left = TitledTableLines(countTitle, CountColumns, countRows, countWidth, colorMode);
            var right = TitledTableLines(DurationsTitle, DurationColumns, durationRows, durationsWidth, colorMode);
            return SideBySide(left, right, countWidth);
        }

        var lines = new List<RenderedLine>();
        lines.AddRange(TitledTableLines(countTitle, CountColumns, countRows, countWidth, colorMode));
        lines.Add(BlankLine);
        lines.Add(MarkupText.RenderTruncated(TitleMarkup(DurationsTitle), innerWidth, colorMode));
        foreach (var columnGroup in SelectDurationTier(durationRows, innerWidth))
        {
            var columns = SelectColumns(columnGroup);
            var rows = SelectCells(durationRows, columnGroup);
            var tableWidth = innerWidth;
            if (allowOverflow)
                tableWidth = Math.Max(innerWidth, TableWidget.MeasureNaturalWidth(columns, rows));

            lines.AddRange(TableWidget.Render(columns, rows, tableWidth, colorMode));
        }

        return lines;
    }

    public static int MeasureMinimumWidth(string countTitle, IReadOnlyList<IReadOnlyList<string?>> countRows, IReadOnlyList<IReadOnlyList<string?>> durationRows)
    {
        var width = BlockWidth(countTitle, CountColumns, countRows);
        width = Math.Max(width, MarkupText.Measure(TitleMarkup(DurationsTitle)));
        return Math.Max(width, TierWidth(DurationColumnTiers[DurationColumnTiers.Length - 1], durationRows));
    }

    public static List<IReadOnlyList<string?>> CountRows(int count, int okCount, int okPerSecond, int failedCount, int failedPerSecond, int? skippedCount)
    {
        var rows = new List<IReadOnlyList<string?>>();
        rows.Add(new[] { "Total", LiveDashboardLayout.FormatCount(count), string.Empty });
        rows.Add(new[] { Styled(LiveDashboardLayout.OkStyle, "OK"), LiveDashboardLayout.FormatCount(okCount), LiveDashboardLayout.FormatCount(okPerSecond) });
        rows.Add(new[] { Styled(LiveDashboardLayout.FailedStyle, "Failed"), LiveDashboardLayout.FormatCount(failedCount), LiveDashboardLayout.FormatCount(failedPerSecond) });
        if (skippedCount != null)
            rows.Add(new[] { Styled(LiveDashboardLayout.SecondaryStyle, "Skipped"), LiveDashboardLayout.FormatCount(skippedCount.Value), string.Empty });

        return rows;
    }

    public static string?[] DurationRow(string label, string style, TimeSpan min, TimeSpan mean, TimeSpan max, TimeSpan standardDeviation, TimeSpan median, TimeSpan percentile75, TimeSpan percentile95, TimeSpan percentile99)
    {
        return new[]
        {
            Styled(style, label),
            Styled(style, min.ToTestFuznDuration()),
            Styled(style, mean.ToTestFuznDuration()),
            Styled(style, max.ToTestFuznDuration()),
            Styled(style, standardDeviation.ToTestFuznDuration()),
            Styled(style, median.ToTestFuznDuration()),
            Styled(style, percentile75.ToTestFuznDuration()),
            Styled(style, percentile95.ToTestFuznDuration()),
            Styled(style, percentile99.ToTestFuznDuration())
        };
    }

    public static RenderedLine PadRight(RenderedLine line, int width)
    {
        if (line.Width >= width)
            return line;

        return new RenderedLine(line.Text + new string(' ', width - line.Width), width);
    }

    public static TableColumn HeaderColumn(string header)
    {
        return new TableColumn("[" + LiveDashboardLayout.SecondaryStyle + "]" + header + "[/]");
    }

    public static TableColumn NumberColumn(string header)
    {
        return new TableColumn("[" + LiveDashboardLayout.SecondaryStyle + "]" + header + "[/]") { Alignment = TextAlignment.Right };
    }

    public static string Styled(string style, string markup)
    {
        return "[" + style + "]" + markup + "[/]";
    }

    private static int BlockWidth(string title, TableColumn[] columns, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        return Math.Max(MarkupText.Measure(TitleMarkup(title)), TableWidget.MeasureNaturalWidth(columns, rows));
    }

    private static int[][] SelectDurationTier(IReadOnlyList<IReadOnlyList<string?>> durationRows, int innerWidth)
    {
        foreach (var tier in DurationColumnTiers)
        {
            if (TierWidth(tier, durationRows) <= innerWidth)
                return tier;
        }

        return DurationColumnTiers[DurationColumnTiers.Length - 1];
    }

    private static int TierWidth(int[][] tier, IReadOnlyList<IReadOnlyList<string?>> durationRows)
    {
        var width = 0;
        foreach (var columnGroup in tier)
            width = Math.Max(width, TableWidget.MeasureNaturalWidth(SelectColumns(columnGroup), SelectCells(durationRows, columnGroup)));

        return width;
    }

    private static TableColumn[] SelectColumns(int[] columnGroup)
    {
        var columns = new TableColumn[columnGroup.Length + 1];
        columns[0] = DurationColumns[0];
        for (var index = 0; index < columnGroup.Length; index++)
            columns[index + 1] = DurationColumns[columnGroup[index]];

        return columns;
    }

    private static IReadOnlyList<string?>[] SelectCells(IReadOnlyList<IReadOnlyList<string?>> rows, int[] columnGroup)
    {
        var selected = new IReadOnlyList<string?>[rows.Count];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = new string?[columnGroup.Length + 1];
            cells[0] = rows[rowIndex][0];
            for (var index = 0; index < columnGroup.Length; index++)
                cells[index + 1] = rows[rowIndex][columnGroup[index]];

            selected[rowIndex] = cells;
        }

        return selected;
    }

    private static List<RenderedLine> TitledTableLines(string title, TableColumn[] columns, IReadOnlyList<IReadOnlyList<string?>> rows, int blockWidth, ColorMode colorMode)
    {
        var lines = new List<RenderedLine>();
        lines.Add(MarkupText.RenderFitted(TitleMarkup(title), blockWidth, colorMode));
        foreach (var line in TableWidget.Render(columns, rows, blockWidth, colorMode))
            lines.Add(PadRight(line, blockWidth));

        return lines;
    }

    private static List<RenderedLine> SideBySide(List<RenderedLine> left, List<RenderedLine> right, int leftWidth)
    {
        var lines = new List<RenderedLine>();
        var gap = new string(' ', TableGap);
        var rowCount = Math.Max(left.Count, right.Count);
        for (var index = 0; index < rowCount; index++)
        {
            if (index >= right.Count)
            {
                lines.Add(left[index]);
                continue;
            }

            RenderedLine leftLine;
            if (index < left.Count)
                leftLine = left[index];
            else
                leftLine = new RenderedLine(new string(' ', leftWidth), leftWidth);

            lines.Add(new RenderedLine(leftLine.Text + gap + right[index].Text, leftLine.Width + TableGap + right[index].Width));
        }

        return lines;
    }

    private static string TitleMarkup(string title)
    {
        return "[" + TitleStyle + "]" + title + "[/]";
    }
}
