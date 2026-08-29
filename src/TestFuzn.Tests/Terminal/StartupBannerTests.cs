using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Golden lines for the startup banner in every color mode — the compact logo, the title line
/// (label in the warm accent, subject bold) and the dimmed detail line, each expected string
/// hand-written with the exact SGR sequences — plus the plain-text contract in
/// <see cref="ColorMode.None"/> (the same three lines, zero escape bytes, the ⚡ kept), escaping
/// of a markup-bearing name, sanitization of control characters, a very long name written whole
/// (the banner never truncates), and the one-write-per-line contract of <see cref="StartupBanner.Write"/>.
/// </summary>
[TestClass]
public class StartupBannerTests : Test
{
    private const string Label = "Running test:";
    private const string Subject = "Fuzn.Shop.Tests.CheckoutTests.Verify_checkout";
    private const string Detail = "Assembly: Fuzn.Shop.Tests · Target environment: staging";

    // The dashboard's warm accent (#ff9d3d) as the label's foreground: 24-bit in TrueColor,
    // downgraded to bright yellow (SGR 93) in 16-color mode — the logo's own accent there.
    private const string TrueColorLabelStart = AnsiCodes.Csi + "1;38;2;255;157;61m";
    private const string Colors16LabelStart = AnsiCodes.Csi + "1;93m";

    [Test]
    public async Task Verify_banner_golden_lines_per_color_mode()
    {
        await Scenario()
            .Step("TrueColor: the gradient-orange lightning with the bold wordmark, the label in the warm accent, the subject bold, the detail dimmed", context =>
            {
                var lines = StartupBanner.Render(Label, Subject, Detail, ColorMode.TrueColor);

                Assert.HasCount(StartupBanner.Height, lines);
                Assert.AreEqual(AnsiCodes.ForegroundTrueColor(255, 92, 0) + "⚡" + AnsiCodes.Reset + " " + AnsiCodes.Bold + "TestFuzn" + AnsiCodes.Reset, lines[0].Text);
                Assert.AreEqual(TrueColorLabelStart + Label + AnsiCodes.Reset + " " + AnsiCodes.Bold + Subject + AnsiCodes.Reset, lines[1].Text);
                Assert.AreEqual(AnsiCodes.Dim + Detail + AnsiCodes.Reset, lines[2].Text);

                Assert.AreEqual(LogoWidget.CompactWidth, lines[0].Width);
                Assert.AreEqual(Label.Length + 1 + Subject.Length, lines[1].Width);
                Assert.AreEqual(Detail.Length, lines[2].Width);
            })
            .Step("Colors16: the lightning and the label in bright yellow, the rest as in TrueColor", context =>
            {
                var lines = StartupBanner.Render(Label, Subject, Detail, ColorMode.Colors16);

                Assert.HasCount(StartupBanner.Height, lines);
                Assert.AreEqual(AnsiCodes.Foreground(ConsoleColor.Yellow) + "⚡" + AnsiCodes.Reset + " " + AnsiCodes.Bold + "TestFuzn" + AnsiCodes.Reset, lines[0].Text);
                Assert.AreEqual(Colors16LabelStart + Label + AnsiCodes.Reset + " " + AnsiCodes.Bold + Subject + AnsiCodes.Reset, lines[1].Text);
                Assert.AreEqual(AnsiCodes.Dim + Detail + AnsiCodes.Reset, lines[2].Text);
            })
            .Step("Monochrome keeps bold and dim but drops every color", context =>
            {
                var lines = StartupBanner.Render(Label, Subject, Detail, ColorMode.Monochrome);

                Assert.HasCount(StartupBanner.Height, lines);
                Assert.AreEqual("⚡ " + AnsiCodes.Bold + "TestFuzn" + AnsiCodes.Reset, lines[0].Text);
                Assert.AreEqual(AnsiCodes.Bold + Label + AnsiCodes.Reset + " " + AnsiCodes.Bold + Subject + AnsiCodes.Reset, lines[1].Text);
                Assert.AreEqual(AnsiCodes.Dim + Detail + AnsiCodes.Reset, lines[2].Text);
            })
            .Step("None renders the same three lines as plain text with zero escape bytes, the lightning kept", context =>
            {
                var lines = StartupBanner.Render(Label, Subject, Detail, ColorMode.None);

                Assert.HasCount(StartupBanner.Height, lines);
                Assert.AreEqual("⚡ TestFuzn", lines[0].Text);
                Assert.AreEqual("Running test: Fuzn.Shop.Tests.CheckoutTests.Verify_checkout", lines[1].Text);
                Assert.AreEqual("Assembly: Fuzn.Shop.Tests · Target environment: staging", lines[2].Text);

                foreach (var line in lines)
                    Assert.DoesNotContain(AnsiCodes.Escape, line.Text);

                Assert.AreEqual(LogoWidget.CompactWidth, lines[0].Width);
                Assert.AreEqual(lines[1].Text.Length, lines[1].Width);
                Assert.AreEqual(lines[2].Text.Length, lines[2].Width);
            })
            .Run();
    }

    [Test]
    public async Task Verify_names_render_literally_whole_and_sanitized()
    {
        await Scenario()
            .Step("Brackets in the subject and the detail render literally, not as markup", context =>
            {
                var lines = StartupBanner.Render(Label, "Tests.Weird[bold]name[/]", "Assembly: [[Shop]]", ColorMode.TrueColor);

                Assert.AreEqual(TrueColorLabelStart + Label + AnsiCodes.Reset + " " + AnsiCodes.Bold + "Tests.Weird[bold]name[/]" + AnsiCodes.Reset, lines[1].Text);
                Assert.AreEqual(Label.Length + 1 + "Tests.Weird[bold]name[/]".Length, lines[1].Width);
                Assert.AreEqual(AnsiCodes.Dim + "Assembly: [[Shop]]" + AnsiCodes.Reset, lines[2].Text);

                var plain = StartupBanner.Render(Label, "Tests.Weird[bold]name[/]", "Assembly: [[Shop]]", ColorMode.None);
                Assert.AreEqual("Running test: Tests.Weird[bold]name[/]", plain[1].Text);
                Assert.AreEqual("Assembly: [[Shop]]", plain[2].Text);
            })
            .Step("A very long name is written whole: no truncation, no ellipsis, the width its full length", context =>
            {
                var longName = string.Concat(Enumerable.Repeat("Fuzn.Shop.Tests.VeryDeeplyNestedNamespace.", 250)) + "Verify_checkout";
                Assert.IsGreaterThan(10000, longName.Length);

                var lines = StartupBanner.Render(Label, longName, Detail, ColorMode.None);

                Assert.AreEqual("Running test: " + longName, lines[1].Text);
                Assert.AreEqual(Label.Length + 1 + longName.Length, lines[1].Width);
                Assert.DoesNotContain(MarkupText.Ellipsis.ToString(), lines[1].Text);

                var styled = StartupBanner.Render(Label, longName, Detail, ColorMode.TrueColor);
                Assert.EndsWith(longName + AnsiCodes.Reset, styled[1].Text);
                Assert.AreEqual(lines[1].Width, styled[1].Width);
            })
            .Step("Control characters in a name sanitize to spaces, so a line can neither break nor carry an escape sequence", context =>
            {
                var lines = StartupBanner.Render(Label, "Tests.Bad\tname\u001b[2J\r\nmore", "Detail\u0007", ColorMode.None);

                Assert.AreEqual("Running test: Tests.Bad name [2J more", lines[1].Text);
                Assert.AreEqual("Detail ", lines[2].Text);
                Assert.AreEqual(lines[1].Text.Length, lines[1].Width);
                foreach (var line in lines)
                    Assert.DoesNotContain(AnsiCodes.Escape, line.Text);
            })
            .Step("An empty subject and an empty detail render as an empty subject and an empty line", context =>
            {
                var lines = StartupBanner.Render(Label, string.Empty, string.Empty, ColorMode.None);

                Assert.AreEqual("Running test: ", lines[1].Text);
                Assert.AreEqual(string.Empty, lines[2].Text);
                Assert.AreEqual(0, lines[2].Width);
            })
            .Run();
    }

    [Test]
    public async Task Verify_write_emits_one_write_per_line_without_reading_the_terminal_size()
    {
        await Scenario()
            .Step("Three writes, each one banner line ending in the line terminator, and the size never read", context =>
            {
                var writer = new FakeTerminalWriter();

                StartupBanner.Write(writer, Label, Subject, Detail, ColorMode.None);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "⚡ TestFuzn" + Environment.NewLine,
                        "Running test: Fuzn.Shop.Tests.CheckoutTests.Verify_checkout" + Environment.NewLine,
                        "Assembly: Fuzn.Shop.Tests · Target environment: staging" + Environment.NewLine
                    },
                    writer.Writes.ToList());
                Assert.AreEqual(0, writer.WindowWidthReadCount);
                Assert.AreEqual(0, writer.WindowHeightReadCount);
            })
            .Step("The styled writes carry the same lines as Render", context =>
            {
                var writer = new FakeTerminalWriter();
                var expected = StartupBanner.Render(Label, Subject, Detail, ColorMode.TrueColor);

                StartupBanner.Write(writer, Label, Subject, Detail, ColorMode.TrueColor);

                Assert.HasCount(StartupBanner.Height, writer.Writes);
                for (var row = 0; row < StartupBanner.Height; row++)
                    Assert.AreEqual(expected[row].Text + Environment.NewLine, writer.Writes[row], $"Write {row}");
            })
            .Step("Null arguments are rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => StartupBanner.Render(null!, Subject, Detail, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => StartupBanner.Render(Label, null!, Detail, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => StartupBanner.Render(Label, Subject, null!, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => StartupBanner.Write(null!, Label, Subject, Detail, ColorMode.None));
            })
            .Run();
    }
}
