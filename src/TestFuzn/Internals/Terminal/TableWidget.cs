using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class TableWidget
{
    private const string ColumnSeparator = "  ";

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string?>> rows, int width, ColorMode colorMode)
    {
        ValidateArguments(columns, rows);

        if (width < 1 || columns.Count == 0)
            return Array.Empty<RenderedLine>();

        var columnWidths = LayoutColumnWidths(columns, rows, width);

        var lines = new List<RenderedLine>(rows.Count + 1);

        var headerCells = new string?[columnWidths.Count];
        for (var index = 0; index < columnWidths.Count; index++)
            headerCells[index] = columns[index].Header;

        lines.Add(RenderRow(headerCells, columns, columnWidths, colorMode));

        foreach (var row in rows)
        {
            var cells = new string?[columnWidths.Count];
            for (var index = 0; index < columnWidths.Count; index++)
                cells[index] = index < row.Count ? row[index] : string.Empty;

            lines.Add(RenderRow(cells, columns, columnWidths, colorMode));
        }

        return lines;
    }

    public static int MeasureNaturalWidth(IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        ValidateArguments(columns, rows);

        if (columns.Count == 0)
            return 0;

        var totalWidth = (columns.Count - 1) * ColumnSeparator.Length;
        foreach (var naturalWidth in NaturalWidths(columns, rows))
            totalWidth += naturalWidth;

        return totalWidth;
    }

    private static void ValidateArguments(IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns), "Columns cannot be null.");
        if (rows == null)
            throw new ArgumentNullException(nameof(rows), "Rows cannot be null.");
        foreach (var row in rows)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(rows), "Rows cannot contain a null row.");
        }
    }

    private static RenderedLine RenderRow(IReadOnlyList<string?> cells, IReadOnlyList<TableColumn> columns, IReadOnlyList<int> columnWidths, ColorMode colorMode)
    {
        var row = new StringBuilder();
        var rowWidth = 0;
        for (var index = 0; index < columnWidths.Count; index++)
        {
            if (index > 0)
            {
                row.Append(ColumnSeparator);
                rowWidth += ColumnSeparator.Length;
            }

            var cell = MarkupText.RenderFitted(cells[index], columnWidths[index], colorMode, columns[index].Alignment);
            row.Append(cell.Text);
            rowWidth += cell.Width;
        }

        return new RenderedLine(row.ToString(), rowWidth);
    }

    private static List<int> LayoutColumnWidths(IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string?>> rows, int width)
    {
        var naturalWidths = NaturalWidths(columns, rows);

        for (var visibleCount = columns.Count; visibleCount >= 1; visibleCount--)
        {
            var columnWidths = TryLayout(naturalWidths, visibleCount, width);
            if (columnWidths != null)
                return columnWidths;
        }

        return new List<int>();
    }

    private static int[] NaturalWidths(IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var naturalWidths = new int[columns.Count];
        for (var index = 0; index < columns.Count; index++)
        {
            var natural = MarkupText.Measure(columns[index].Header);
            foreach (var row in rows)
            {
                if (index < row.Count)
                    natural = Math.Max(natural, MarkupText.Measure(row[index]));
            }

            var maxWidth = columns[index].MaxWidth;
            if (maxWidth != null && natural > maxWidth.Value)
                natural = maxWidth.Value;
            if (natural < 1)
                natural = 1;

            naturalWidths[index] = natural;
        }

        return naturalWidths;
    }

    private static List<int>? TryLayout(int[] naturalWidths, int visibleCount, int width)
    {
        var columnWidths = new List<int>(visibleCount);
        var totalWidth = (visibleCount - 1) * ColumnSeparator.Length;
        for (var index = 0; index < visibleCount; index++)
        {
            columnWidths.Add(naturalWidths[index]);
            totalWidth += naturalWidths[index];
        }

        while (totalWidth > width)
        {
            var widestIndex = 0;
            for (var index = 1; index < visibleCount; index++)
            {
                if (columnWidths[index] > columnWidths[widestIndex])
                    widestIndex = index;
            }

            if (columnWidths[widestIndex] <= 1)
                return null;

            columnWidths[widestIndex]--;
            totalWidth--;
        }

        return columnWidths;
    }
}
