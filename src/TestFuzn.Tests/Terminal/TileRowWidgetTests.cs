using System.Globalization;
using System.Text.RegularExpressions;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Golden frames for <see cref="TileRowWidget"/>, derived by hand: the boxes share the width
/// less the gaps equally (five tiles at 120 columns: 116 / 5 = 23 and one column over, so the
/// first box is 24 wide; three at 60: 58 / 3 = 19 and one over), each box holds its tile's
/// lines at the box width less 4, and the row is as tall as the tallest tile plus the borders.
/// </summary>
[TestClass]
public class TileRowWidgetTests : Test
{
    private const string Dim = "2";
    private const string Bold = "1";
    private const string Green = "38;5;2";
    private const string Red = "38;5;9";
    private const string Yellow = "38;5;11";
    private const string BoldGreen = "1;38;5;2";
    private const string BoldRed = "1;38;5;9";
    private const string BoldYellow = "1;38;5;11";
    private const string Green16 = "32";
    private const string Red16 = "91";
    private const string Yellow16 = "93";
    private const string BoldGreen16 = "1;32";
    private const string BoldRed16 = "1;91";
    private const string BoldYellow16 = "1;93";

    private static readonly ColorMode[] AllColorModes = { ColorMode.None, ColorMode.Monochrome, ColorMode.Colors16, ColorMode.TrueColor };

    /// <summary>
    /// A dashboard's tile row: requests with a good rise and a four-sample trend (two braille
    /// columns at the right), rps with a bad fall, p95 in the warning state with its gauge —
    /// at 19 inner columns the bar has 10 cells, 412 / 500 × 10 = 8.24: eight full cells and a
    /// two-eighths cell, the band from cell 8 — errors at zero in the ok state, and p99 in the
    /// critical state past its limit (a full 13-cell bar ending in the overshoot marker) with
    /// a flat four-sample trend (two middle-level columns).
    /// </summary>
    private static StatTile[] DashboardTiles()
    {
        return new[]
        {
            new StatTile("requests", "12 345") { Delta = new StatTileDelta(12.5, "12.5 %", upIsGood: true), Trend = new double[] { 0, 1, 2, 3 } },
            new StatTile("rps", "1 024") { Delta = new StatTileDelta(-3.1, "3.1 %", upIsGood: true) },
            new StatTile("p95", "412") { Unit = "ms", State = StatTileState.Warning, Gauge = new StatTileGauge(412, 500, "500 ms") },
            new StatTile("errors", "0") { State = StatTileState.Ok, Delta = new StatTileDelta(0, "0", upIsGood: false) },
            new StatTile("p99", "1.2") { Unit = "s", State = StatTileState.Critical, Gauge = new StatTileGauge(1200, 1000, "1 s"), Trend = new double[] { 1, 1, 1, 1 } }
        };
    }

    [Test]
    public async Task Verify_five_tiles_golden_frame_at_120()
    {
        await Scenario()
            .Step("Color mode None boxes the five tiles 24 + 23 × 4 wide with a one-column gap, every box six rows tall", context =>
            {
                var lines = TileRowWidget.Render(DashboardTiles(), 120, ColorMode.None);

                AssertFrame(new[]
                {
                    Row(Top(24), Top(23), Top(23), Top(23), Top(23)),
                    Row(Box("requests" + Spaces(4) + "▲ 12.5 %"), Box("rps" + Spaces(9) + "▼ 3.1 %"), Box("p95" + Spaces(16)), Box("errors" + Spaces(10) + "▲ 0"), Box("p99" + Spaces(16))),
                    Row(Box("12 345" + Spaces(14)), Box("1 024" + Spaces(14)), Box("412 ms" + Spaces(13)), Box("0" + Spaces(18)), Box("1.2 s" + Spaces(14))),
                    Row(Box(Spaces(18) + "⣠⣾"), Box(Spaces(19)), Box("▕████████▎░▏ 500 ms"), Box(Spaces(19)), Box("▕████████████▶▏ 1 s")),
                    Row(Box(Spaces(20)), Box(Spaces(19)), Box(Spaces(19)), Box(Spaces(19)), Box(Spaces(17) + "⣤⣤")),
                    Row(Bottom(24), Bottom(23), Bottom(23), Bottom(23), Bottom(23))
                }, 120, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("TrueColor styles every part in the palette and leaves the borders and padding bare", context =>
            {
                var lines = TileRowWidget.Render(DashboardTiles(), 120, ColorMode.TrueColor);

                AssertFrame(new[]
                {
                    Row(Top(24), Top(23), Top(23), Top(23), Top(23)),
                    Row(
                        Box(Sgr(Dim, "requests") + Spaces(4) + Sgr(Green, "▲ 12.5 %")),
                        Box(Sgr(Dim, "rps") + Spaces(9) + Sgr(Red, "▼ 3.1 %")),
                        Box(Sgr(Dim, "p95") + Spaces(16)),
                        Box(Sgr(Dim, "errors") + Spaces(10) + "▲ 0"),
                        Box(Sgr(Dim, "p99") + Spaces(16))),
                    Row(
                        Box(Sgr(Bold, "12 345") + Spaces(14)),
                        Box(Sgr(Bold, "1 024") + Spaces(14)),
                        Box(Sgr(BoldYellow, "412") + " " + Sgr(Dim, "ms") + Spaces(13)),
                        Box(Sgr(BoldGreen, "0") + Spaces(18)),
                        Box(Sgr(BoldRed, "1.2") + " " + Sgr(Dim, "s") + Spaces(14))),
                    Row(
                        Box(Sgr(Dim, Spaces(18) + "⣠⣾")),
                        Box(Spaces(19)),
                        Box(Sgr(Dim, "▕") + Sgr(Yellow, "████████▎") + Sgr(Dim, "░▏ 500 ms")),
                        Box(Spaces(19)),
                        Box(Sgr(Dim, "▕") + Sgr(Red, "████████████▶") + Sgr(Dim, "▏ 1 s"))),
                    Row(Box(Spaces(20)), Box(Spaces(19)), Box(Spaces(19)), Box(Spaces(19)), Box(Sgr(Red, Spaces(17) + "⣤⣤"))),
                    Row(Bottom(24), Bottom(23), Bottom(23), Bottom(23), Bottom(23))
                }, 120, lines);
            })
            .Step("Colors16 downgrades the palette colours to the classic codes", context =>
            {
                var lines = TileRowWidget.Render(DashboardTiles(), 120, ColorMode.Colors16);

                Assert.HasCount(6, lines);
                AssertLine(Row(
                    Box(Sgr(Dim, "requests") + Spaces(4) + Sgr(Green16, "▲ 12.5 %")),
                    Box(Sgr(Dim, "rps") + Spaces(9) + Sgr(Red16, "▼ 3.1 %")),
                    Box(Sgr(Dim, "p95") + Spaces(16)),
                    Box(Sgr(Dim, "errors") + Spaces(10) + "▲ 0"),
                    Box(Sgr(Dim, "p99") + Spaces(16))), 120, lines[1]);
                AssertLine(Row(
                    Box(Sgr(Bold, "12 345") + Spaces(14)),
                    Box(Sgr(Bold, "1 024") + Spaces(14)),
                    Box(Sgr(BoldYellow16, "412") + " " + Sgr(Dim, "ms") + Spaces(13)),
                    Box(Sgr(BoldGreen16, "0") + Spaces(18)),
                    Box(Sgr(BoldRed16, "1.2") + " " + Sgr(Dim, "s") + Spaces(14))), 120, lines[2]);
                AssertLine(Row(
                    Box(Sgr(Dim, Spaces(18) + "⣠⣾")),
                    Box(Spaces(19)),
                    Box(Sgr(Dim, "▕") + Sgr(Yellow16, "████████▎") + Sgr(Dim, "░▏ 500 ms")),
                    Box(Spaces(19)),
                    Box(Sgr(Dim, "▕") + Sgr(Red16, "████████████▶") + Sgr(Dim, "▏ 1 s"))), 120, lines[3]);
                AssertLine(Row(Box(Spaces(20)), Box(Spaces(19)), Box(Spaces(19)), Box(Spaces(19)), Box(Sgr(Red16, Spaces(17) + "⣤⣤"))), 120, lines[4]);
            })
            .Step("Monochrome keeps bold and dim and drops every colour", context =>
            {
                var lines = TileRowWidget.Render(DashboardTiles(), 120, ColorMode.Monochrome);

                Assert.HasCount(6, lines);
                AssertLine(Row(
                    Box(Sgr(Dim, "requests") + Spaces(4) + "▲ 12.5 %"),
                    Box(Sgr(Dim, "rps") + Spaces(9) + "▼ 3.1 %"),
                    Box(Sgr(Dim, "p95") + Spaces(16)),
                    Box(Sgr(Dim, "errors") + Spaces(10) + "▲ 0"),
                    Box(Sgr(Dim, "p99") + Spaces(16))), 120, lines[1]);
                AssertLine(Row(
                    Box(Sgr(Bold, "12 345") + Spaces(14)),
                    Box(Sgr(Bold, "1 024") + Spaces(14)),
                    Box(Sgr(Bold, "412") + " " + Sgr(Dim, "ms") + Spaces(13)),
                    Box(Sgr(Bold, "0") + Spaces(18)),
                    Box(Sgr(Bold, "1.2") + " " + Sgr(Dim, "s") + Spaces(14))), 120, lines[2]);
                AssertLine(Row(
                    Box(Sgr(Dim, Spaces(18) + "⣠⣾")),
                    Box(Spaces(19)),
                    Box(Sgr(Dim, "▕") + "████████▎" + Sgr(Dim, "░▏ 500 ms")),
                    Box(Spaces(19)),
                    Box(Sgr(Dim, "▕") + "████████████▶" + Sgr(Dim, "▏ 1 s"))), 120, lines[3]);
                AssertLine(Row(Box(Spaces(20)), Box(Spaces(19)), Box(Spaces(19)), Box(Spaces(19)), Box(Spaces(17) + "⣤⣤")), 120, lines[4]);

                foreach (var line in lines)
                    AssertNoColorParameters(line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_three_tiles_golden_frame_at_60()
    {
        // Boxes 20 + 19 + 19: inner widths 16, 15 and 15. "requests" and its delta need 18, so
        // the first tile loses its delta but keeps its trend (two braille columns at the right
        // of its 16); the p95 bar has 6 cells, 412 / 500 × 6 = 4.944 — four full cells and
        // 7.55 eighths, which round up to a fifth full cell — and the band is the last cell,
        // round(4.8) = 5.
        await Scenario()
            .Step("Color mode None: the first tile drops only its delta, and the three-line tiles set the height", context =>
            {
                var lines = TileRowWidget.Render(new[] { DashboardTiles()[0], DashboardTiles()[1], DashboardTiles()[2] }, 60, ColorMode.None);

                AssertFrame(new[]
                {
                    Row(Top(20), Top(19), Top(19)),
                    Row(Box("requests" + Spaces(8)), Box("rps" + Spaces(5) + "▼ 3.1 %"), Box("p95" + Spaces(12))),
                    Row(Box("12 345" + Spaces(10)), Box("1 024" + Spaces(10)), Box("412 ms" + Spaces(9))),
                    Row(Box(Spaces(14) + "⣠⣾"), Box(Spaces(15)), Box("▕█████░▏ 500 ms")),
                    Row(Bottom(20), Bottom(19), Bottom(19))
                }, 60, lines);
            })
            .Step("TrueColor styles the remaining parts", context =>
            {
                var lines = TileRowWidget.Render(new[] { DashboardTiles()[0], DashboardTiles()[1], DashboardTiles()[2] }, 60, ColorMode.TrueColor);

                AssertFrame(new[]
                {
                    Row(Top(20), Top(19), Top(19)),
                    Row(Box(Sgr(Dim, "requests") + Spaces(8)), Box(Sgr(Dim, "rps") + Spaces(5) + Sgr(Red, "▼ 3.1 %")), Box(Sgr(Dim, "p95") + Spaces(12))),
                    Row(Box(Sgr(Bold, "12 345") + Spaces(10)), Box(Sgr(Bold, "1 024") + Spaces(10)), Box(Sgr(BoldYellow, "412") + " " + Sgr(Dim, "ms") + Spaces(9))),
                    Row(Box(Sgr(Dim, Spaces(14) + "⣠⣾")), Box(Spaces(15)), Box(Sgr(Dim, "▕") + Sgr(Yellow, "█████") + Sgr(Dim, "░▏ 500 ms"))),
                    Row(Bottom(20), Bottom(19), Bottom(19))
                }, 60, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_tiles_drop_from_the_right_until_the_rest_fit()
    {
        await Scenario()
            .Step("Five tiles at 40 columns keep the first three: 38 columns share as 13 + 13 + 12, the deltas and the gauge no longer fit but the requests trend does", context =>
            {
                var lines = TileRowWidget.Render(DashboardTiles(), 40, ColorMode.None);

                AssertFrame(new[]
                {
                    Row(Top(13), Top(13), Top(12)),
                    Row(Box("requests "), Box("rps      "), Box("p95     ")),
                    Row(Box("12 345   "), Box("1 024    "), Box("412 ms  ")),
                    Row(Box(Spaces(7) + "⣠⣾"), Box(Spaces(9)), Box(Spaces(8))),
                    Row(Bottom(13), Bottom(13), Bottom(12))
                }, 40, lines);
            })
            .Step("Two tiles at 12 columns keep the first alone in a box of the full width; at 11 not even that fits", context =>
            {
                var tiles = new[] { DashboardTiles()[0], DashboardTiles()[1] };

                AssertFrame(new[] { Top(12), Box("requests"), Box("12 345  "), Box(Spaces(6) + "⣠⣾"), Bottom(12) }, 12, TileRowWidget.Render(tiles, 12, ColorMode.None));
                Assert.IsEmpty(TileRowWidget.Render(tiles, 11, ColorMode.None));
            })
            .Step("A first tile that cannot fit on its own renders nothing; once it fits it is boxed alone", context =>
            {
                var tiles = new[] { new StatTile(new string('w', 30), "1"), new StatTile("s", "1") };

                Assert.IsEmpty(TileRowWidget.Render(tiles, 30, ColorMode.None));
                AssertFrame(new[] { Top(34), Box(new string('w', 30)), Box("1" + Spaces(29)), Bottom(34) }, 34, TileRowWidget.Render(tiles, 34, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_row_edges()
    {
        await Scenario()
            .Step("No tiles, only null tiles, or a width below the minimum box renders nothing; a null list is rejected", context =>
            {
                Assert.IsEmpty(TileRowWidget.Render(Array.Empty<StatTile>(), 80, ColorMode.None));
                Assert.IsEmpty(TileRowWidget.Render(new StatTile[] { null!, null! }, 80, ColorMode.None));
                Assert.IsEmpty(TileRowWidget.Render(DashboardTiles(), TileRowWidget.MinimumBoxWidth - 1, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => TileRowWidget.Render(null!, 80, ColorMode.None));
            })
            .Step("A null entry is skipped and the others lay out as if it were not there", context =>
            {
                var withNull = TileRowWidget.Render(new[] { null!, DashboardTiles()[1] }, 40, ColorMode.None);
                var without = TileRowWidget.Render(new[] { DashboardTiles()[1] }, 40, ColorMode.None);

                Assert.HasCount(without.Count, withNull);
                for (var index = 0; index < without.Count; index++)
                    Assert.AreEqual(without[index].Text, withNull[index].Text, $"Text mismatch at row {index}");
            })
            .Step("The narrowest box holds one column: a blank label above a one-character value", context =>
            {
                AssertFrame(new[] { "╭───╮", "│   │", "│ 1 │", "╰───╯" }, 5, TileRowWidget.Render(new[] { new StatTile(string.Empty, "1") }, 5, ColorMode.None));
            })
            .Step("The glyph set passes through to the trend", context =>
            {
                var tile = new StatTile("t", "1") { Trend = new double[] { 0, 1, 2, 3, 4, 5, 6, 7 } };

                AssertFrame(new[] { Top(12), Box("t" + Spaces(7)), Box("1" + Spaces(7)), Box("▁▂▃▄▅▆▇█"), Bottom(12) }, 12, TileRowWidget.Render(new[] { tile }, 12, ColorMode.None, SparklineGlyphSet.Blocks));
            })
            .Step("Identical inputs render an identical frame", context =>
            {
                var first = TileRowWidget.Render(DashboardTiles(), 120, ColorMode.TrueColor);
                var second = TileRowWidget.Render(DashboardTiles(), 120, ColorMode.TrueColor);

                Assert.HasCount(first.Count, second);
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.AreEqual(first[index].Text, second[index].Text, $"Text mismatch at row {index}");
                    Assert.AreEqual(first[index].Width, second[index].Width, $"Width mismatch at row {index}");
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_tile_row_never_throws_and_keeps_its_width_for_hostile_inputs()
    {
        await Scenario()
            .Step("Every width 1-200 with 0-8 hostile tiles in every color mode renders nothing or a boxed row of lines of exactly the width, never truncated, with no escape bytes in None and no color parameters in Monochrome", context =>
            {
                var ellipsis = MarkupText.Ellipsis.ToString();
                var pool = HostileTiles();
                foreach (var order in new[] { pool, Enumerable.Reverse(pool).ToArray() })
                {
                    for (var count = 0; count <= 8; count++)
                    {
                        var tiles = order.Take(count).ToArray();
                        foreach (var colorMode in AllColorModes)
                        {
                            for (var width = 1; width <= 200; width++)
                            {
                                var lines = TileRowWidget.Render(tiles, width, colorMode);
                                if (lines.Count == 0)
                                    continue;

                                Assert.IsGreaterThanOrEqualTo(4, lines.Count, $"{count} tiles at {width} ({colorMode})");
                                for (var index = 0; index < lines.Count; index++)
                                {
                                    var line = lines[index];
                                    Assert.AreEqual(width, line.Width, $"{count} tiles row {index} width at {width} ({colorMode})");
                                    Assert.DoesNotContain(ellipsis, line.Text, $"{count} tiles row {index} truncated at {width} ({colorMode})");

                                    if (colorMode == ColorMode.None)
                                    {
                                        Assert.AreEqual(width, line.Text.Length, $"{count} tiles row {index} text length at {width}");
                                        Assert.DoesNotContain("\u001b", line.Text, $"{count} tiles row {index} escape at {width}");
                                    }
                                    else if (colorMode == ColorMode.Monochrome)
                                    {
                                        AssertNoColorParameters(line.Text);
                                    }
                                }

                                if (colorMode == ColorMode.None)
                                {
                                    Assert.StartsWith("╭", lines[0].Text, $"{count} tiles at {width}");
                                    Assert.EndsWith("╮", lines[0].Text, $"{count} tiles at {width}");
                                    Assert.StartsWith("╰", lines[lines.Count - 1].Text, $"{count} tiles at {width}");
                                    Assert.EndsWith("╯", lines[lines.Count - 1].Text, $"{count} tiles at {width}");
                                }
                            }
                        }
                    }
                }
            })
            .Run();
    }

    private static StatTile[] HostileTiles()
    {
        var ramp = new double[500];
        for (var index = 0; index < ramp.Length; index++)
            ramp[index] = index;

        return new[]
        {
            new StatTile(string.Empty, string.Empty),
            null!,
            new StatTile(new string('l', 60), "1"),
            new StatTile("x", new string('9', 60)) { Unit = "u" },
            new StatTile("[red]l[/]", "[[v]]") { Unit = "]u[", Delta = new StatTileDelta(double.NaN, "[bold]", upIsGood: true), Gauge = new StatTileGauge(5, -5, "[/]"), Trend = new[] { double.NaN, double.PositiveInfinity } },
            new StatTile("\u001b[2J\t", "a\r\nb") { Unit = "\0", Delta = new StatTileDelta(double.NegativeInfinity, "\u001b[31m", upIsGood: false), Gauge = new StatTileGauge(double.PositiveInfinity, 0, "\r\n") { WarningFraction = double.NaN }, Trend = ramp },
            new StatTile("p95", "412") { Unit = "ms", State = StatTileState.Critical, Delta = new StatTileDelta(1e308, string.Empty, upIsGood: true), Gauge = new StatTileGauge(1e308, 1, "1") { WarningFraction = -1 }, Trend = new[] { -1e308, 1e308 } },
            new StatTile("ok", "0") { State = StatTileState.Ok, Delta = new StatTileDelta(0, "0", upIsGood: false), Gauge = new StatTileGauge(double.NaN, double.NaN, string.Empty) { WarningFraction = 2 }, Trend = Array.Empty<double>() },
            new StatTile("warn", "7") { State = StatTileState.Warning, Gauge = new StatTileGauge(double.NegativeInfinity, double.PositiveInfinity, new string('t', 40)) { WarningFraction = 0 } },
            new StatTile("requests", "12 345") { Delta = new StatTileDelta(12.5, "12.5 %", upIsGood: true), Trend = new double[] { 0, 1, 2, 3 } },
            new StatTile("rps", "120") { State = StatTileState.Ok, Delta = new StatTileDelta(3, "3 %", upIsGood: true), Gauge = new StatTileGauge(120, 50, "50 rps") { Comparison = ThresholdComparison.GreaterThanOrEqualTo }, Trend = ramp }
        };
    }

    private static string Row(params string[] boxes)
    {
        return string.Join(new string(' ', TileRowWidget.Gap), boxes);
    }

    private static string Box(string inner)
    {
        return "│ " + inner + " │";
    }

    private static string Top(int width)
    {
        return "╭" + new string('─', width - 2) + "╮";
    }

    private static string Bottom(int width)
    {
        return "╰" + new string('─', width - 2) + "╯";
    }

    private static string Spaces(int count)
    {
        return new string(' ', count);
    }

    private static string Sgr(string parameters, string text)
    {
        return "\u001b[" + parameters + "m" + text + "\u001b[0m";
    }

    // Monochrome may carry any decoration and reset, never a color: no SGR parameter in the
    // foreground and background ranges (30-49, which also catches the extended 38/48 forms, and
    // the bright 90-107), and every escape is an SGR sequence.
    private static void AssertNoColorParameters(string text)
    {
        var sequences = Regex.Matches(text, AnsiCodes.Escape + "\\[([0-9;]*)m");
        foreach (Match match in sequences)
        {
            foreach (var parameter in match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var code = int.Parse(parameter, CultureInfo.InvariantCulture);
                Assert.IsFalse((code >= 30 && code <= 49) || (code >= 90 && code <= 107), $"Color parameter {code} in Monochrome output: {match.Value}");
            }
        }

        Assert.HasCount(text.Count(character => character == AnsiCodes.Escape[0]), sequences, "Every escape must be an SGR sequence");
    }

    private static void AssertFrame(string[] expectedRows, int expectedWidth, IReadOnlyList<RenderedLine> actualLines)
    {
        Assert.HasCount(expectedRows.Length, actualLines);
        for (var row = 0; row < expectedRows.Length; row++)
        {
            Assert.AreEqual(expectedRows[row], actualLines[row].Text, $"Text mismatch at row {row}");
            Assert.AreEqual(expectedWidth, actualLines[row].Width, $"Width mismatch at row {row}");
        }
    }

    private static void AssertLine(string expectedText, int expectedWidth, RenderedLine actualLine)
    {
        Assert.AreEqual(expectedText, actualLine.Text);
        Assert.AreEqual(expectedWidth, actualLine.Width);
    }
}
