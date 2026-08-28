using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class TableWidgetTests : Test
{
    [Test]
    public async Task Verify_table_golden_frame_with_auto_sized_columns()
    {
        await Scenario()
            .Step("Width 20 leaves the natural widths untouched and aligns per column", context =>
            {
                var columns = new[]
                {
                    new TableColumn("Name"),
                    new TableColumn("Count") { Alignment = TextAlignment.Right }
                };
                var rows = new[]
                {
                    new[] { "alpha", "10" },
                    new[] { "b", "5" }
                };

                var lines = TableWidget.Render(columns, rows, 20, ColorMode.None);

                AssertLines(
                    new[]
                    {
                        "Name " + "  " + "Count",
                        "alpha" + "  " + "   10",
                        "b    " + "  " + "    5"
                    },
                    lines);

                foreach (var line in lines)
                    Assert.AreEqual(12, line.Width);
            })
            .Step("The table keeps its natural width and emits no escape codes in mode None", context =>
            {
                var lines = TableWidget.Render(
                    new[] { new TableColumn("A"), new TableColumn("B") },
                    new[] { new[] { "x", "y" } },
                    40,
                    ColorMode.None);

                AssertLines(new[] { "A  B", "x  y" }, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_table_shrinks_the_widest_column_first()
    {
        await Scenario()
            .Step("Width 12 shrinks only the widest column until the table fits", context =>
            {
                var columns = new[] { new TableColumn("Col"), new TableColumn("Col2") };
                var rows = new[] { new[] { "aaaaaaaaaa", "bbbb" } };

                var lines = TableWidget.Render(columns, rows, 12, ColorMode.None);

                AssertLines(
                    new[]
                    {
                        "Col   " + "  " + "Col2",
                        "aaaaa…" + "  " + "bbbb"
                    },
                    lines);
            })
            .Step("Equally wide columns shrink round-robin from the left", context =>
            {
                var columns = new[] { new TableColumn("abcde"), new TableColumn("vwxyz") };

                var lines = TableWidget.Render(columns, Array.Empty<IReadOnlyList<string>>(), 9, ColorMode.None);

                var header = Assert.ContainsSingle(lines);
                Assert.AreEqual("ab…" + "  " + "vwx…", header.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_table_max_width_cap_and_colored_cells()
    {
        await Scenario()
            .Step("A column MaxWidth caps auto-sizing and cells truncate with an ellipsis", context =>
            {
                var columns = new[]
                {
                    new TableColumn("Name") { MaxWidth = 4 },
                    new TableColumn("St")
                };
                var rows = new[] { new[] { "abcdef", "[green]ok[/]" } };

                var lines = TableWidget.Render(columns, rows, 10, ColorMode.TrueColor);

                AssertLines(
                    new[]
                    {
                        "Name" + "  " + "St",
                        "abc…" + "  " + "\u001b[38;5;2mok\u001b[0m"
                    },
                    lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_table_degrades_at_narrow_widths_and_partial_rows()
    {
        await Scenario()
            .Step("When even one-column widths cannot fit, trailing columns drop entirely", context =>
            {
                var columns = new[] { new TableColumn("One"), new TableColumn("Two") };
                var rows = new[] { new[] { "aaaa", "bb" } };

                var lines = TableWidget.Render(columns, rows, 3, ColorMode.None);

                AssertLines(new[] { "One", "aa…" }, lines);
            })
            .Step("Missing cells render empty and extra cells are ignored", context =>
            {
                var columns = new[] { new TableColumn("A"), new TableColumn("B") };
                var rows = new IReadOnlyList<string>[] { new[] { "x" }, new[] { "y", "z", "ignored" } };

                var lines = TableWidget.Render(columns, rows, 10, ColorMode.None);

                AssertLines(new[] { "A  B", "x   ", "y  z" }, lines);
            })
            .Step("No columns or a width below 1 renders nothing", context =>
            {
                Assert.IsEmpty(TableWidget.Render(Array.Empty<TableColumn>(), Array.Empty<IReadOnlyList<string>>(), 20, ColorMode.None));
                Assert.IsEmpty(TableWidget.Render(new[] { new TableColumn("A") }, Array.Empty<IReadOnlyList<string>>(), 0, ColorMode.None));
            })
            .Step("Null columns, rows, or a null row are rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => TableWidget.Render(null!, Array.Empty<IReadOnlyList<string>>(), 10, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => TableWidget.Render(new[] { new TableColumn("A") }, null!, 10, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => TableWidget.Render(new[] { new TableColumn("A") }, new IReadOnlyList<string>[] { null! }, 10, ColorMode.None));
            })
            .Run();
    }

    private static void AssertLines(IReadOnlyList<string> expectedLines, IReadOnlyList<RenderedLine> actualLines)
    {
        Assert.HasCount(expectedLines.Count, actualLines);

        for (var index = 0; index < expectedLines.Count; index++)
            Assert.AreEqual(expectedLines[index], actualLines[index].Text, $"Line mismatch at row {index}");
    }
}
