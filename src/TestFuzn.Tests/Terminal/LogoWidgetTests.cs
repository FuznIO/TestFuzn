using System.Text;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class LogoWidgetTests : Test
{
    private const string BannerFirstRow = "████████╗███████╗███████╗████████╗███████╗██╗   ██╗███████╗███╗   ██╗";
    private const string BannerLastRow = "   ╚═╝   ╚══════╝╚══════╝   ╚═╝   ╚═╝      ╚═════╝ ╚══════╝╚═╝  ╚═══╝";

    [Test]
    public async Task Verify_logo_banner_golden_frames_per_color_mode()
    {
        await Scenario()
            .Step("Color mode None renders the plain art as a perfect 69 by 6 rectangle", context =>
            {
                var lines = LogoWidget.Render(LogoWidget.BannerWidth, LogoWidget.BannerHeight, ColorMode.None);

                Assert.HasCount(LogoWidget.BannerHeight, lines);
                Assert.AreEqual(BannerFirstRow, lines[0].Text);
                Assert.AreEqual(BannerLastRow, lines[5].Text);

                foreach (var line in lines)
                {
                    Assert.AreEqual(LogoWidget.BannerWidth, line.Width);
                    Assert.AreEqual(LogoWidget.BannerWidth, line.Text.Length);
                    Assert.DoesNotContain("\u001b", line.Text);
                }
            })
            .Step("TrueColor sweeps the gradient from deep orange to warm amber, skipping space runs", context =>
            {
                var plainLines = LogoWidget.Render(LogoWidget.BannerWidth, LogoWidget.BannerHeight, ColorMode.None);
                var lines = LogoWidget.Render(LogoWidget.BannerWidth, LogoWidget.BannerHeight, ColorMode.TrueColor);

                Assert.HasCount(LogoWidget.BannerHeight, lines);
                Assert.StartsWith(AnsiCodes.ForegroundTrueColor(255, 92, 0) + "█", lines[0].Text);
                Assert.EndsWith(AnsiCodes.ForegroundTrueColor(255, 207, 107) + "╗" + AnsiCodes.Reset, lines[0].Text);
                Assert.AreEqual(ExpectedGradientRow(BannerFirstRow), lines[0].Text);
                Assert.AreEqual(ExpectedGradientRow(BannerLastRow), lines[5].Text);

                for (var row = 0; row < lines.Count; row++)
                {
                    Assert.AreEqual(ExpectedGradientRow(plainLines[row].Text), lines[row].Text, $"Row mismatch at row {row}");
                    Assert.AreEqual(LogoWidget.BannerWidth, lines[row].Width);
                }
            })
            .Step("Colors16 renders the whole banner in the single warm accent", context =>
            {
                var plainLines = LogoWidget.Render(LogoWidget.BannerWidth, LogoWidget.BannerHeight, ColorMode.None);
                var lines = LogoWidget.Render(LogoWidget.BannerWidth, LogoWidget.BannerHeight, ColorMode.Colors16);

                Assert.HasCount(LogoWidget.BannerHeight, lines);
                Assert.AreEqual(AnsiCodes.Foreground(ConsoleColor.Yellow) + BannerFirstRow + AnsiCodes.Reset, lines[0].Text);

                for (var row = 0; row < lines.Count; row++)
                {
                    Assert.AreEqual(AnsiCodes.Foreground(ConsoleColor.Yellow) + plainLines[row].Text + AnsiCodes.Reset, lines[row].Text, $"Row mismatch at row {row}");
                    Assert.AreEqual(LogoWidget.BannerWidth, lines[row].Width);
                }
            })
            .Step("Monochrome renders the banner bold without colors", context =>
            {
                var plainLines = LogoWidget.Render(LogoWidget.BannerWidth, LogoWidget.BannerHeight, ColorMode.None);
                var lines = LogoWidget.Render(LogoWidget.BannerWidth, LogoWidget.BannerHeight, ColorMode.Monochrome);

                Assert.HasCount(LogoWidget.BannerHeight, lines);
                Assert.AreEqual(AnsiCodes.Bold + BannerFirstRow + AnsiCodes.Reset, lines[0].Text);

                for (var row = 0; row < lines.Count; row++)
                    Assert.AreEqual(AnsiCodes.Bold + plainLines[row].Text + AnsiCodes.Reset, lines[row].Text, $"Row mismatch at row {row}");
            })
            .Run();
    }

    [Test]
    public async Task Verify_logo_compact_golden_frames_per_color_mode()
    {
        await Scenario()
            .Step("TrueColor colors the lightning in the gradient's starting orange with the wordmark bold", context =>
            {
                var line = Assert.ContainsSingle(LogoWidget.Render(LogoWidget.CompactWidth, LogoWidget.CompactHeight, ColorMode.TrueColor));

                Assert.AreEqual(AnsiCodes.ForegroundTrueColor(255, 92, 0) + "⚡" + AnsiCodes.Reset + " " + AnsiCodes.Bold + "TestFuzn" + AnsiCodes.Reset, line.Text);
                Assert.AreEqual(LogoWidget.CompactWidth, line.Width);
            })
            .Step("Colors16 colors the lightning in the warm accent with the wordmark bold", context =>
            {
                var line = Assert.ContainsSingle(LogoWidget.Render(LogoWidget.CompactWidth, LogoWidget.CompactHeight, ColorMode.Colors16));

                Assert.AreEqual(AnsiCodes.Foreground(ConsoleColor.Yellow) + "⚡" + AnsiCodes.Reset + " " + AnsiCodes.Bold + "TestFuzn" + AnsiCodes.Reset, line.Text);
                Assert.AreEqual(LogoWidget.CompactWidth, line.Width);
            })
            .Step("Monochrome keeps the bold wordmark but drops the lightning's color", context =>
            {
                var line = Assert.ContainsSingle(LogoWidget.Render(LogoWidget.CompactWidth, LogoWidget.CompactHeight, ColorMode.Monochrome));

                Assert.AreEqual("⚡ " + AnsiCodes.Bold + "TestFuzn" + AnsiCodes.Reset, line.Text);
                Assert.AreEqual(LogoWidget.CompactWidth, line.Width);
            })
            .Step("Color mode None renders plain text with the lightning declared two columns wide", context =>
            {
                var line = Assert.ContainsSingle(LogoWidget.Render(LogoWidget.CompactWidth, LogoWidget.CompactHeight, ColorMode.None));

                Assert.AreEqual("⚡ TestFuzn", line.Text);
                Assert.DoesNotContain("\u001b", line.Text);

                // The declared width counts ⚡ (U+26A1) as two columns — one more than the
                // string's character count — so a flush-right placement can never overflow on
                // emoji-presentation terminals.
                Assert.AreEqual(11, line.Width);
                Assert.AreEqual(10, line.Text.Length);
            })
            .Run();
    }

    [Test]
    public async Task Verify_logo_variant_selection_boundaries()
    {
        await Scenario()
            .Step("The banner needs exactly 69 by 6 and one short in either dimension falls back to compact", context =>
            {
                Assert.AreEqual(LogoVariant.Banner, LogoWidget.SelectVariant(69, 6));
                Assert.AreEqual(LogoVariant.Banner, LogoWidget.SelectVariant(200, 50));
                Assert.AreEqual(LogoVariant.Compact, LogoWidget.SelectVariant(68, 6));
                Assert.AreEqual(LogoVariant.Compact, LogoWidget.SelectVariant(69, 5));
            })
            .Step("The compact wordmark needs exactly 11 by 1 and one short selects none", context =>
            {
                Assert.AreEqual(LogoVariant.Compact, LogoWidget.SelectVariant(11, 1));
                Assert.AreEqual(LogoVariant.None, LogoWidget.SelectVariant(10, 1));
                Assert.AreEqual(LogoVariant.None, LogoWidget.SelectVariant(11, 0));
                Assert.AreEqual(LogoVariant.None, LogoWidget.SelectVariant(0, 0));
                Assert.AreEqual(LogoVariant.None, LogoWidget.SelectVariant(-1, -1));
            })
            .Step("Render agrees with the selected variant at the boundaries", context =>
            {
                Assert.HasCount(LogoWidget.BannerHeight, LogoWidget.Render(69, 6, ColorMode.None));

                var compactLine = Assert.ContainsSingle(LogoWidget.Render(68, 6, ColorMode.None));
                Assert.AreEqual("⚡ TestFuzn", compactLine.Text);
                Assert.AreEqual("⚡ TestFuzn", Assert.ContainsSingle(LogoWidget.Render(69, 5, ColorMode.None)).Text);

                Assert.IsEmpty(LogoWidget.Render(10, 1, ColorMode.None));
                Assert.IsEmpty(LogoWidget.Render(11, 0, ColorMode.None));
            })
            .Run();
    }

    // Composes the expected TrueColor render of one plain art row: every glyph column takes the
    // foreground interpolated between the deep-orange and warm-amber gradient endpoints, space
    // runs stay bare, and the row closes with Reset.
    private static string ExpectedGradientRow(string artRow)
    {
        var expected = new StringBuilder();
        for (var column = 0; column < artRow.Length; column++)
        {
            if (artRow[column] == ' ')
            {
                expected.Append(' ');
                continue;
            }

            var position = (double)column / (artRow.Length - 1);
            expected.Append(AnsiCodes.ForegroundTrueColor(
                255,
                InterpolateChannel(92, 207, position),
                InterpolateChannel(0, 107, position)));
            expected.Append(artRow[column]);
        }

        return expected.Append(AnsiCodes.Reset).ToString();
    }

    private static byte InterpolateChannel(byte start, byte end, double position)
    {
        return (byte)Math.Round(start + ((end - start) * position), MidpointRounding.AwayFromZero);
    }
}
