using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders a borderless table: a header row followed by one line per data row, columns
/// separated by two spaces. Headers and cells are markup text, never pre-rendered widget
/// output — a <see cref="RenderedLine"/>'s text fed in as a cell would have its SGR escapes
/// re-measured as content (see <see cref="RenderedLine"/>); compose widgets inside a panel's
/// pre-rendered overload instead. Styling a header (or an
/// underline separator row) is the caller's choice via markup. Column widths auto-size to the
/// widest of the header and the cells, measured on stripped markup and capped by each column's
/// <see cref="TableColumn.MaxWidth"/>; the table keeps its natural width and never stretches to
/// fill the given width. When the natural widths do not fit, the widest column shrinks first —
/// ties broken leftmost, one column at a time, which round-robins equally wide columns — until
/// the table fits or every column is at the minimum width of one; if even that does not fit,
/// trailing columns drop entirely. Headers and cells truncate with an ellipsis and pad to
/// their column width per the column's alignment, so every emitted line has the same display
/// width, never more than the given width. Missing cells in a short row render empty and extra
/// cells are ignored; a width below 1 or an empty column list renders nothing. Stateless and
/// thread-safe.
/// </summary>
internal static class TableWidget
{
    private const string ColumnSeparator = "  ";

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string?>> rows, int width, ColorMode colorMode)
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

    // Auto-sizes and shrinks columns per the policy in the class summary, returning the widths
    // of the visible columns; trailing columns that cannot fit are not in the list.
    private static List<int> LayoutColumnWidths(IReadOnlyList<TableColumn> columns, IReadOnlyList<IReadOnlyList<string?>> rows, int width)
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

        for (var visibleCount = columns.Count; visibleCount >= 1; visibleCount--)
        {
            var columnWidths = TryLayout(naturalWidths, visibleCount, width);
            if (columnWidths != null)
                return columnWidths;
        }

        // Unreachable for width >= 1: a single column always shrinks down to fit.
        return new List<int>();
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
