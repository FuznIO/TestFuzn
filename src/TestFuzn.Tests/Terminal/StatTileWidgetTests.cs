using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class StatTileWidgetTests : Test
{
    [Test]
    public async Task Verify_stat_tile_golden_frames_at_fixed_widths()
    {
        await Scenario()
            .Step("Width 12 renders the dimmed label above the bold value", context =>
            {
                var lines = StatTileWidget.Render("Requests", "12 345", 12, ColorMode.TrueColor);

                AssertLines(new[] { "\u001b[2mRequests\u001b[0m", "\u001b[1m12 345\u001b[0m" }, lines);
                Assert.AreEqual(8, lines[0].Width);
                Assert.AreEqual(6, lines[1].Width);
            })
            .Step("Caller styling on the value composes with the bold emphasis", context =>
            {
                AssertLines(
                    new[] { "\u001b[2mErrors\u001b[0m", "\u001b[1;38;5;9m3\u001b[0m" },
                    StatTileWidget.Render("Errors", "[red]3[/]", 10, ColorMode.TrueColor));
                AssertLines(
                    new[] { "\u001b[2mErrors\u001b[0m", "\u001b[1;91m3\u001b[0m" },
                    StatTileWidget.Render("Errors", "[red]3[/]", 10, ColorMode.Colors16));
            })
            .Step("Width 6 truncates both lines with a style-closed ellipsis", context =>
            {
                AssertLines(
                    new[] { "\u001b[2mReque…\u001b[0m", "\u001b[1m12345…\u001b[0m" },
                    StatTileWidget.Render("Requests", "1234567", 6, ColorMode.TrueColor));
            })
            .Run();
    }

    [Test]
    public async Task Verify_stat_tile_color_modes_and_edges()
    {
        await Scenario()
            .Step("Color mode None renders pure plain text", context =>
            {
                var lines = StatTileWidget.Render("Requests", "[green]12[/]", 8, ColorMode.None);

                AssertLines(new[] { "Requests", "12" }, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("Monochrome keeps the dim and bold emphasis but drops colors", context =>
            {
                AssertLines(
                    new[] { "\u001b[2mRequests\u001b[0m", "\u001b[1m12\u001b[0m" },
                    StatTileWidget.Render("Requests", "[green]12[/]", 8, ColorMode.Monochrome));
            })
            .Step("A width below 1 renders nothing", context =>
            {
                Assert.IsEmpty(StatTileWidget.Render("Requests", "12", 0, ColorMode.None));
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
