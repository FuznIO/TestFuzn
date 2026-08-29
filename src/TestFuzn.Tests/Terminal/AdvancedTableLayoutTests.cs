using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Terminal;
using CellAlignment = Fuzn.TestFuzn.ConsoleOutput.TextAlignment;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins <see cref="AdvancedTableLayout"/> with hand-derived goldens: the column widths sized
/// as the plain-text rendering sizes them (content plus four columns of air, a spanning cell's
/// width shared over its columns), spanning cells, divider rows, key/value cells with the key
/// left and the value right, cell alignment, a short row completed to the table's width, the
/// widest-column-first shrink with ellipsis truncation when the table does not fit, the
/// minimum-width floor, and styled cells in TrueColor.
/// </summary>
[TestClass]
public class AdvancedTableLayoutTests : Test
{
    /// <summary>Three columns: a spanning header row, a divider, a plain cell beside a spanning key/value cell, and a full-width row.</summary>
    private static AdvancedTable ThreeColumnTable()
    {
        var table = new AdvancedTable { ColumnCount = 3 };
        table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("Name", 2), new AdvancedTableCell("Value", 1) } });
        table.Rows.Add(new AdvancedTableRow { IsDivider = true });
        table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("a", 1), new KeyValueCell("Ok", "12 ms", 2) } });
        table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("wide text here", 3) } });
        return table;
    }

    [Test]
    public async Task Verify_table_golden_at_its_natural_width()
    {
        await Scenario()
            .Step("Every column is its widest content plus four, a spanning cell shares its width over its columns, and rows fill the table's width", context =>
            {
                // Natural widths: 5, 5, 5 from the full-width row shared over three columns (the
                // key/value cell's 8 over two gives 4, "Value" gives 5) — plus four each: 9, 9, 9,
                // with four borders: 31.
                var lines = AdvancedTableLayout.Render(ThreeColumnTable(), 40, ColorMode.None);

                AssertLines(
                    new[]
                    {
                        "╭" + new string('─', 29) + "╮",
                        "│ Name" + new string(' ', 14) + "│ Value   │",
                        "├" + new string('─', 29) + "┤",
                        "│ a       │ Ok          12 ms │",
                        "│ wide text here" + new string(' ', 14) + "│",
                        "╰" + new string('─', 29) + "╯"
                    },
                    lines,
                    31);

                foreach (var line in lines)
                    Assert.DoesNotContain(AnsiCodes.Escape, line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_table_shrinks_the_widest_columns_first_to_fit_the_width()
    {
        await Scenario()
            .Step("At width 20 the three equal columns shrink round-robin from the left to 5, 5 and 6; cells that no longer fit truncate with an ellipsis", context =>
            {
                var lines = AdvancedTableLayout.Render(ThreeColumnTable(), 20, ColorMode.None);

                AssertLines(
                    new[]
                    {
                        "╭" + new string('─', 18) + "╮",
                        "│ Name      │ Val… │",
                        "├" + new string('─', 18) + "┤",
                        "│ a   │ Ok   12 ms │",
                        "│ wide text here   │",
                        "╰" + new string('─', 18) + "╯"
                    },
                    lines,
                    20);
            })
            .Step("Below the minimum widths the table renders at its minimum — three columns of three — rather than degrading further", context =>
            {
                var lines = AdvancedTableLayout.Render(ThreeColumnTable(), 5, ColorMode.None);

                Assert.HasCount(6, lines);
                foreach (var line in lines)
                    Assert.AreEqual(13, line.Width);

                Assert.AreEqual("│ a │ Ok 1… │", lines[3].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_cell_alignment_key_value_overflow_and_short_rows()
    {
        await Scenario()
            .Step("Cells align left, center or right within their content width", context =>
            {
                var table = new AdvancedTable { ColumnCount = 1 };
                table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("ab", 1, CellAlignment.Left) } });
                table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("ab", 1, CellAlignment.Center) } });
                table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("ab", 1, CellAlignment.Right) } });

                var lines = AdvancedTableLayout.Render(table, 8, ColorMode.None);

                AssertLines(new[] { "╭──────╮", "│ ab   │", "│  ab  │", "│   ab │", "╰──────╯" }, lines, 8);
            })
            .Step("A key/value pair that cannot fit with a space between is joined by one and truncated", context =>
            {
                var table = new AdvancedTable { ColumnCount = 1 };
                table.Rows.Add(new AdvancedTableRow { Cells = { new KeyValueCell("Status", "Passed", 1) } });

                var lines = AdvancedTableLayout.Render(table, 10, ColorMode.None);

                AssertLines(new[] { "╭────────╮", "│ Statu… │", "╰────────╯" }, lines, 10);
            })
            .Step("A row whose cells span fewer columns than the table has is completed with an empty cell across the rest", context =>
            {
                var table = new AdvancedTable { ColumnCount = 2 };
                table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("x", 1) } });

                var lines = AdvancedTableLayout.Render(table, 40, ColorMode.None);

                AssertLines(new[] { "╭──────────╮", "│ x   │    │", "╰──────────╯" }, lines, 12);
            })
            .Step("A row spanning more columns than the table has, a column count below one and a null table are rejected", context =>
            {
                var overflowing = new AdvancedTable { ColumnCount = 1 };
                overflowing.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("x", 2) } });
                Assert.ThrowsExactly<ArgumentException>(() => AdvancedTableLayout.Render(overflowing, 40, ColorMode.None));

                Assert.ThrowsExactly<ArgumentException>(() => AdvancedTableLayout.Render(new AdvancedTable { ColumnCount = 0 }, 40, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => AdvancedTableLayout.Render(null!, 40, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_TrueColor_styles_markup_cells()
    {
        await Scenario()
            .Step("A markup cell renders styled inside its padding, the width measured on the text", context =>
            {
                var table = new AdvancedTable { ColumnCount = 1 };
                table.Rows.Add(new AdvancedTableRow { Cells = { new AdvancedTableCell("[green]Ok[/]", 1) } });

                var lines = AdvancedTableLayout.Render(table, 40, ColorMode.TrueColor);

                AssertLines(new[] { "╭──────╮", "│ \u001b[38;5;2mOk\u001b[0m   │", "╰──────╯" }, lines, 8);
            })
            .Run();
    }

    private static void AssertLines(IReadOnlyList<string> expectedLines, IReadOnlyList<RenderedLine> actualLines, int expectedWidth)
    {
        Assert.HasCount(expectedLines.Count, actualLines);

        for (var index = 0; index < expectedLines.Count; index++)
        {
            Assert.AreEqual(expectedLines[index], actualLines[index].Text, $"Line mismatch at row {index}");
            Assert.AreEqual(expectedWidth, actualLines[index].Width, $"Width mismatch at row {index}");
        }
    }
}
