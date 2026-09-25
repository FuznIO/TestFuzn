using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class StepPerformanceLayout
{
    internal const string Header = "Step Performance";
    internal const string SummaryStepName = "All Steps (Summary)";
    internal const string OkType = "Ok";
    internal const string FailedType = "Failed";
    internal const string SubStepPrefix = "→ ";

    private const int StepColumnMaxWidth = 36;

    private static readonly TableColumn[] Columns =
    {
        new TableColumn(MetricsTableLayout.HeaderColumn("Step").Header) { MaxWidth = StepColumnMaxWidth },
        MetricsTableLayout.HeaderColumn("Type"),
        MetricsTableLayout.NumberColumn("Requests"),
        MetricsTableLayout.NumberColumn("RPS"),
        MetricsTableLayout.NumberColumn("Mean"),
        MetricsTableLayout.NumberColumn("Median"),
        MetricsTableLayout.NumberColumn("P75"),
        MetricsTableLayout.NumberColumn("P95"),
        MetricsTableLayout.NumberColumn("P99"),
        MetricsTableLayout.NumberColumn("Min"),
        MetricsTableLayout.NumberColumn("Max"),
        MetricsTableLayout.NumberColumn("StdDev")
    };

    private static readonly int[][] ColumnTiers =
    {
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 },
        new[] { 0, 1, 2, 3, 4, 5, 7, 8 },
        new[] { 0, 1, 2, 3, 4, 7 },
        new[] { 0, 1, 2, 7 }
    };

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<IReadOnlyList<string?>> rows, int innerWidth, ColorMode colorMode)
    {
        if (rows == null)
            throw new ArgumentNullException(nameof(rows), "Rows cannot be null.");

        for (var tier = 0; tier < ColumnTiers.Length; tier++)
        {
            var columns = SelectColumns(ColumnTiers[tier]);
            var tierRows = SelectCells(rows, ColumnTiers[tier]);
            var isNarrowestTier = tier == ColumnTiers.Length - 1;
            if (isNarrowestTier || TableWidget.MeasureNaturalWidth(columns, tierRows) <= innerWidth)
                return TableWidget.Render(columns, tierRows, innerWidth, colorMode);
        }

        return Array.Empty<RenderedLine>();
    }

    public static int MeasureNaturalWidth(IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        return TableWidget.MeasureNaturalWidth(Columns, rows);
    }

    public static int MeasureMinimumWidth(IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var narrowestTier = ColumnTiers[ColumnTiers.Length - 1];
        return TableWidget.MeasureNaturalWidth(SelectColumns(narrowestTier), SelectCells(rows, narrowestTier));
    }

    public static string?[] Row(string name, bool isOk, int requestCount, int requestsPerSecond, TimeSpan mean, TimeSpan median,
        TimeSpan percentile75, TimeSpan percentile95, TimeSpan percentile99, TimeSpan min, TimeSpan max, TimeSpan standardDeviation)
    {
        var typeStyle = isOk ? LiveDashboardLayout.OkStyle : LiveDashboardLayout.FailedStyle;
        return new[]
        {
            MarkupParser.Escape(name),
            MetricsTableLayout.Styled(typeStyle, isOk ? OkType : FailedType),
            LiveDashboardLayout.FormatCount(requestCount),
            LiveDashboardLayout.FormatCount(requestsPerSecond),
            mean.ToTestFuznResponseTime(),
            median.ToTestFuznResponseTime(),
            percentile75.ToTestFuznResponseTime(),
            percentile95.ToTestFuznResponseTime(),
            percentile99.ToTestFuznResponseTime(),
            min.ToTestFuznResponseTime(),
            max.ToTestFuznResponseTime(),
            standardDeviation.ToTestFuznResponseTime()
        };
    }

    private static TableColumn[] SelectColumns(int[] columnIndexes)
    {
        var selected = new TableColumn[columnIndexes.Length];
        for (var index = 0; index < columnIndexes.Length; index++)
            selected[index] = Columns[columnIndexes[index]];

        return selected;
    }

    private static IReadOnlyList<string?>[] SelectCells(IReadOnlyList<IReadOnlyList<string?>> rows, int[] columnIndexes)
    {
        var selected = new IReadOnlyList<string?>[rows.Count];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = new string?[columnIndexes.Length];
            for (var index = 0; index < columnIndexes.Length; index++)
                cells[index] = rows[rowIndex][columnIndexes[index]];

            selected[rowIndex] = cells;
        }

        return selected;
    }
}
