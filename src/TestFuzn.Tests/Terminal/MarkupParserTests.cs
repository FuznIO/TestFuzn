using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class MarkupParserTests : Test
{
    [Test]
    public async Task Verify_plain_text_and_styled_spans()
    {
        await Scenario()
            .Step("Plain text parses to a single unstyled span", context =>
            {
                var spans = MarkupParser.Parse("Requests per second");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("Requests per second", spans[0].Text);
                Assert.IsTrue(spans[0].Style.IsPlain);
            })
            .Step("A color tag styles its content with the xterm palette entry", context =>
            {
                var spans = MarkupParser.Parse("[green]Passed[/]");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("Passed", spans[0].Text);
                AssertForegroundPalette(spans[0], 2);
            })
            .Step("Text around a tag becomes separate plain spans", context =>
            {
                var spans = MarkupParser.Parse("a[red]b[/]c");

                Assert.HasCount(3, spans);
                Assert.AreEqual("a", spans[0].Text);
                Assert.IsTrue(spans[0].Style.IsPlain);
                Assert.AreEqual("b", spans[1].Text);
                AssertForegroundPalette(spans[1], 9);
                Assert.AreEqual("c", spans[2].Text);
                Assert.IsTrue(spans[2].Style.IsPlain);
            })
            .Step("Null and empty markup parse to no spans", context =>
            {
                Assert.IsEmpty(MarkupParser.Parse(null));
                Assert.IsEmpty(MarkupParser.Parse(string.Empty));
            })
            .Run();
    }

    [Test]
    public async Task Verify_compound_and_background_tags()
    {
        await Scenario()
            .Step("A compound tag sets decoration, foreground and background together", context =>
            {
                var spans = MarkupParser.Parse("[bold white on blue] Metadata [/]");

                Assert.ContainsSingle(spans);
                Assert.AreEqual(" Metadata ", spans[0].Text);
                Assert.IsTrue(spans[0].Style.Bold);
                AssertForegroundPalette(spans[0], 15);
                AssertBackgroundPalette(spans[0], 12);
            })
            .Step("The underline abbreviation u used by table headers parses", context =>
            {
                var spans = MarkupParser.Parse("[u]Scenario[/]");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("Scenario", spans[0].Text);
                Assert.IsTrue(spans[0].Style.Underline);
                Assert.IsNull(spans[0].Style.Foreground);
            })
            .Step("darkgreen resolves to palette entry 22", context =>
            {
                var spans = MarkupParser.Parse("[bold white on darkgreen] Step Details [/]");

                Assert.ContainsSingle(spans);
                AssertBackgroundPalette(spans[0], 22);
            })
            .Step("A background-only tag leaves the foreground unset", context =>
            {
                var spans = MarkupParser.Parse("[on blue]x[/]");

                Assert.ContainsSingle(spans);
                Assert.IsNull(spans[0].Style.Foreground);
                AssertBackgroundPalette(spans[0], 12);
            })
            .Step("Color names are case insensitive", context =>
            {
                var spans = MarkupParser.Parse("[Green]x[/]");

                Assert.ContainsSingle(spans);
                AssertForegroundPalette(spans[0], 2);
            })
            .Run();
    }

    [Test]
    public async Task Verify_nested_tags_restore_enclosing_style()
    {
        await Scenario()
            .Step("An inner color overrides the outer color and closing restores it", context =>
            {
                var spans = MarkupParser.Parse("[red]a[green]b[/]c[/]");

                Assert.HasCount(3, spans);
                AssertForegroundPalette(spans[0], 9);
                AssertForegroundPalette(spans[1], 2);
                AssertForegroundPalette(spans[2], 9);
            })
            .Step("An inner decoration combines with the outer color", context =>
            {
                var spans = MarkupParser.Parse("[red]a[bold]b[/]c[/]");

                Assert.HasCount(3, spans);
                Assert.IsFalse(spans[0].Style.Bold);
                Assert.IsTrue(spans[1].Style.Bold);
                AssertForegroundPalette(spans[1], 9);
                Assert.IsFalse(spans[2].Style.Bold);
            })
            .Step("Adjacent tags with no text between them combine into one span", context =>
            {
                var spans = MarkupParser.Parse("[bold][red]x[/][/]");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("x", spans[0].Text);
                Assert.IsTrue(spans[0].Style.Bold);
                AssertForegroundPalette(spans[0], 9);
            })
            .Run();
    }

    [Test]
    public async Task Verify_literal_bracket_escapes()
    {
        await Scenario()
            .Step("Doubled brackets are literal brackets", context =>
            {
                var spans = MarkupParser.Parse("a[[x]]b");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("a[x]b", spans[0].Text);
                Assert.IsTrue(spans[0].Style.IsPlain);
            })
            .Step("Escapes inside a styled run stay in the styled span", context =>
            {
                var spans = MarkupParser.Parse("[grey]a[[b]][/]");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("a[b]", spans[0].Text);
                AssertForegroundPalette(spans[0], 8);
            })
            .Run();
    }

    [Test]
    public async Task Verify_malformed_markup_tolerance()
    {
        await Scenario()
            .Step("An unclosed tag styles to the end of the string", context =>
            {
                var spans = MarkupParser.Parse("[red]text");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("text", spans[0].Text);
                AssertForegroundPalette(spans[0], 9);
            })
            .Step("A stray closing tag is ignored", context =>
            {
                var spans = MarkupParser.Parse("a[/]b");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("ab", spans[0].Text);
                Assert.IsTrue(spans[0].Style.IsPlain);
            })
            .Step("A tag with an unknown color or style word is literal text", context =>
            {
                var spans = MarkupParser.Parse("[notacolor]x[/]");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("[notacolor]x", spans[0].Text);
                Assert.IsTrue(spans[0].Style.IsPlain);
            })
            .Step("An unterminated bracket is literal text", context =>
            {
                var spans = MarkupParser.Parse("abc[red");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("abc[red", spans[0].Text);
            })
            .Step("A lone closing bracket is literal text", context =>
            {
                var spans = MarkupParser.Parse("a]b");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("a]b", spans[0].Text);
            })
            .Step("An empty tag is literal text", context =>
            {
                var spans = MarkupParser.Parse("a[]b");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("a[]b", spans[0].Text);
            })
            .Step("A named closing tag is literal text", context =>
            {
                var spans = MarkupParser.Parse("[/red]x");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("[/red]x", spans[0].Text);
            })
            .Step("When a tag repeats a foreground color the last one wins", context =>
            {
                var spans = MarkupParser.Parse("[red green]x[/]");

                Assert.ContainsSingle(spans);
                AssertForegroundPalette(spans[0], 2);
            })
            .Step("A dangling on with no background color after it is dropped", context =>
            {
                var spans = MarkupParser.Parse("[bold on]x[/]");

                Assert.ContainsSingle(spans);
                Assert.IsTrue(spans[0].Style.Bold);
                Assert.IsNull(spans[0].Style.Background);
            })
            .Step("Extra spaces inside a tag are tolerated", context =>
            {
                var spans = MarkupParser.Parse("[ red ]x[/]");

                Assert.ContainsSingle(spans);
                Assert.AreEqual("x", spans[0].Text);
                AssertForegroundPalette(spans[0], 9);
            })
            .Step("Tags left open at the end of the string are closed implicitly", context =>
            {
                var spans = MarkupParser.Parse("[red]a[green]b");

                Assert.HasCount(2, spans);
                AssertForegroundPalette(spans[0], 9);
                AssertForegroundPalette(spans[1], 2);
            })
            .Run();
    }

    [Test]
    public async Task Verify_parser_never_throws()
    {
        var nastyInputs = new List<string>
        {
            "[", "]", "[/", "[[", "]]", "[[[", "]]]", "[]", "[ ]", "[/]", "[/][/]",
            "[red][/][/]", "[on]x[/]", "[on on blue]x[/]", "[red [green]x[/]", "[#zz]x[/]",
            "[#12345]x[/]", "[#1234567]x[/]", "[bold", "bold]", "[red]a[", "[red]a]",
            "[\ud800]", "[\udc00]", "\ud800", "\udc00",
            "[red]a\u0000b\u0007c[/]\u001b\u0001"
        };

        var openTags = string.Concat(Enumerable.Repeat("[red]", 1000));
        var closeTags = string.Concat(Enumerable.Repeat("[/]", 1000));
        nastyInputs.Add(openTags + "x" + closeTags);
        nastyInputs.Add(openTags + "x");

        await Scenario()
            .Step("No malformed input throws in Parse, StripMarkup or Render in any color mode", context =>
            {
                Assert.IsNotEmpty(nastyInputs);

                foreach (var input in nastyInputs)
                {
                    var spans = MarkupParser.Parse(input);
                    Assert.IsNotNull(spans, $"Parse returned null for {input}");

                    var stripped = MarkupParser.StripMarkup(input);
                    Assert.IsNotNull(stripped, $"StripMarkup returned null for {input}");

                    foreach (var colorMode in new[] { ColorMode.None, ColorMode.Monochrome, ColorMode.Colors16, ColorMode.TrueColor })
                    {
                        var rendered = MarkupRenderer.Render(input, colorMode);
                        Assert.IsNotNull(rendered, $"Render returned null for {input} in {colorMode}");
                    }
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_strip_markup()
    {
        var expectedPlainTexts = new Dictionary<string, string>
        {
            { "[red]Assert exceptions:[/]", "Assert exceptions:" },
            { "[bold white on blue] Metadata [/]", " Metadata " },
            { "[bold]Total elapsed Time:[/] [yellow]00:00:05[/]", "Total elapsed Time: 00:00:05" },
            { "[green]Status: Completed successfully.[/]\r\n", "Status: Completed successfully.\r\n" },
            { "no markup at all", "no markup at all" },
            { "[grey]a[[b]][/]", "a[b]" },
            { "[notacolor]x[/]", "[notacolor]x" },
            { "abc[red", "abc[red" }
        };

        await Scenario()
            .Step("Valid tags are removed, escapes unescaped and malformed markup preserved", context =>
            {
                Assert.IsNotEmpty(expectedPlainTexts);

                foreach (var pair in expectedPlainTexts)
                    Assert.AreEqual(pair.Value, MarkupParser.StripMarkup(pair.Key), $"StripMarkup mismatch for {pair.Key}");
            })
            .Step("Null and empty markup strip to an empty string", context =>
            {
                Assert.AreEqual(string.Empty, MarkupParser.StripMarkup(null));
                Assert.AreEqual(string.Empty, MarkupParser.StripMarkup(string.Empty));
            })
            .Run();
    }

    [Test]
    public async Task Verify_escape_makes_bracketed_text_render_literally()
    {
        await Scenario()
            .Step("Escaped brackets survive a round trip through the parser as literal text", context =>
            {
                Assert.AreEqual("a [[b]] c", MarkupParser.Escape("a [b] c"));
                Assert.AreEqual("a [b] c", MarkupParser.StripMarkup("[red]" + MarkupParser.Escape("a [b] c") + "[/]"));
                Assert.AreEqual("[red]x[/]", MarkupParser.StripMarkup(MarkupParser.Escape("[red]x[/]")));
            })
            .Step("Text without brackets is returned unchanged, and null is rejected", context =>
            {
                Assert.AreSame("plain text", MarkupParser.Escape("plain text"));
                Assert.ThrowsExactly<ArgumentNullException>(() => MarkupParser.Escape(null!));
            })
            .Run();
    }

    [Test]
    public async Task Verify_strip_markup_matches_markup_helper_for_repo_constant_strings()
    {
        // The CONSTANT markup strings the framework writes through ITestFrameworkAdapter.WriteMarkup
        // and WriteTable today — ConsoleWriter's MSTest-path summary lines and the cells of its
        // threshold verdict table, ConsoleManager's live view failure, StandaloneRunnerCore's skip
        // and invocation-error lines, HttpPlugin's captured request —
        // and the TestSelectionMenu's prompt-fallback lines, with bracket-free representative
        // values for the interpolated parts. The MSTest adapter strips through MarkupHelper (and
        // its own regex), so both strip paths must agree on these; the standalone summary's own
        // markup is rendered by the engine and never reaches a strip path. Interpolated templates
        // whose values carry brackets diverge deliberately — pinned in
        // Verify_strip_markup_preserves_bracketed_data_unlike_markup_helper below.
        var repoMarkupStrings = new List<string>
        {
            "[red]HTTP Plugin: Latest HTTP request captured during failed step[/]",
            "[grey]GET https://localhost:7058/products HTTP/1.1[/]",
            "[bold]Total elapsed Time:[/] [yellow]00:00:05:00[/]",
            "[red]Status: Stopped[/]\r\n",
            "[red]Status: Stopped, reason: Assert failed[/]\r\n",
            "[green]Status: Completed successfully.[/]\r\n",
            "[bold]Thresholds:[/]",
            "[green]Ok[/]",
            "[red]Breached[/]",
            "[red]Assert exceptions:[/]",
            "  [red]Expected response time to be below 100ms[/]",
            "[red]Errors by Step:[/]",
            "[red]Fetch products:[/]",
            "  [red]Connection refused (Count: 3)[/]",
            "[red]Live view failed: IOException: Broken pipe[/]",
            "[yellow]Test skipped.[/]",
            "[red]--test-name requires a value: --test-name=<FullyQualifiedName>[/]",
            "[bold green]TestFuzn Test Runner[/]",
            "[bold green]Enter the ID of the test you want to run or enter to quit:[/]",
            "[bold red]Invalid test index.[/]"
        };

        await Scenario()
            .Step("MarkupParser.StripMarkup matches MarkupHelper.StripMarkup for every string", context =>
            {
                Assert.IsNotEmpty(repoMarkupStrings);

                foreach (var markup in repoMarkupStrings)
                    Assert.AreEqual(MarkupHelper.StripMarkup(markup), MarkupParser.StripMarkup(markup), $"Strip mismatch for {markup}");
            })
            .Run();
    }

    [Test]
    public async Task Verify_strip_markup_preserves_bracketed_data_unlike_markup_helper()
    {
        // A deliberate divergence: the repo's interpolated markup templates
        // (assert messages, step names, error texts) can carry brackets in their values.
        // MarkupParser.StripMarkup keeps a bracketed group that is not a valid tag as literal
        // data, while the legacy regex-based MarkupHelper.StripMarkup removes every bracketed
        // group, data included. Both behaviors are asserted so a change to either shows up here.
        await Scenario()
            .Step("An equality assert message keeps its expected and actual values", context =>
            {
                var markup = "  [red]Assert.AreEqual failed. Expected:<[1, 2, 3]>. Actual:<[1, 2]>.[/]";

                Assert.AreEqual(
                    "  Assert.AreEqual failed. Expected:<[1, 2, 3]>. Actual:<[1, 2]>.",
                    MarkupParser.StripMarkup(markup));
                Assert.AreEqual(
                    "  Assert.AreEqual failed. Expected:<>. Actual:<>.",
                    MarkupHelper.StripMarkup(markup));
            })
            .Step("A collection assert message keeps its bracketed collections", context =>
            {
                var markup = "  [red]Expected collection [a, b] to contain [c][/]";

                Assert.AreEqual(
                    "  Expected collection [a, b] to contain [c]",
                    MarkupParser.StripMarkup(markup));
                Assert.AreEqual(
                    "  Expected collection  to contain ",
                    MarkupHelper.StripMarkup(markup));
            })
            .Step("A step name with an index keeps the index", context =>
            {
                var markup = "[red]Step [0] Fetch:[/]";

                Assert.AreEqual("Step [0] Fetch:", MarkupParser.StripMarkup(markup));
                Assert.AreEqual("Step  Fetch:", MarkupHelper.StripMarkup(markup));
            })
            .Run();
    }

    private static void AssertForegroundPalette(StyledSpan span, byte expectedPaletteIndex)
    {
        Assert.IsNotNull(span.Style.Foreground);
        Assert.AreEqual(expectedPaletteIndex, span.Style.Foreground.Value.PaletteIndex);
    }

    private static void AssertBackgroundPalette(StyledSpan span, byte expectedPaletteIndex)
    {
        Assert.IsNotNull(span.Style.Background);
        Assert.AreEqual(expectedPaletteIndex, span.Style.Background.Value.PaletteIndex);
    }
}
