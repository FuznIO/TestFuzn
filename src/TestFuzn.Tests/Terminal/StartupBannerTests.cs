using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class StartupBannerTests : Test
{
    private const string Label = "Running test:";
    private const string Subject = "Fuzn.Shop.Tests.CheckoutTests.Verify_checkout";

    private const string TrueColorLabelStart = AnsiCodes.Csi + "1;38;2;255;157;61m";
    private const string Colors16LabelStart = AnsiCodes.Csi + "1;93m";

    [Test]
    public async Task Verify_banner_golden_lines_per_color_mode()
    {
        await Scenario()
            .Step("TrueColor: the single line carries the label in the warm accent and the subject bold", context =>
            {
                var lines = StartupBanner.Render(Label, Subject, ColorMode.TrueColor);

                Assert.HasCount(StartupBanner.Height, lines);
                Assert.AreEqual(TrueColorLabelStart + Label + AnsiCodes.Reset + " " + AnsiCodes.Bold + Subject + AnsiCodes.Reset, lines[0].Text);
                Assert.AreEqual(Label.Length + 1 + Subject.Length, lines[0].Width);
            })
            .Step("Colors16 downgrades the label accent to bright yellow, the rest as in TrueColor", context =>
            {
                var lines = StartupBanner.Render(Label, Subject, ColorMode.Colors16);

                Assert.HasCount(StartupBanner.Height, lines);
                Assert.AreEqual(Colors16LabelStart + Label + AnsiCodes.Reset + " " + AnsiCodes.Bold + Subject + AnsiCodes.Reset, lines[0].Text);
            })
            .Step("Monochrome keeps bold but drops every color", context =>
            {
                var lines = StartupBanner.Render(Label, Subject, ColorMode.Monochrome);

                Assert.HasCount(StartupBanner.Height, lines);
                Assert.AreEqual(AnsiCodes.Bold + Label + AnsiCodes.Reset + " " + AnsiCodes.Bold + Subject + AnsiCodes.Reset, lines[0].Text);
            })
            .Step("None renders the same line as plain text with zero escape bytes", context =>
            {
                var lines = StartupBanner.Render(Label, Subject, ColorMode.None);

                Assert.HasCount(StartupBanner.Height, lines);
                Assert.AreEqual("Running test: Fuzn.Shop.Tests.CheckoutTests.Verify_checkout", lines[0].Text);
                Assert.DoesNotContain(AnsiCodes.Escape, lines[0].Text);
                Assert.AreEqual(lines[0].Text.Length, lines[0].Width);
            })
            .Run();
    }

    [Test]
    public async Task Verify_names_render_literally_whole_and_sanitized()
    {
        await Scenario()
            .Step("Brackets in the subject render literally, not as markup", context =>
            {
                var lines = StartupBanner.Render(Label, "Tests.Weird[bold]name[/]", ColorMode.TrueColor);

                Assert.AreEqual(TrueColorLabelStart + Label + AnsiCodes.Reset + " " + AnsiCodes.Bold + "Tests.Weird[bold]name[/]" + AnsiCodes.Reset, lines[0].Text);
                Assert.AreEqual(Label.Length + 1 + "Tests.Weird[bold]name[/]".Length, lines[0].Width);

                var plain = StartupBanner.Render(Label, "Tests.Weird[bold]name[/]", ColorMode.None);
                Assert.AreEqual("Running test: Tests.Weird[bold]name[/]", plain[0].Text);
            })
            .Step("A very long name is written whole: no truncation, no ellipsis, the width its full length", context =>
            {
                var longName = string.Concat(Enumerable.Repeat("Fuzn.Shop.Tests.VeryDeeplyNestedNamespace.", 250)) + "Verify_checkout";
                Assert.IsGreaterThan(10000, longName.Length);

                var lines = StartupBanner.Render(Label, longName, ColorMode.None);

                Assert.AreEqual("Running test: " + longName, lines[0].Text);
                Assert.AreEqual(Label.Length + 1 + longName.Length, lines[0].Width);
                Assert.DoesNotContain(MarkupText.Ellipsis.ToString(), lines[0].Text);

                var styled = StartupBanner.Render(Label, longName, ColorMode.TrueColor);
                Assert.EndsWith(longName + AnsiCodes.Reset, styled[0].Text);
                Assert.AreEqual(lines[0].Width, styled[0].Width);
            })
            .Step("Control characters in a name sanitize to spaces, so a line can neither break nor carry an escape sequence", context =>
            {
                var lines = StartupBanner.Render(Label, "Tests.Bad\tname\u001b[2J\r\nmore", ColorMode.None);

                Assert.AreEqual("Running test: Tests.Bad name [2J more", lines[0].Text);
                Assert.AreEqual(lines[0].Text.Length, lines[0].Width);
                Assert.DoesNotContain(AnsiCodes.Escape, lines[0].Text);
            })
            .Step("An empty subject renders as the label and a trailing space", context =>
            {
                var lines = StartupBanner.Render(Label, string.Empty, ColorMode.None);

                Assert.AreEqual("Running test: ", lines[0].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_write_emits_one_write_per_line_without_reading_the_terminal_size()
    {
        await Scenario()
            .Step("One write, the banner line ending in the line terminator, and the size never read", context =>
            {
                var writer = new FakeTerminalWriter();

                StartupBanner.Write(writer, Label, Subject, ColorMode.None);

                CollectionAssert.AreEqual(
                    new[] { "Running test: Fuzn.Shop.Tests.CheckoutTests.Verify_checkout" + Environment.NewLine },
                    writer.Writes.ToList());
                Assert.AreEqual(0, writer.WindowWidthReadCount);
                Assert.AreEqual(0, writer.WindowHeightReadCount);
            })
            .Step("The styled writes carry the same lines as Render", context =>
            {
                var writer = new FakeTerminalWriter();
                var expected = StartupBanner.Render(Label, Subject, ColorMode.TrueColor);

                StartupBanner.Write(writer, Label, Subject, ColorMode.TrueColor);

                Assert.HasCount(StartupBanner.Height, writer.Writes);
                for (var row = 0; row < StartupBanner.Height; row++)
                    Assert.AreEqual(expected[row].Text + Environment.NewLine, writer.Writes[row], $"Write {row}");
            })
            .Step("Null arguments are rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => StartupBanner.Render(null!, Subject, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => StartupBanner.Render(Label, null!, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => StartupBanner.Write(null!, Label, Subject, ColorMode.None));
            })
            .Run();
    }
}
