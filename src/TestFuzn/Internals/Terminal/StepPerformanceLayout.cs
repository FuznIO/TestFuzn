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
        MetricsTableLayout.NumberColumn("Count"),
        MetricsTableLayout.NumberColumn("Per Second"),
        MetricsTableLayout.NumberColumn("Mean"),
        MetricsTableLayout.NumberColumn("Median"),
        MetricsTableLayout.NumberColumn("P75"),
        MetricsTableLayout.NumberColumn("P90"),
        MetricsTableLayout.NumberColumn("P95"),
        MetricsTableLayout.NumberColumn("P99"),
        MetricsTableLayout.NumberColumn("Min"),
        MetricsTableLayout.NumberColumn("Max"),
        MetricsTableLayout.NumberColumn("StdDev")
    };

    private static readonly int[][] ColumnTiers =
    {
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
        new[] { 0, 1, 2, 3, 4, 5, 8, 9 },
        new[] { 0, 1, 2, 3, 4, 8 },
        new[] { 0, 1, 2, 8 }
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

    public static string?[] Row(string name, bool isOk, int count, int perSecond, TimeSpan mean, TimeSpan median,
        TimeSpan percentile75, TimeSpan percentile90, TimeSpan percentile95, TimeSpan percentile99, TimeSpan min, TimeSpan max, TimeSpan standardDeviation)
    {
        var typeStyle = isOk ? LiveDashboardLayout.OkStyle : LiveDashboardLayout.FailedStyle;
        return new[]
        {
            MarkupParser.Escape(name),
            MetricsTableLayout.Styled(typeStyle, isOk ? OkType : FailedType),
            LiveDashboardLayout.FormatCount(count),
            LiveDashboardLayout.FormatCount(perSecond),
            mean.ToTestFuznDuration(),
            median.ToTestFuznDuration(),
            percentile75.ToTestFuznDuration(),
            percentile90.ToTestFuznDuration(),
            percentile95.ToTestFuznDuration(),
            percentile99.ToTestFuznDuration(),
            min.ToTestFuznDuration(),
            max.ToTestFuznDuration(),
            standardDeviation.ToTestFuznDuration()
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
