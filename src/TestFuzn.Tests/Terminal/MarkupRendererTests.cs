using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class MarkupRendererTests : Test
{
    // Per color name: the 16-color foreground SGR code and the xterm palette index, both
    // captured from the console library the standalone runner used before the engine (its 0.50
    // release) rendering the same markup. Names map to that library's xterm palette, NOT to
    // ConsoleColor: green is dark (32, palette 2) while red is bright (91, palette 9).
    // Backgrounds are the foreground code + 10 and 48;5;N respectively.
    private static readonly Dictionary<string, (int Colors16Code, int PaletteIndex)> ExpectedNameCodes = new()
    {
        { "black", (30, 0) },
        { "maroon", (31, 1) },
        { "green", (32, 2) },
        { "olive", (33, 3) },
        { "navy", (34, 4) },
        { "purple", (35, 5) },
        { "teal", (36, 6) },
        { "silver", (37, 7) },
        { "grey", (90, 8) },
        { "red", (91, 9) },
        { "lime", (92, 10) },
        { "yellow", (93, 11) },
        { "blue", (94, 12) },
        { "fuchsia", (95, 13) },
        { "aqua", (96, 14) },
        { "white", (97, 15) }
    };

    [Test]
    public async Task Verify_color_names_render_xterm_palette_codes()
    {
        await Scenario()
            .Step("Foregrounds in 16-color mode use the xterm palette codes", context =>
            {
                Assert.HasCount(16, ExpectedNameCodes);

                foreach (var pair in ExpectedNameCodes)
                {
                    var rendered = MarkupRenderer.Render($"[{pair.Key}]x[/]", ColorMode.Colors16);
                    Assert.AreEqual($"\u001b[{pair.Value.Colors16Code}mx\u001b[0m", rendered, $"Foreground mismatch for {pair.Key}");
                }
            })
            .Step("Backgrounds in 16-color mode use the foreground code plus 10", context =>
            {
                foreach (var pair in ExpectedNameCodes)
                {
                    var rendered = MarkupRenderer.Render($"[on {pair.Key}]x[/]", ColorMode.Colors16);
                    Assert.AreEqual($"\u001b[{pair.Value.Colors16Code + 10}mx\u001b[0m", rendered, $"Background mismatch for {pair.Key}");
                }
            })
            .Step("Foregrounds in true color mode use the 38;5;N palette form", context =>
            {
                foreach (var pair in ExpectedNameCodes)
                {
                    var rendered = MarkupRenderer.Render($"[{pair.Key}]x[/]", ColorMode.TrueColor);
                    Assert.AreEqual($"\u001b[38;5;{pair.Value.PaletteIndex}mx\u001b[0m", rendered, $"Foreground mismatch for {pair.Key}");
                }
            })
            .Step("Backgrounds in true color mode use the 48;5;N palette form", context =>
            {
                foreach (var pair in ExpectedNameCodes)
                {
                    var rendered = MarkupRenderer.Render($"[on {pair.Key}]x[/]", ColorMode.TrueColor);
                    Assert.AreEqual($"\u001b[48;5;{pair.Value.PaletteIndex}mx\u001b[0m", rendered, $"Background mismatch for {pair.Key}");
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_darkgreen_renders_palette_22_and_downgrades_to_green()
    {
        await Scenario()
            .Step("True color mode renders darkgreen as palette entry 22", context =>
            {
                Assert.AreEqual("\u001b[38;5;22mx\u001b[0m", MarkupRenderer.Render("[darkgreen]x[/]", ColorMode.TrueColor));
                Assert.AreEqual("\u001b[48;5;22mx\u001b[0m", MarkupRenderer.Render("[on darkgreen]x[/]", ColorMode.TrueColor));
            })
            .Step("16-color mode downgrades darkgreen to green like the previous console library does", context =>
            {
                Assert.AreEqual("\u001b[32mx\u001b[0m", MarkupRenderer.Render("[darkgreen]x[/]", ColorMode.Colors16));
                Assert.AreEqual("\u001b[42mx\u001b[0m", MarkupRenderer.Render("[on darkgreen]x[/]", ColorMode.Colors16));
            })
            .Run();
    }

    [Test]
    public async Task Verify_style_words_render_sgr_flags()
    {
        var expectedStyleCodes = new Dictionary<string, int>
        {
            { "bold", 1 },
            { "b", 1 },
            { "dim", 2 },
            { "italic", 3 },
            { "i", 3 },
            { "underline", 4 },
            { "u", 4 },
            { "reverse", 7 },
            { "invert", 7 },
            { "strikethrough", 9 },
            { "s", 9 }
        };

        await Scenario()
            .Step("Each style word renders its SGR code in both color modes", context =>
            {
                Assert.IsNotEmpty(expectedStyleCodes);

                foreach (var pair in expectedStyleCodes)
                {
                    Assert.AreEqual($"\u001b[{pair.Value}mx\u001b[0m", MarkupRenderer.Render($"[{pair.Key}]x[/]", ColorMode.Colors16), $"16-color mismatch for {pair.Key}");
                    Assert.AreEqual($"\u001b[{pair.Value}mx\u001b[0m", MarkupRenderer.Render($"[{pair.Key}]x[/]", ColorMode.TrueColor), $"True color mismatch for {pair.Key}");
                }
            })
            .Step("Combined flags render in a fixed SGR code order regardless of word order", context =>
            {
                Assert.AreEqual("\u001b[1;4mx\u001b[0m", MarkupRenderer.Render("[bold u]x[/]", ColorMode.Colors16));
                Assert.AreEqual("\u001b[1;4mx\u001b[0m", MarkupRenderer.Render("[u bold]x[/]", ColorMode.Colors16));
                Assert.AreEqual("\u001b[7;9mx\u001b[0m", MarkupRenderer.Render("[s reverse]x[/]", ColorMode.Colors16));
            })
            .Step("default and none are valid no-op words as in the previous console library", context =>
            {
                Assert.AreEqual("x", MarkupRenderer.Render("[default]x[/]", ColorMode.Colors16));
                Assert.AreEqual("x", MarkupRenderer.Render("[none]x[/]", ColorMode.TrueColor));
            })
            .Run();
    }

    [Test]
    public async Task Verify_repo_markup_matches_previous_library_output()
    {
        // Expected strings are byte-for-byte what the previous console library (0.50) emits for
        // the same markup with ANSI forced, in its TrueColor and Standard color systems respectively.
        await Scenario()
            .Step("Panel headers with decoration, foreground and background", context =>
            {
                Assert.AreEqual("\u001b[1;38;5;15;48;5;12m Metadata \u001b[0m", MarkupRenderer.Render("[bold white on blue] Metadata [/]", ColorMode.TrueColor));
                Assert.AreEqual("\u001b[1;97;104m Metadata \u001b[0m", MarkupRenderer.Render("[bold white on blue] Metadata [/]", ColorMode.Colors16));
                Assert.AreEqual("\u001b[1;38;5;15;48;5;22m Step Details \u001b[0m", MarkupRenderer.Render("[bold white on darkgreen] Step Details [/]", ColorMode.TrueColor));
                Assert.AreEqual("\u001b[1;97;42m Step Details \u001b[0m", MarkupRenderer.Render("[bold white on darkgreen] Step Details [/]", ColorMode.Colors16));
            })
            .Step("Bold color combinations", context =>
            {
                Assert.AreEqual("\u001b[1;38;5;2mTestFuzn\u001b[0m", MarkupRenderer.Render("[bold green]TestFuzn[/]", ColorMode.TrueColor));
                Assert.AreEqual("\u001b[1;32mTestFuzn\u001b[0m", MarkupRenderer.Render("[bold green]TestFuzn[/]", ColorMode.Colors16));
                Assert.AreEqual("\u001b[1;38;5;9mInvalid.\u001b[0m", MarkupRenderer.Render("[bold red]Invalid.[/]", ColorMode.TrueColor));
                Assert.AreEqual("\u001b[1;91mInvalid.\u001b[0m", MarkupRenderer.Render("[bold red]Invalid.[/]", ColorMode.Colors16));
            })
            .Step("Multiple tags with plain text between them", context =>
            {
                Assert.AreEqual(
                    "\u001b[1mTotal elapsed Time:\u001b[0m \u001b[38;5;11m00:01\u001b[0m",
                    MarkupRenderer.Render("[bold]Total elapsed Time:[/] [yellow]00:01[/]", ColorMode.TrueColor));
                Assert.AreEqual(
                    "\u001b[1mTotal elapsed Time:\u001b[0m \u001b[93m00:01\u001b[0m",
                    MarkupRenderer.Render("[bold]Total elapsed Time:[/] [yellow]00:01[/]", ColorMode.Colors16));
            })
            .Run();
    }

    [Test]
    public async Task Verify_nested_markup_matches_previous_library_output()
    {
        await Scenario()
            .Step("An inner color temporarily overrides the outer color", context =>
            {
                Assert.AreEqual(
                    "\u001b[38;5;9ma\u001b[0m\u001b[38;5;2mb\u001b[0m\u001b[38;5;9mc\u001b[0m",
                    MarkupRenderer.Render("[red]a[green]b[/]c[/]", ColorMode.TrueColor));
                Assert.AreEqual(
                    "\u001b[91ma\u001b[0m\u001b[32mb\u001b[0m\u001b[91mc\u001b[0m",
                    MarkupRenderer.Render("[red]a[green]b[/]c[/]", ColorMode.Colors16));
            })
            .Step("An inner decoration combines with the outer color", context =>
            {
                Assert.AreEqual(
                    "\u001b[91ma\u001b[0m\u001b[1;91mb\u001b[0m\u001b[91mc\u001b[0m",
                    MarkupRenderer.Render("[red]a[bold]b[/]c[/]", ColorMode.Colors16));
            })
            .Run();
    }

    [Test]
    public async Task Verify_styled_line_breaks_close_and_reopen_the_style()
    {
        await Scenario()
            .Step("A line break inside a styled run closes the style before it and reopens it after", context =>
            {
                Assert.AreEqual(
                    "\u001b[38;5;2ma\u001b[0m\r\n\u001b[38;5;2mb\u001b[0m",
                    MarkupRenderer.Render("[green]a\r\nb[/]", ColorMode.TrueColor));
                Assert.AreEqual(
                    "\u001b[32ma\u001b[0m\n\u001b[32mb\u001b[0m",
                    MarkupRenderer.Render("[green]a\nb[/]", ColorMode.Colors16));
            })
            .Step("A lone carriage return is a break too, so FrameBuffer rows stay style-complete", context =>
            {
                // Deliberate divergence from the previous console library, which leaves a lone \r
                // inside the styled run: FrameBuffer.AddLine splits on \r as well, so the renderer
                // must close the style around it for every stored row to carry its own styling.
                Assert.AreEqual(
                    "\u001b[32ma\u001b[0m\r\u001b[32mb\u001b[0m",
                    MarkupRenderer.Render("[green]a\rb[/]", ColorMode.Colors16));
            })
            .Step("Breaks at the edges of a styled run emit no empty styled segments", context =>
            {
                Assert.AreEqual("\u001b[38;5;2ma\u001b[0m\r\n", MarkupRenderer.Render("[green]a\r\n[/]", ColorMode.TrueColor));
                Assert.AreEqual("\r\n\u001b[38;5;2ma\u001b[0m", MarkupRenderer.Render("[green]\r\na[/]", ColorMode.TrueColor));
                Assert.AreEqual(
                    "\u001b[32ma\u001b[0m\n\n\u001b[32mb\u001b[0m",
                    MarkupRenderer.Render("[green]a\n\nb[/]", ColorMode.Colors16));
            })
            .Step("A break outside the styled run passes through a plain span verbatim", context =>
            {
                Assert.AreEqual(
                    "\u001b[38;5;2mStatus: Completed successfully.\u001b[0m\r\n",
                    MarkupRenderer.Render("[green]Status: Completed successfully.[/]\r\n", ColorMode.TrueColor));
            })
            .Step("A rendered multi-line assert message is style-complete on every physical line", context =>
            {
                Assert.AreEqual(
                    "  \u001b[91mExpected:\u001b[0m\r\n\u001b[91m  <5>\u001b[0m\r\n\u001b[91m  Actual:\u001b[0m\r\n\u001b[91m  <7>\u001b[0m",
                    MarkupRenderer.Render("  [red]Expected:\r\n  <5>\r\n  Actual:\r\n  <7>[/]", ColorMode.Colors16));
            })
            .Step("Line breaks survive monochrome and none rendering unchanged", context =>
            {
                Assert.AreEqual(
                    "\u001b[1ma\u001b[0m\n\u001b[1mb\u001b[0m",
                    MarkupRenderer.Render("[bold]a\nb[/]", ColorMode.Monochrome));
                Assert.AreEqual("a\r\nb", MarkupRenderer.Render("[green]a\r\nb[/]", ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_monochrome_keeps_decorations_and_drops_colors()
    {
        await Scenario()
            .Step("A decoration with a color renders the decoration SGR only", context =>
            {
                Assert.AreEqual("\u001b[1mx\u001b[0m", MarkupRenderer.Render("[bold black]x[/]", ColorMode.Monochrome));
                Assert.AreEqual("\u001b[1m Metadata \u001b[0m", MarkupRenderer.Render("[bold white on blue] Metadata [/]", ColorMode.Monochrome));
                Assert.AreEqual("\u001b[4mScenario\u001b[0m", MarkupRenderer.Render("[u]Scenario[/]", ColorMode.Monochrome));
                Assert.AreEqual("\u001b[1;4mx\u001b[0m", MarkupRenderer.Render("[bold u red]x[/]", ColorMode.Monochrome));
            })
            .Step("Color-only markup renders as plain text without escape codes", context =>
            {
                Assert.AreEqual("x", MarkupRenderer.Render("[red]x[/]", ColorMode.Monochrome));
                Assert.AreEqual("abc", MarkupRenderer.Render("[red]a[green]b[/]c[/]", ColorMode.Monochrome));
            })
            .Step("Hex colors are dropped like named colors", context =>
            {
                Assert.AreEqual("\u001b[1mx\u001b[0m", MarkupRenderer.Render("[bold #ff8800 on #005f00]x[/]", ColorMode.Monochrome));
                Assert.AreEqual("x", MarkupRenderer.Render("[#ff8800]x[/]", ColorMode.Monochrome));
            })
            .Step("None still emits zero escape codes for the same markup", context =>
            {
                var rendered = MarkupRenderer.Render("[bold black]x[/]", ColorMode.None);

                Assert.AreEqual("x", rendered);
                Assert.DoesNotContain("\u001b", rendered);
            })
            .Run();
    }

    [Test]
    public async Task Verify_hex_colors_truecolor_and_downgrade()
    {
        await Scenario()
            .Step("Hex colors render as 24-bit RGB in true color mode", context =>
            {
                Assert.AreEqual("\u001b[38;2;255;136;0mx\u001b[0m", MarkupRenderer.Render("[#ff8800]x[/]", ColorMode.TrueColor));
                Assert.AreEqual("\u001b[48;2;0;95;0mx\u001b[0m", MarkupRenderer.Render("[on #005f00]x[/]", ColorMode.TrueColor));
            })
            .Step("Shorthand hex doubles each digit", context =>
            {
                Assert.AreEqual("\u001b[38;2;255;136;0mx\u001b[0m", MarkupRenderer.Render("[#f80]x[/]", ColorMode.TrueColor));
            })
            .Step("16-color mode downgrades hex to the closest standard color like the previous console library does", context =>
            {
                Assert.AreEqual("\u001b[33mx\u001b[0m", MarkupRenderer.Render("[#ff8800]x[/]", ColorMode.Colors16));
                Assert.AreEqual("\u001b[32mx\u001b[0m", MarkupRenderer.Render("[#005f00]x[/]", ColorMode.Colors16));
                Assert.AreEqual("\u001b[42mx\u001b[0m", MarkupRenderer.Render("[on #005f00]x[/]", ColorMode.Colors16));
                Assert.AreEqual("\u001b[90mx\u001b[0m", MarkupRenderer.Render("[#808080]x[/]", ColorMode.Colors16));
            })
            .Run();
    }

    [Test]
    public async Task Verify_color_mode_none_renders_plain_text()
    {
        await Scenario()
            .Step("No escape codes are emitted at all", context =>
            {
                var rendered = MarkupRenderer.Render("[bold white on blue] Metadata [/]", ColorMode.None);

                Assert.AreEqual(" Metadata ", rendered);
                Assert.DoesNotContain("\u001b", rendered);
            })
            .Step("Decorations are suppressed too", context =>
            {
                Assert.AreEqual("Scenario", MarkupRenderer.Render("[u]Scenario[/]", ColorMode.None));
                Assert.AreEqual("x", MarkupRenderer.Render("[bold #ff8800]x[/]", ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_malformed_markup_rendering()
    {
        await Scenario()
            .Step("An unclosed tag still terminates the output with a reset", context =>
            {
                Assert.AreEqual("\u001b[91mtext\u001b[0m", MarkupRenderer.Render("[red]text", ColorMode.Colors16));
            })
            .Step("A stray closing tag is ignored", context =>
            {
                Assert.AreEqual("ab", MarkupRenderer.Render("a[/]b", ColorMode.Colors16));
            })
            .Step("An unknown tag renders as literal text without escape codes", context =>
            {
                Assert.AreEqual("[notacolor]x", MarkupRenderer.Render("[notacolor]x[/]", ColorMode.Colors16));
            })
            .Step("An unterminated bracket renders as literal text", context =>
            {
                Assert.AreEqual("abc[red", MarkupRenderer.Render("abc[red", ColorMode.TrueColor));
            })
            .Step("Escaped brackets render inside the styled run", context =>
            {
                Assert.AreEqual("\u001b[90ma[b]\u001b[0m", MarkupRenderer.Render("[grey]a[[b]][/]", ColorMode.Colors16));
            })
            .Step("Null and empty markup render to an empty string", context =>
            {
                Assert.AreEqual(string.Empty, MarkupRenderer.Render((string?)null, ColorMode.Colors16));
                Assert.AreEqual(string.Empty, MarkupRenderer.Render(string.Empty, ColorMode.TrueColor));
                Assert.AreEqual(string.Empty, MarkupRenderer.Render((string?)null, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_directly_composed_spans_render()
    {
        await Scenario()
            .Step("Widgets can compose spans without markup strings", context =>
            {
                var spans = new List<StyledSpan>
                {
                    new StyledSpan("OK ", new TerminalStyle { Foreground = TerminalColor.FromRgb(0, 200, 0), Bold = true }),
                    new StyledSpan("1234", TerminalStyle.Plain)
                };

                Assert.AreEqual("\u001b[1;38;2;0;200;0mOK \u001b[0m1234", MarkupRenderer.Render(spans, ColorMode.TrueColor));
            })
            .Step("Composed RGB spans downgrade in 16-color mode", context =>
            {
                var spans = new List<StyledSpan>
                {
                    new StyledSpan("x", new TerminalStyle { Background = TerminalColor.FromRgb(0, 95, 0) })
                };

                Assert.AreEqual("\u001b[42mx\u001b[0m", MarkupRenderer.Render(spans, ColorMode.Colors16));
            })
            .Step("Empty spans render nothing and a null span list is rejected", context =>
            {
                Assert.AreEqual(string.Empty, MarkupRenderer.Render(new List<StyledSpan>(), ColorMode.TrueColor));
                Assert.ThrowsExactly<ArgumentNullException>(() => MarkupRenderer.Render((IReadOnlyList<StyledSpan>)null!, ColorMode.TrueColor));
            })
            .Run();
    }
}
