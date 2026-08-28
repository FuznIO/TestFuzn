using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class KeyHintBarWidgetTests : Test
{
    [Test]
    public async Task Verify_key_hint_bar_golden_frames_at_fixed_widths()
    {
        await Scenario()
            .Step("Width 20 renders bold keys with dimmed separators", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("q", "quit"), new KeyHint("f", "filter") }, 20, ColorMode.TrueColor));

                Assert.AreEqual("\u001b[1mq\u001b[0m quit\u001b[2m · \u001b[0m\u001b[1mf\u001b[0m filter", line.Text);
                Assert.AreEqual(17, line.Width);
            })
            .Step("Width 17 is an exact fit for both hints", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("q", "quit"), new KeyHint("f", "filter") }, 17, ColorMode.TrueColor));

                Assert.AreEqual("\u001b[1mq\u001b[0m quit\u001b[2m · \u001b[0m\u001b[1mf\u001b[0m filter", line.Text);
                Assert.AreEqual(17, line.Width);
            })
            .Step("Color mode None renders pure plain text", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("q", "quit"), new KeyHint("f", "filter") }, 20, ColorMode.None));

                Assert.AreEqual("q quit · f filter", line.Text);
                Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("Width 30 fits three hints", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("q", "quit"), new KeyHint("f", "filter"), new KeyHint("s", "stop") }, 30, ColorMode.TrueColor));

                Assert.AreEqual("\u001b[1mq\u001b[0m quit\u001b[2m · \u001b[0m\u001b[1mf\u001b[0m filter\u001b[2m · \u001b[0m\u001b[1ms\u001b[0m stop", line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_key_hint_bar_truncates_gracefully()
    {
        await Scenario()
            .Step("A hint that does not fit entirely is dropped along with the rest", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("q", "quit"), new KeyHint("f", "filter"), new KeyHint("s", "stop") }, 20, ColorMode.TrueColor));

                Assert.AreEqual("\u001b[1mq\u001b[0m quit\u001b[2m · \u001b[0m\u001b[1mf\u001b[0m filter", line.Text);
            })
            .Step("Width 10 keeps only the first hint", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("q", "quit"), new KeyHint("f", "filter") }, 10, ColorMode.TrueColor));

                Assert.AreEqual("\u001b[1mq\u001b[0m quit", line.Text);
                Assert.AreEqual(6, line.Width);
            })
            .Step("When even the first hint is too wide it truncates with an ellipsis", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("q", "quit") }, 4, ColorMode.TrueColor));

                Assert.AreEqual("\u001b[1mq\u001b[0m q…", line.Text);
                Assert.AreEqual(4, line.Width);
            })
            .Step("No hints renders an empty line and edge inputs are handled", context =>
            {
                Assert.AreEqual(string.Empty, Assert.ContainsSingle(KeyHintBarWidget.Render(Array.Empty<KeyHint>(), 10, ColorMode.None)).Text);
                Assert.IsEmpty(KeyHintBarWidget.Render(Array.Empty<KeyHint>(), 0, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => KeyHintBarWidget.Render(null!, 10, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_key_hint_bar_sanitizes_line_breaks_to_a_single_row()
    {
        await Scenario()
            .Step("A line break inside a description becomes a space instead of splitting the line", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("q", "qu\nit") }, 20, ColorMode.None));

                Assert.AreEqual("q qu it", line.Text);
                Assert.AreEqual(7, line.Width);
            })
            .Step("A CRLF counts as the one column it renders as", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("a\r\nb", "c"), new KeyHint("d", "e") }, 40, ColorMode.None));

                Assert.AreEqual("a b c · d e", line.Text);
                Assert.AreEqual(11, line.Width);
            })
            .Step("Styled hints stay style-complete around the sanitized break", context =>
            {
                var line = Assert.ContainsSingle(KeyHintBarWidget.Render(
                    new[] { new KeyHint("q", "qu\nit") }, 20, ColorMode.TrueColor));

                Assert.AreEqual("\u001b[1mq\u001b[0m qu it", line.Text);
            })
            .Run();
    }
}
