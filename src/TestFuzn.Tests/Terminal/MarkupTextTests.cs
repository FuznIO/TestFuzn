using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class MarkupTextTests : Test
{
    [Test]
    public async Task Verify_measure_uses_display_width_of_stripped_text()
    {
        await Scenario()
            .Step("Plain text measures its character count", context =>
            {
                Assert.AreEqual(5, MarkupText.Measure("hello"));
                Assert.AreEqual(0, MarkupText.Measure(string.Empty));
                Assert.AreEqual(0, MarkupText.Measure(null));
            })
            .Step("Markup tags contribute nothing and escapes count unescaped", context =>
            {
                Assert.AreEqual(5, MarkupText.Measure("[green]hello[/]"));
                Assert.AreEqual(10, MarkupText.Measure("[bold white on blue] Metadata [/]"));
                Assert.AreEqual(3, MarkupText.Measure("[[x]]"));
            })
            .Step("Each control character counts as the single space it sanitizes to", context =>
            {
                Assert.AreEqual(3, MarkupText.Measure("a\r\nb"));
                Assert.AreEqual(3, MarkupText.Measure("a\nb"));
                Assert.AreEqual(3, MarkupText.Measure("a\rb"));
                Assert.AreEqual(4, MarkupText.Measure("a\n\nb"));
                Assert.AreEqual(3, MarkupText.Measure("a\tb"));
                Assert.AreEqual(3, MarkupText.Measure("a\u001bb"));
            })
            .Run();
    }

    [Test]
    public async Task Verify_render_fitted_pads_to_the_exact_width()
    {
        await Scenario()
            .Step("Left alignment pads on the right, right alignment on the left", context =>
            {
                Assert.AreEqual("ok   ", MarkupText.RenderFitted("ok", 5, ColorMode.None).Text);
                Assert.AreEqual("   ok", MarkupText.RenderFitted("ok", 5, ColorMode.None, TextAlignment.Right).Text);
                Assert.AreEqual("abcde", MarkupText.RenderFitted("abcde", 5, ColorMode.None).Text);
                Assert.AreEqual("   ", MarkupText.RenderFitted(string.Empty, 3, ColorMode.None).Text);
            })
            .Step("The rendered line declares the exact width it was fitted to", context =>
            {
                Assert.AreEqual(5, MarkupText.RenderFitted("ok", 5, ColorMode.None).Width);
                Assert.AreEqual(5, MarkupText.RenderFitted("abcdefgh", 5, ColorMode.None).Width);
                Assert.AreEqual(3, MarkupText.RenderFitted(string.Empty, 3, ColorMode.None).Width);
            })
            .Step("Padding is plain spaces outside the styling", context =>
            {
                Assert.AreEqual("\u001b[38;5;2mok\u001b[0m   ", MarkupText.RenderFitted("[green]ok[/]", 5, ColorMode.TrueColor).Text);
                Assert.AreEqual("   \u001b[38;5;2mok\u001b[0m", MarkupText.RenderFitted("[green]ok[/]", 5, ColorMode.TrueColor, TextAlignment.Right).Text);
            })
            .Step("A width below 1 renders an empty line of width 0", context =>
            {
                Assert.AreEqual(string.Empty, MarkupText.RenderFitted("abc", 0, ColorMode.None).Text);
                Assert.AreEqual(string.Empty, MarkupText.RenderFitted("abc", -1, ColorMode.None).Text);
                Assert.AreEqual(0, MarkupText.RenderFitted("abc", 0, ColorMode.None).Width);
            })
            .Run();
    }

    [Test]
    public async Task Verify_truncation_replaces_the_overflow_with_an_ellipsis()
    {
        await Scenario()
            .Step("Content wider than the width keeps width minus one characters plus the ellipsis", context =>
            {
                Assert.AreEqual("abc…", MarkupText.RenderTruncated("abcdef", 4, ColorMode.None).Text);
                Assert.AreEqual("abc…", MarkupText.RenderFitted("abcdef", 4, ColorMode.None).Text);
                Assert.AreEqual("…", MarkupText.RenderTruncated("abcdef", 1, ColorMode.None).Text);
            })
            .Step("Content that fits is not touched and never padded by RenderTruncated", context =>
            {
                Assert.AreEqual("abcdef", MarkupText.RenderTruncated("abcdef", 6, ColorMode.None).Text);
                Assert.AreEqual("ab", MarkupText.RenderTruncated("ab", 6, ColorMode.None).Text);
            })
            .Step("The rendered line declares the content's actual width, never the maximum", context =>
            {
                Assert.AreEqual(4, MarkupText.RenderTruncated("abcdef", 4, ColorMode.None).Width);
                Assert.AreEqual(2, MarkupText.RenderTruncated("ab", 6, ColorMode.None).Width);
            })
            .Step("A maximum below 1 renders an empty line of width 0", context =>
            {
                Assert.AreEqual(string.Empty, MarkupText.RenderTruncated("abcdef", 0, ColorMode.None).Text);
                Assert.AreEqual(0, MarkupText.RenderTruncated("abcdef", 0, ColorMode.None).Width);
            })
            .Run();
    }

    [Test]
    public async Task Verify_truncated_styled_content_stays_reset_closed()
    {
        await Scenario()
            .Step("A cut inside a styled span appends the ellipsis to it, still Reset-closed", context =>
            {
                Assert.AreEqual("\u001b[38;5;2mabc…\u001b[0m", MarkupText.RenderTruncated("[green]abcdef[/]", 4, ColorMode.TrueColor).Text);
                Assert.AreEqual("\u001b[38;5;9ma…\u001b[0m", MarkupText.RenderTruncated("[red]abc[/]def", 2, ColorMode.TrueColor).Text);
            })
            .Step("A cut on a span boundary styles the ellipsis like the first removed character", context =>
            {
                Assert.AreEqual("ab\u001b[38;5;9m…\u001b[0m", MarkupText.RenderTruncated("ab[red]cdef[/]", 3, ColorMode.TrueColor).Text);
            })
            .Step("Color mode None truncates to pure plain text", context =>
            {
                var line = MarkupText.RenderTruncated("[green]abcdef[/]", 4, ColorMode.None);

                Assert.AreEqual("abc…", line.Text);
                Assert.DoesNotContain("\u001b", line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_truncation_never_splits_a_surrogate_pair()
    {
        await Scenario()
            .Step("A cut that would land inside a pair backs off one character", context =>
            {
                var line = MarkupText.RenderTruncated("😀😀😀", 4, ColorMode.None);

                Assert.AreEqual("😀…", line.Text);
                Assert.AreEqual(3, line.Width);
            })
            .Step("A fitted render absorbs the backed-off column with padding, staying exact", context =>
            {
                var line = MarkupText.RenderFitted("😀😀😀", 4, ColorMode.None);

                Assert.AreEqual("😀… ", line.Text);
                Assert.AreEqual(4, line.Width);
            })
            .Step("A cut between two pairs needs no back-off", context =>
            {
                Assert.AreEqual("😀😀…", MarkupText.RenderTruncated("😀😀😀", 5, ColorMode.None).Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_control_characters_render_as_spaces_so_output_stays_one_row()
    {
        await Scenario()
            .Step("Breaks in plain text become single spaces", context =>
            {
                Assert.AreEqual("a b  ", MarkupText.RenderFitted("a\nb", 5, ColorMode.None).Text);
                Assert.AreEqual("a b", MarkupText.RenderTruncated("a\r\nb", 10, ColorMode.None).Text);
            })
            .Step("Breaks inside a styled run become spaces inside one style-complete segment", context =>
            {
                Assert.AreEqual("\u001b[38;5;2ma b\u001b[0m", MarkupText.RenderTruncated("[green]a\nb[/]", 10, ColorMode.TrueColor).Text);
            })
            .Step("TAB, ESC, BEL and DEL each become a single space at exact width", context =>
            {
                Assert.AreEqual("a b", MarkupText.RenderTruncated("a\tb", 10, ColorMode.None).Text);
                Assert.AreEqual("a b", MarkupText.RenderTruncated("a\u001bb", 10, ColorMode.None).Text);
                Assert.AreEqual("a b", MarkupText.RenderTruncated("a\ab", 10, ColorMode.None).Text);
                Assert.AreEqual("a b", MarkupText.RenderTruncated("a\u007fb", 10, ColorMode.None).Text);
                Assert.AreEqual(3, MarkupText.RenderTruncated("a\tb", 10, ColorMode.None).Width);
            })
            .Step("An embedded erase-screen escape sequence renders inert", context =>
            {
                var line = MarkupText.RenderFitted("esc\u001b[2Jclear", 12, ColorMode.None);

                Assert.AreEqual("esc [2Jclear", line.Text);
                Assert.AreEqual(12, line.Width);
                Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("Truncation cannot cut inside an input escape sequence, and mode None emits no ESC byte", context =>
            {
                var line = MarkupText.RenderFitted("esc\u001b[31minjected", 6, ColorMode.None);

                Assert.AreEqual("esc […", line.Text);
                Assert.DoesNotContain("\u001b", line.Text);
            })
            .Run();
    }
}
