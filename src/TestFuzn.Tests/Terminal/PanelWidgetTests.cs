using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class PanelWidgetTests : Test
{
    [Test]
    public async Task Verify_panel_golden_frames_at_fixed_widths()
    {
        await Scenario()
            .Step("Width 20 fits the header and pads content to the inner width", context =>
            {
                var lines = PanelWidget.Render("[bold]Checkout[/]", new[] { "Requests: 128", "[green]OK[/]" }, 20, ColorMode.TrueColor);

                AssertLines(
                    new[]
                    {
                        "╭─ \u001b[1mCheckout\u001b[0m ───────╮",
                        "│ " + "Requests: 128   " + " │",
                        "│ " + "\u001b[38;5;2mOK\u001b[0m              " + " │",
                        "╰──────────────────╯"
                    },
                    lines,
                    20);
            })
            .Step("Width 12 truncates the header with a style-closed ellipsis", context =>
            {
                var lines = PanelWidget.Render("[bold]Checkout[/]", new[] { "abc" }, 12, ColorMode.TrueColor);

                AssertLines(
                    new[]
                    {
                        "╭─ \u001b[1mChecko…\u001b[0m ╮",
                        "│ " + "abc     " + " │",
                        "╰──────────╯"
                    },
                    lines,
                    12);
            })
            .Step("Width 8 without a header renders pure plain text in color mode None", context =>
            {
                var lines = PanelWidget.Render(null, new[] { "hi" }, 8, ColorMode.None);

                AssertLines(
                    new[]
                    {
                        "╭──────╮",
                        "│ hi   │",
                        "╰──────╯"
                    },
                    lines,
                    8);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_panel_content_truncation_and_narrow_widths()
    {
        await Scenario()
            .Step("A content line wider than the inner width truncates with an ellipsis", context =>
            {
                var lines = PanelWidget.Render(null, new[] { "[red]abcdefgh[/]" }, 10, ColorMode.TrueColor);

                AssertLines(
                    new[]
                    {
                        "╭────────╮",
                        "│ \u001b[38;5;9mabcde…\u001b[0m │",
                        "╰────────╯"
                    },
                    lines,
                    10);
            })
            .Step("Below 6 columns the header is dropped and the top border renders plain", context =>
            {
                var lines = PanelWidget.Render("Hdr", new[] { "x" }, 5, ColorMode.None);

                AssertLines(new[] { "╭───╮", "│ x │", "╰───╯" }, lines, 5);
            })
            .Step("Width 4 leaves no content columns and width 3 renders nothing", context =>
            {
                AssertLines(new[] { "╭──╮", "│  │", "╰──╯" }, PanelWidget.Render(null, new[] { "x" }, 4, ColorMode.None), 4);
                Assert.IsEmpty(PanelWidget.Render(null, new[] { "x" }, 3, ColorMode.None));
            })
            .Step("Empty content renders just the borders and null content lines are rejected", context =>
            {
                AssertLines(new[] { "╭────╮", "╰────╯" }, PanelWidget.Render(null, Array.Empty<string>(), 6, ColorMode.None), 6);
                Assert.ThrowsExactly<ArgumentNullException>(() => PanelWidget.Render(null, (IReadOnlyList<string?>)null!, 10, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => PanelWidget.Render(null, (IReadOnlyList<RenderedLine>)null!, 10, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_panel_embeds_pre_rendered_widget_lines()
    {
        await Scenario()
            .Step("Widgets rendered at the inner width embed with aligned borders and closed styling", context =>
            {
                var content = new List<RenderedLine>();
                content.AddRange(ProgressBarWidget.Render(0.5, 16, ColorMode.TrueColor, barStyle: "green"));
                content.AddRange(SparklineWidget.Render(new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 16, ColorMode.TrueColor, SparklineGlyphSet.Blocks, "green"));

                var lines = PanelWidget.Render("[bold]Load[/]", content, 20, ColorMode.TrueColor);

                AssertLines(
                    new[]
                    {
                        "╭─ \u001b[1mLoad\u001b[0m ───────────╮",
                        "│ \u001b[38;5;2m██████\u001b[0m░░░░░  50% │",
                        "│ \u001b[38;5;2m        ▁▂▃▄▅▆▇█\u001b[0m │",
                        "╰──────────────────╯"
                    },
                    lines,
                    20);
            })
            .Step("A narrower rendered line pads right to the inner width by its known width", context =>
            {
                var tile = StatTileWidget.Render("Errors", "[red]3[/]", 10, ColorMode.TrueColor);

                var lines = PanelWidget.Render(null, tile, 20, ColorMode.TrueColor);

                AssertLines(
                    new[]
                    {
                        "╭──────────────────╮",
                        "│ \u001b[2mErrors\u001b[0m           │",
                        "│ \u001b[1;38;5;9m3\u001b[0m                │",
                        "╰──────────────────╯"
                    },
                    lines,
                    20);
            })
            .Step("A rendered line wider than the inner width fails loud", context =>
            {
                var overlong = ProgressBarWidget.Render(0.5, 17, ColorMode.TrueColor);

                Assert.ThrowsExactly<ArgumentException>(() => PanelWidget.Render(null, overlong, 20, ColorMode.TrueColor));
            })
            .Step("Empty pre-rendered content renders just the borders", context =>
            {
                AssertLines(new[] { "╭────╮", "╰────╯" }, PanelWidget.Render(null, Array.Empty<RenderedLine>(), 6, ColorMode.None), 6);
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
