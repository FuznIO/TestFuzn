using System.Text;
using Fuzn.TestFuzn.ConsoleOutput;
using CellAlignment = Fuzn.TestFuzn.ConsoleOutput.TextAlignment;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders an <see cref="AdvancedTable"/> — the console writer's row-oriented result table, with
/// cells spanning columns, divider rows and key/value cells — as a bordered box on the terminal
/// engine: <c>╭──╮</c> on top, one <c>│ cell │ cell │</c> line per row, <c>├──┤</c> for a divider
/// row, <c>╰──╯</c> at the bottom. Column widths auto-size from the content: the widest
/// single-column cell of each column, a spanning cell's width shared equally over the columns
/// it spans — a key/value cell measuring its key, one space and its value — plus
/// <see cref="ColumnPadding"/> columns of air per column, the air the plain-text rendering adds
/// too (that rendering measures a key/value cell one column wider; the two layouts are
/// independent). A spanning cell's width is its columns' widths plus the borders between them.
/// When that does
/// not fit the given width the widest column shrinks first, one column at a time, down to
/// <see cref="MinimumColumnWidth"/> — cells then truncate with an ellipsis — so the table is
/// never wider than the given width unless even the minimum widths exceed it, in which case it
/// renders at the minimum (only a terminal too narrow to read the table at all gets there, and
/// the standalone adapter never lays out below its own minimum width). Cells are markup —
/// rendered through <see cref="MarkupText"/>, so brackets need escaping and control characters
/// sanitize to spaces — and are left, centered or right aligned per the cell; a key/value cell
/// puts the key at the left edge and the value at the right, or the two joined by a space and
/// truncated when they do not fit. A row whose cells span fewer than the table's columns is
/// completed with an empty cell across the rest, so every line is the table's width; a row
/// spanning more is a caller bug and throws. Stateless and thread-safe.
/// </summary>
internal static class AdvancedTableLayout
{
    /// <summary>Columns of air added to every column's content width, as the plain-text rendering adds.</summary>
    public const int ColumnPadding = 4;

    /// <summary>The narrowest a column shrinks to: one content column between its two padding spaces.</summary>
    public const int MinimumColumnWidth = 3;

    // A cell renders as one space, its content, one space.
    private const int CellPadding = 2;

    public static IReadOnlyList<RenderedLine> Render(AdvancedTable table, int width, ColorMode colorMode)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table), "Table cannot be null.");
        if (table.ColumnCount < 1)
            throw new ArgumentException("The table must have at least one column.", nameof(table));
        if (table.Rows == null)
            throw new ArgumentException("The table's rows cannot be null.", nameof(table));

        var columnWidths = LayoutColumnWidths(table, width);
        var tableWidth = TotalWidth(columnWidths);

        var lines = new List<RenderedLine>(table.Rows.Count + 2);
        lines.Add(new RenderedLine("╭" + new string('─', tableWidth - 2) + "╮", tableWidth));

        foreach (var row in table.Rows)
        {
            if (row == null)
                throw new ArgumentException("The table's rows cannot contain null.", nameof(table));

            if (row.IsDivider)
            {
                lines.Add(new RenderedLine("├" + new string('─', tableWidth - 2) + "┤", tableWidth));
                continue;
            }

            lines.Add(RenderRow(row, columnWidths, tableWidth, colorMode));
        }

        lines.Add(new RenderedLine("╰" + new string('─', tableWidth - 2) + "╯", tableWidth));
        return lines;
    }

    private static RenderedLine RenderRow(AdvancedTableRow row, int[] columnWidths, int tableWidth, ColorMode colorMode)
    {
        var line = new StringBuilder("│");
        var column = 0;
        foreach (var cell in CellsOf(row))
        {
            var span = SpanOf(cell);
            if (column + span > columnWidths.Length)
                throw new ArgumentException("A row spans more columns than the table has.", nameof(row));

            line.Append(RenderCell(cell, SpanWidth(columnWidths, column, span), colorMode).Text).Append('│');
            column += span;
        }

        // The columns the row's cells leave uncovered render as one empty cell across them.
        if (column < columnWidths.Length)
            line.Append(new string(' ', SpanWidth(columnWidths, column, columnWidths.Length - column))).Append('│');

        return new RenderedLine(line.ToString(), tableWidth);
    }

    // The width of a cell spanning the given columns: their widths plus the borders between them.
    private static int SpanWidth(int[] columnWidths, int column, int span)
    {
        var spanWidth = span - 1;
        for (var index = column; index < column + span; index++)
            spanWidth += columnWidths[index];

        return spanWidth;
    }

    private static RenderedLine RenderCell(IAdvancedTableCell cell, int cellWidth, ColorMode colorMode)
    {
        var contentWidth = cellWidth - CellPadding;

        RenderedLine content;
        if (cell is KeyValueCell keyValueCell)
            content = RenderKeyValue(keyValueCell, contentWidth, colorMode);
        else if (cell is AdvancedTableCell textCell)
            content = RenderText(textCell.Text, textCell.Alignment, contentWidth, colorMode);
        else
            throw new NotSupportedException("Cannot render a " + cell.GetType().Name + " cell.");

        return new RenderedLine(" " + content.Text + " ", cellWidth);
    }

    // The key at the left edge and the value at the right; when the two do not fit with a space
    // between, they are joined by one and truncated as a left-aligned text.
    private static RenderedLine RenderKeyValue(KeyValueCell cell, int contentWidth, ColorMode colorMode)
    {
        var keyWidth = MarkupText.Measure(cell.Key);
        var valueWidth = MarkupText.Measure(cell.Value);
        if (keyWidth + 1 + valueWidth > contentWidth)
            return MarkupText.RenderFitted(cell.Key + " " + cell.Value, contentWidth, colorMode);

        var key = MarkupText.RenderTruncated(cell.Key, keyWidth, colorMode);
        var value = MarkupText.RenderTruncated(cell.Value, valueWidth, colorMode);
        return new RenderedLine(key.Text + new string(' ', contentWidth - keyWidth - valueWidth) + value.Text, contentWidth);
    }

    private static RenderedLine RenderText(string? text, CellAlignment alignment, int contentWidth, ColorMode colorMode)
    {
        if (alignment == CellAlignment.Right)
            return MarkupText.RenderFitted(text, contentWidth, colorMode, TextAlignment.Right);

        if (alignment == CellAlignment.Center)
        {
            var textWidth = MarkupText.Measure(text);
            if (textWidth < contentWidth)
            {
                var leftPadding = (contentWidth - textWidth) / 2;
                var rendered = MarkupText.RenderTruncated(text, textWidth, colorMode);
                return new RenderedLine(new string(' ', leftPadding) + rendered.Text + new string(' ', contentWidth - textWidth - leftPadding), contentWidth);
            }
        }

        return MarkupText.RenderFitted(text, contentWidth, colorMode);
    }

    // Natural widths as the plain-text rendering sizes them, then the widest column shrinks —
    // ties broken leftmost — until the table fits or every column is at the minimum.
    private static int[] LayoutColumnWidths(AdvancedTable table, int width)
    {
        var columnWidths = new int[table.ColumnCount];
        foreach (var row in table.Rows)
        {
            if (row == null || row.IsDivider)
                continue;

            var column = 0;
            foreach (var cell in CellsOf(row))
            {
                var span = SpanOf(cell);
                if (column + span > columnWidths.Length)
                    throw new ArgumentException("A row spans more columns than the table has.", nameof(table));

                var contentWidth = ContentWidthOf(cell);
                var widthPerColumn = (contentWidth + span - 1) / span;
                for (var index = column; index < column + span; index++)
                {
                    if (widthPerColumn > columnWidths[index])
                        columnWidths[index] = widthPerColumn;
                }

                column += span;
            }
        }

        for (var index = 0; index < columnWidths.Length; index++)
            columnWidths[index] += ColumnPadding;

        while (TotalWidth(columnWidths) > width)
        {
            var widestIndex = 0;
            for (var index = 1; index < columnWidths.Length; index++)
            {
                if (columnWidths[index] > columnWidths[widestIndex])
                    widestIndex = index;
            }

            if (columnWidths[widestIndex] <= MinimumColumnWidth)
                break;

            columnWidths[widestIndex]--;
        }

        return columnWidths;
    }

    // The columns plus a border before each and after the last.
    private static int TotalWidth(int[] columnWidths)
    {
        var totalWidth = columnWidths.Length + 1;
        foreach (var columnWidth in columnWidths)
            totalWidth += columnWidth;

        return totalWidth;
    }

    private static int ContentWidthOf(IAdvancedTableCell cell)
    {
        if (cell is KeyValueCell keyValueCell)
            return MarkupText.Measure(keyValueCell.Key) + 1 + MarkupText.Measure(keyValueCell.Value);

        if (cell is AdvancedTableCell textCell)
            return MarkupText.Measure(textCell.Text);

        throw new NotSupportedException("Cannot measure a " + cell.GetType().Name + " cell.");
    }

    private static int SpanOf(IAdvancedTableCell cell)
    {
        if (cell.ColSpan < 1)
            throw new ArgumentException("A cell must span at least one column.", nameof(cell));

        return cell.ColSpan;
    }

    private static IEnumerable<IAdvancedTableCell> CellsOf(AdvancedTableRow row)
    {
        if (row.Cells == null)
            return Array.Empty<IAdvancedTableCell>();

        foreach (var cell in row.Cells)
        {
            if (cell == null)
                throw new ArgumentException("A row's cells cannot contain null.", nameof(row));
        }

        return row.Cells;
    }
}
