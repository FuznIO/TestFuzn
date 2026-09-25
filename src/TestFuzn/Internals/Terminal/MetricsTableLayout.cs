using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class MetricsTableLayout
{
    internal const string ResponseTimesTitle = "Response Times";

    private const string TitleStyle = "bold";
    private const int TableGap = 4;

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    internal static readonly TableColumn[] RequestsColumns =
    {
        HeaderColumn("Metric"),
        NumberColumn("Count"),
        NumberColumn("RPS")
    };

    internal static readonly TableColumn[] ResponseTimeColumns =
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

    private static readonly int[][][] ResponseTimeColumnTiers =
    {
        new[] { new[] { 1, 2, 3, 4, 5, 6, 7, 8 } },
        new[] { new[] { 1, 2, 3, 4 }, new[] { 5, 6, 7, 8 } },
        new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 }, new[] { 7, 8 } }
    };

    public static IReadOnlyList<RenderedLine> Render(string requestsTitle, IReadOnlyList<IReadOnlyList<string?>> requestsRows, IReadOnlyList<IReadOnlyList<string?>> responseTimeRows, int innerWidth, ColorMode colorMode, bool allowOverflow = true)
    {
        var requestsWidth = BlockWidth(requestsTitle, RequestsColumns, requestsRows);
        var responseTimesWidth = BlockWidth(ResponseTimesTitle, ResponseTimeColumns, responseTimeRows);
        if (!allowOverflow && requestsWidth > innerWidth)
            requestsWidth = innerWidth;

        if (requestsWidth + TableGap + responseTimesWidth <= innerWidth)
        {
            var left = TitledTableLines(requestsTitle, RequestsColumns, requestsRows, requestsWidth, colorMode);
            var right = TitledTableLines(ResponseTimesTitle, ResponseTimeColumns, responseTimeRows, responseTimesWidth, colorMode);
            return SideBySide(left, right, requestsWidth);
        }

        var lines = new List<RenderedLine>();
        lines.AddRange(TitledTableLines(requestsTitle, RequestsColumns, requestsRows, requestsWidth, colorMode));
        lines.Add(BlankLine);
        lines.Add(MarkupText.RenderTruncated(TitleMarkup(ResponseTimesTitle), innerWidth, colorMode));
        foreach (var columnGroup in SelectResponseTimeTier(responseTimeRows, innerWidth))
        {
            var columns = SelectColumns(columnGroup);
            var rows = SelectCells(responseTimeRows, columnGroup);
            var tableWidth = innerWidth;
            if (allowOverflow)
                tableWidth = Math.Max(innerWidth, TableWidget.MeasureNaturalWidth(columns, rows));

            lines.AddRange(TableWidget.Render(columns, rows, tableWidth, colorMode));
        }

        return lines;
    }

    public static int MeasureMinimumWidth(string requestsTitle, IReadOnlyList<IReadOnlyList<string?>> requestsRows, IReadOnlyList<IReadOnlyList<string?>> responseTimeRows)
    {
        var width = BlockWidth(requestsTitle, RequestsColumns, requestsRows);
        width = Math.Max(width, MarkupText.Measure(TitleMarkup(ResponseTimesTitle)));
        return Math.Max(width, TierWidth(ResponseTimeColumnTiers[ResponseTimeColumnTiers.Length - 1], responseTimeRows));
    }

    public static List<IReadOnlyList<string?>> RequestsRows(int requestCount, int okCount, int okRequestsPerSecond, int failedCount, int failedRequestsPerSecond, int? skippedCount)
    {
        var rows = new List<IReadOnlyList<string?>>();
        rows.Add(new[] { "Total", LiveDashboardLayout.FormatCount(requestCount), string.Empty });
        rows.Add(new[] { Styled(LiveDashboardLayout.OkStyle, "OK"), LiveDashboardLayout.FormatCount(okCount), LiveDashboardLayout.FormatCount(okRequestsPerSecond) });
        rows.Add(new[] { Styled(LiveDashboardLayout.FailedStyle, "Failed"), LiveDashboardLayout.FormatCount(failedCount), LiveDashboardLayout.FormatCount(failedRequestsPerSecond) });
        if (skippedCount != null)
            rows.Add(new[] { Styled(LiveDashboardLayout.SecondaryStyle, "Skipped"), LiveDashboardLayout.FormatCount(skippedCount.Value), string.Empty });

        return rows;
    }

    public static string?[] ResponseTimeRow(string label, string style, TimeSpan min, TimeSpan mean, TimeSpan max, TimeSpan standardDeviation, TimeSpan median, TimeSpan percentile75, TimeSpan percentile95, TimeSpan percentile99)
    {
        return new[]
        {
            Styled(style, label),
            Styled(style, min.ToTestFuznResponseTime()),
            Styled(style, mean.ToTestFuznResponseTime()),
            Styled(style, max.ToTestFuznResponseTime()),
            Styled(style, standardDeviation.ToTestFuznResponseTime()),
            Styled(style, median.ToTestFuznResponseTime()),
            Styled(style, percentile75.ToTestFuznResponseTime()),
            Styled(style, percentile95.ToTestFuznResponseTime()),
            Styled(style, percentile99.ToTestFuznResponseTime())
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

    private static int[][] SelectResponseTimeTier(IReadOnlyList<IReadOnlyList<string?>> responseTimeRows, int innerWidth)
    {
        foreach (var tier in ResponseTimeColumnTiers)
        {
            if (TierWidth(tier, responseTimeRows) <= innerWidth)
                return tier;
        }

        return ResponseTimeColumnTiers[ResponseTimeColumnTiers.Length - 1];
    }

    private static int TierWidth(int[][] tier, IReadOnlyList<IReadOnlyList<string?>> responseTimeRows)
    {
        var width = 0;
        foreach (var columnGroup in tier)
            width = Math.Max(width, TableWidget.MeasureNaturalWidth(SelectColumns(columnGroup), SelectCells(responseTimeRows, columnGroup)));

        return width;
    }

    private static TableColumn[] SelectColumns(int[] columnGroup)
    {
        var columns = new TableColumn[columnGroup.Length + 1];
        columns[0] = ResponseTimeColumns[0];
        for (var index = 0; index < columnGroup.Length; index++)
            columns[index + 1] = ResponseTimeColumns[columnGroup[index]];

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
