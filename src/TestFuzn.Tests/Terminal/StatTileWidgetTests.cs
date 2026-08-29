using System.Globalization;
using System.Text.RegularExpressions;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Golden lines for <see cref="StatTileWidget"/>, derived by hand: a gauge's fill is
/// Value / Limit × bar cells — the whole cells █ plus one partial cell of the rounded eighths
/// (▏▎▍▌▋▊▉) — and its warning band starts at cell round(WarningFraction × bar cells); a
/// braille trend packs two samples per column at four levels, newest at the right (the
/// sparkline tests spell out the glyphs).
/// </summary>
[TestClass]
public class StatTileWidgetTests : Test
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
    private const string Yellow16 = "93";
    private const string BoldYellow16 = "1;93";

    private static readonly ColorMode[] AllColorModes = { ColorMode.None, ColorMode.Monochrome, ColorMode.Colors16, ColorMode.TrueColor };

    /// <summary>
    /// p95 at 412 ms of a 500 ms limit in the warning state, down 8.3 % where down is good,
    /// with an eight-sample ramp. At width 30 the bar has 21 cells: 412 / 500 × 21 = 17.304, so
    /// 17 full cells and a two-eighths cell (0.304 × 8 = 2.4), the band from cell round(16.8) =
    /// 17; the ramp's levels 1 1 2 2 3 3 4 4 pair into ⣀⣤⣶⣿ in the last four of the 30 columns.
    /// </summary>
    private static StatTile EveryPartTile()
    {
        return new StatTile("p95", "412")
        {
            Unit = "ms",
            State = StatTileState.Warning,
            Delta = new StatTileDelta(-8.3, "8.3 %", upIsGood: false),
            Gauge = new StatTileGauge(412, 500, "500 ms"),
            Trend = new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }
        };
    }

    [Test]
    public async Task Verify_tile_with_every_part_golden_frame_at_width_30()
    {
        var bar = new string('█', 17) + "▎";
        var trend = "⣀⣤⣶⣿";

        await Scenario()
            .Step("Color mode None renders the label with the delta, the value with its unit, the gauge and the trend as plain text", context =>
            {
                var lines = StatTileWidget.Render(EveryPartTile(), 30, ColorMode.None);

                AssertFrame(new[]
                {
                    "p95" + Spaces(20) + "▼ 8.3 %",
                    "412 ms" + Spaces(24),
                    "▕" + bar + "░░░▏ 500 ms",
                    Spaces(26) + trend
                }, 30, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("TrueColor: dim label, ok-green delta, bold yellow value, dim unit, yellow fill on a dim track, yellow trend", context =>
            {
                var lines = StatTileWidget.Render(EveryPartTile(), 30, ColorMode.TrueColor);

                AssertFrame(new[]
                {
                    Sgr(Dim, "p95") + Spaces(20) + Sgr(Green, "▼ 8.3 %"),
                    Sgr(BoldYellow, "412") + " " + Sgr(Dim, "ms") + Spaces(24),
                    Sgr(Dim, "▕") + Sgr(Yellow, bar) + Sgr(Dim, "░░░▏ 500 ms"),
                    Sgr(Yellow, Spaces(26) + trend)
                }, 30, lines);
            })
            .Step("Colors16 downgrades the palette colours to the classic codes", context =>
            {
                var lines = StatTileWidget.Render(EveryPartTile(), 30, ColorMode.Colors16);

                AssertFrame(new[]
                {
                    Sgr(Dim, "p95") + Spaces(20) + Sgr(Green16, "▼ 8.3 %"),
                    Sgr(BoldYellow16, "412") + " " + Sgr(Dim, "ms") + Spaces(24),
                    Sgr(Dim, "▕") + Sgr(Yellow16, bar) + Sgr(Dim, "░░░▏ 500 ms"),
                    Sgr(Yellow16, Spaces(26) + trend)
                }, 30, lines);
            })
            .Step("Monochrome keeps bold and dim and drops every colour", context =>
            {
                var lines = StatTileWidget.Render(EveryPartTile(), 30, ColorMode.Monochrome);

                AssertFrame(new[]
                {
                    Sgr(Dim, "p95") + Spaces(20) + "▼ 8.3 %",
                    Sgr(Bold, "412") + " " + Sgr(Dim, "ms") + Spaces(24),
                    Sgr(Dim, "▕") + bar + Sgr(Dim, "░░░▏ 500 ms"),
                    Spaces(26) + trend
                }, 30, lines);

                foreach (var line in lines)
                    AssertNoColorParameters(line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_delta_arrow_and_colour_follow_the_sign_and_the_good_direction()
    {
        await Scenario()
            .Step("A change in the good direction is ok-green, one in the bad direction failed-red, whichever way is good", context =>
            {
                AssertLine(Sgr(Dim, "rate") + Spaces(8) + Sgr(Green, "▲ 12.5 %"), 20, LabelLine(12.5, "12.5 %", upIsGood: true));
                AssertLine(Sgr(Dim, "rate") + Spaces(9) + Sgr(Red, "▼ 3.1 %"), 20, LabelLine(-3.1, "3.1 %", upIsGood: true));
                AssertLine(Sgr(Dim, "rate") + Spaces(9) + Sgr(Green, "▼ 8.3 %"), 20, LabelLine(-8.3, "8.3 %", upIsGood: false));
                AssertLine(Sgr(Dim, "rate") + Spaces(11) + Sgr(Red, "▲ 5 %"), 20, LabelLine(5, "5 %", upIsGood: false));
            })
            .Step("Zero is an unstyled up arrow, whichever way is good", context =>
            {
                AssertLine(Sgr(Dim, "rate") + Spaces(13) + "▲ 0", 20, LabelLine(0, "0", upIsGood: true));
                AssertLine(Sgr(Dim, "rate") + Spaces(13) + "▲ 0", 20, LabelLine(-0.0, "0", upIsGood: false));
            })
            .Step("NaN draws no delta at all, an infinity draws its arrow, and empty text leaves the arrow alone", context =>
            {
                AssertLine(Sgr(Dim, "rate") + Spaces(16), 20, LabelLine(double.NaN, "x", upIsGood: true));
                AssertLine(Sgr(Dim, "rate") + Spaces(13) + Sgr(Green, "▲ ∞"), 20, LabelLine(double.PositiveInfinity, "∞", upIsGood: true));
                AssertLine(Sgr(Dim, "rate") + Spaces(13) + Sgr(Green, "▼ ∞"), 20, LabelLine(double.NegativeInfinity, "∞", upIsGood: false));
                AssertLine(Sgr(Dim, "rate") + Spaces(15) + Sgr(Green, "▲"), 20, LabelLine(2, "", upIsGood: true));
            })
            .Step("The value of a neutral tile is bold in the default foreground", context =>
            {
                var lines = StatTileWidget.Render(new StatTile("rate", "1") { Delta = new StatTileDelta(2, "2", upIsGood: true) }, 20, ColorMode.TrueColor);

                Assert.HasCount(2, lines);
                AssertLine(Sgr(Bold, "1") + Spaces(19), 20, lines[1]);
            })
            .Run();
    }

    private static RenderedLine LabelLine(double value, string text, bool upIsGood)
    {
        var lines = StatTileWidget.Render(new StatTile("rate", "1") { Delta = new StatTileDelta(value, text, upIsGood) }, 20, ColorMode.TrueColor);

        Assert.HasCount(2, lines);
        return lines[0];
    }

    [Test]
    public async Task Verify_gauge_golden_lines_at_width_20()
    {
        // The limit text "500 ms" and its space leave 20 − 2 − 7 = 11 bar cells; the warning
        // band starts at cell round(0.8 × 11) = 9.
        await Scenario()
            .Step("Ok: 100 of 500 is 2.2 cells — two full cells and a two-eighths cell — then six dots and the two band cells", context =>
            {
                AssertLine("▕██▎······░░▏ 500 ms", 20, GaugeLine(100, 500, StatTileState.Ok, ColorMode.None));
                AssertLine(Sgr(Dim, "▕") + Sgr(Green, "██▎") + Sgr(Dim, "······░░▏ 500 ms"), 20, GaugeLine(100, 500, StatTileState.Ok, ColorMode.TrueColor));
            })
            .Step("Warning: 450 of 500 is 9.9 cells — nine full cells and a seven-eighths cell — reaching into the band", context =>
            {
                AssertLine("▕█████████▉░▏ 500 ms", 20, GaugeLine(450, 500, StatTileState.Warning, ColorMode.None));
                AssertLine(Sgr(Dim, "▕") + Sgr(Yellow, "█████████▉") + Sgr(Dim, "░▏ 500 ms"), 20, GaugeLine(450, 500, StatTileState.Warning, ColorMode.TrueColor));
            })
            .Step("Critical: 490 of 500 is 10.78 cells — ten full cells and a six-eighths cell — leaving no track", context =>
            {
                AssertLine("▕██████████▊▏ 500 ms", 20, GaugeLine(490, 500, StatTileState.Critical, ColorMode.None));
                AssertLine(Sgr(Dim, "▕") + Sgr(Red, "██████████▊") + Sgr(Dim, "▏ 500 ms"), 20, GaugeLine(490, 500, StatTileState.Critical, ColorMode.TrueColor));
            })
            .Step("Past the limit the bar is full and its last cell is the overshoot marker; at the limit exactly it is full without one", context =>
            {
                AssertLine("▕██████████▶▏ 500 ms", 20, GaugeLine(620, 500, StatTileState.Critical, ColorMode.None));
                AssertLine(Sgr(Dim, "▕") + Sgr(Red, "██████████▶") + Sgr(Dim, "▏ 500 ms"), 20, GaugeLine(620, 500, StatTileState.Critical, ColorMode.TrueColor));
                AssertLine("▕███████████▏ 500 ms", 20, GaugeLine(500, 500, StatTileState.Critical, ColorMode.None));
            })
            .Step("A neutral tile fills unstyled between the dim caps, and an empty bar is a single dim span", context =>
            {
                AssertLine("▕█████▌···░░▏ 500 ms", 20, GaugeLine(250, 500, StatTileState.Neutral, ColorMode.None));
                AssertLine(Sgr(Dim, "▕") + "█████▌" + Sgr(Dim, "···░░▏ 500 ms"), 20, GaugeLine(250, 500, StatTileState.Neutral, ColorMode.TrueColor));
                AssertLine("▕·········░░▏ 500 ms", 20, GaugeLine(0, 500, StatTileState.Neutral, ColorMode.None));
                AssertLine(Sgr(Dim, "▕·········░░▏ 500 ms"), 20, GaugeLine(0, 500, StatTileState.Neutral, ColorMode.TrueColor));
            })
            .Step("The value line takes the state's colour in bold with the unit dim", context =>
            {
                var lines = StatTileWidget.Render(new StatTile("p95", "412") { Unit = "ms", State = StatTileState.Ok, Gauge = new StatTileGauge(100, 500, "500 ms") }, 20, ColorMode.TrueColor);

                AssertLine(Sgr(BoldGreen, "412") + " " + Sgr(Dim, "ms") + Spaces(14), 20, lines[1]);
                AssertLine(Sgr(BoldRed, "412") + " " + Sgr(Dim, "ms") + Spaces(14), 20, StatTileWidget.Render(new StatTile("p95", "412") { Unit = "ms", State = StatTileState.Critical }, 20, ColorMode.TrueColor)[1]);
            })
            .Step("A limit that is not finite or not positive, or a NaN or negative value, renders an empty track; an infinite value is overshoot", context =>
            {
                var empty = "▕·········░░▏ 500 ms";
                AssertLine(empty, 20, GaugeLine(5, 0, StatTileState.Neutral, ColorMode.None));
                AssertLine(empty, 20, GaugeLine(5, -5, StatTileState.Neutral, ColorMode.None));
                AssertLine(empty, 20, GaugeLine(5, double.NaN, StatTileState.Neutral, ColorMode.None));
                AssertLine(empty, 20, GaugeLine(5, double.PositiveInfinity, StatTileState.Neutral, ColorMode.None));
                AssertLine(empty, 20, GaugeLine(double.NaN, 500, StatTileState.Neutral, ColorMode.None));
                AssertLine(empty, 20, GaugeLine(-100, 500, StatTileState.Neutral, ColorMode.None));
                AssertLine(empty, 20, GaugeLine(double.NegativeInfinity, 500, StatTileState.Neutral, ColorMode.None));
                AssertLine("▕██████████▶▏ 500 ms", 20, GaugeLine(double.PositiveInfinity, 500, StatTileState.Neutral, ColorMode.None));
            })
            .Step("The warning band follows the fraction: none at 1 or above or NaN, the whole track at 0 or below, at least the last cell in between", context =>
            {
                AssertLine("▕░░░░░░░░░░░▏ 500 ms", 20, GaugeLine(0, 500, StatTileState.Neutral, ColorMode.None, warningFraction: 0));
                AssertLine("▕░░░░░░░░░░░▏ 500 ms", 20, GaugeLine(0, 500, StatTileState.Neutral, ColorMode.None, warningFraction: -1));
                AssertLine("▕···········▏ 500 ms", 20, GaugeLine(0, 500, StatTileState.Neutral, ColorMode.None, warningFraction: 1));
                AssertLine("▕···········▏ 500 ms", 20, GaugeLine(0, 500, StatTileState.Neutral, ColorMode.None, warningFraction: 2));
                AssertLine("▕···········▏ 500 ms", 20, GaugeLine(0, 500, StatTileState.Neutral, ColorMode.None, warningFraction: double.NaN));
                AssertLine("▕······░░░░░▏ 500 ms", 20, GaugeLine(0, 500, StatTileState.Neutral, ColorMode.None, warningFraction: 0.5));
                AssertLine("▕··········░▏ 500 ms", 20, GaugeLine(0, 500, StatTileState.Neutral, ColorMode.None, warningFraction: 0.999));
            })
            .Step("Without limit text the bar takes every column but the caps", context =>
            {
                // 18 cells: 0.2 × 18 = 3.6 — three full cells and a five-eighths cell — the band from cell round(14.4) = 14.
                AssertLine("▕███▋··········░░░░▏", 20, GaugeLine(100, 500, StatTileState.Ok, ColorMode.None, limitText: string.Empty));
            })
            .Step("A remainder below two eighths draws nothing: the one-eighth glyph is the right cap's and would read as a doubled cap", context =>
            {
                // 459 / 500 × 11 = 10.098 (0.78 eighths) and 412 / 500 × 11 = 9.064 (0.51 eighths) both round to one eighth, drawn as none.
                AssertLine("▕██████████░▏ 500 ms", 20, GaugeLine(459, 500, StatTileState.Warning, ColorMode.None));
                AssertLine("▕█████████░░▏ 500 ms", 20, GaugeLine(412, 500, StatTileState.Warning, ColorMode.None));
            })
            .Run();
    }

    private static RenderedLine GaugeLine(double current, double limit, StatTileState state, ColorMode colorMode, string limitText = "500 ms", double? warningFraction = null)
    {
        var gauge = new StatTileGauge(current, limit, limitText);
        if (warningFraction != null)
            gauge = new StatTileGauge(current, limit, limitText) { WarningFraction = warningFraction.Value };

        var lines = StatTileWidget.Render(new StatTile("p95", "412") { Unit = "ms", State = state, Gauge = gauge }, 20, colorMode);

        Assert.HasCount(3, lines);
        return lines[2];
    }

    [Test]
    public async Task Verify_minimum_gauge_golden_lines_at_width_20()
    {
        // A minimum of 50 rps with the 0.8 warning fraction: the bar spans 0 → 62.5 over the
        // same 11 cells, with the band at cells 9-10 as for a maximum — so the band is the
        // evaluator's warning zone [50, 62.5).
        await Scenario()
            .Step("Ok: 120 of 50 is past the top of the band — a full bar, never an overshoot marker", context =>
            {
                AssertLine("▕███████████▏ 50 rps", 20, MinimumGaugeLine(120, 50, StatTileState.Ok, ColorMode.None));
                AssertLine(Sgr(Dim, "▕") + Sgr(Green, new string('█', 11)) + Sgr(Dim, "▏ 50 rps"), 20, MinimumGaugeLine(120, 50, StatTileState.Ok, ColorMode.TrueColor));
                AssertLine("▕███████████▏ 50 rps", 20, MinimumGaugeLine(double.PositiveInfinity, 50, StatTileState.Ok, ColorMode.None));
            })
            .Step("Warning: 60 of 50 is 60 / 62.5 × 11 = 10.56 cells — ten full cells and a four-eighths cell ending inside the band", context =>
            {
                AssertLine("▕██████████▌▏ 50 rps", 20, MinimumGaugeLine(60, 50, StatTileState.Warning, ColorMode.None));
                AssertLine(Sgr(Dim, "▕") + Sgr(Yellow, "██████████▌") + Sgr(Dim, "▏ 50 rps"), 20, MinimumGaugeLine(60, 50, StatTileState.Warning, ColorMode.TrueColor));
            })
            .Step("Breached: 31 of 50 is 31 / 62.5 × 11 = 5.456 cells — five full cells and a four-eighths cell ending below the band", context =>
            {
                AssertLine("▕█████▌···░░▏ 50 rps", 20, MinimumGaugeLine(31, 50, StatTileState.Critical, ColorMode.None));
                AssertLine(Sgr(Dim, "▕") + Sgr(Red, "█████▌") + Sgr(Dim, "···░░▏ 50 rps"), 20, MinimumGaugeLine(31, 50, StatTileState.Critical, ColorMode.TrueColor));
            })
            .Step("At the limit exactly the fill ends where the band starts: 50 / 62.5 × 11 = 8.8 cells", context =>
            {
                AssertLine("▕████████▊░░▏ 50 rps", 20, MinimumGaugeLine(50, 50, StatTileState.Warning, ColorMode.None));
            })
            .Step("A fraction that cannot size a band makes the bar span the limit itself, with the band drawn as for a maximum", context =>
            {
                AssertLine("▕███████████▏ 50 rps", 20, MinimumGaugeLine(60, 50, StatTileState.Ok, ColorMode.None, warningFraction: 1));
                AssertLine("▕█████▌░░░░░▏ 50 rps", 20, MinimumGaugeLine(25, 50, StatTileState.Critical, ColorMode.None, warningFraction: 0));
                AssertLine("▕█████▌·····▏ 50 rps", 20, MinimumGaugeLine(25, 50, StatTileState.Critical, ColorMode.None, warningFraction: double.NaN));
            })
            .Step("A limit that is not positive gives a minimum no scale either", context =>
            {
                AssertLine("▕·········░░▏ 50 rps", 20, MinimumGaugeLine(5, 0, StatTileState.Neutral, ColorMode.None));
                AssertLine("▕·········░░▏ 50 rps", 20, MinimumGaugeLine(5, -50, StatTileState.Neutral, ColorMode.None));
            })
            .Run();
    }

    private static RenderedLine MinimumGaugeLine(double current, double limit, StatTileState state, ColorMode colorMode, double? warningFraction = null)
    {
        var gauge = new StatTileGauge(current, limit, "50 rps") { Comparison = ThresholdComparison.GreaterThanOrEqualTo };
        if (warningFraction != null)
            gauge = new StatTileGauge(current, limit, "50 rps") { Comparison = ThresholdComparison.GreaterThanOrEqualTo, WarningFraction = warningFraction.Value };

        var lines = StatTileWidget.Render(new StatTile("rps", "60") { State = state, Gauge = gauge }, 20, colorMode);

        Assert.HasCount(3, lines);
        return lines[2];
    }

    [Test]
    public async Task Verify_optional_parts_show_independently_and_minimum_width()
    {
        var requests = new StatTile("requests", "12 345") { Delta = new StatTileDelta(12.5, "12.5 %", upIsGood: true), Trend = new double[] { 0, 1, 2, 3 } };
        var p95 = new StatTile("p95", "412") { Unit = "ms", Gauge = new StatTileGauge(412, 500, "500 ms"), Trend = new double[] { 0, 1, 2, 3 } };

        await Scenario()
            .Step("At 18 columns the label, the delta and the trend all fit", context =>
            {
                AssertFrame(new[] { "requests  ▲ 12.5 %", "12 345" + Spaces(12), Spaces(16) + "⣠⣾" }, 18, StatTileWidget.Render(requests, 18, ColorMode.None));
            })
            .Step("At 17 the delta no longer fits beside the label and goes alone: the trend stays", context =>
            {
                AssertFrame(new[] { "requests" + Spaces(9), "12 345" + Spaces(11), Spaces(15) + "⣠⣾" }, 17, StatTileWidget.Render(requests, 17, ColorMode.None));
            })
            .Step("The minimum width is the wider of the label and the value line; below it nothing renders", context =>
            {
                Assert.AreEqual(8, StatTileWidget.MeasureMinimumWidth(requests));
                AssertFrame(new[] { "requests", "12 345  ", Spaces(6) + "⣠⣾" }, 8, StatTileWidget.Render(requests, 8, ColorMode.None));
                Assert.IsEmpty(StatTileWidget.Render(requests, 7, ColorMode.None));
                Assert.IsEmpty(StatTileWidget.Render(requests, 0, ColorMode.None));

                Assert.AreEqual(6, StatTileWidget.MeasureMinimumWidth(p95));
                Assert.AreEqual(13, StatTileWidget.MeasureMinimumWidth(new StatTile("x", "1 234 567 890")));
                Assert.AreEqual(4, StatTileWidget.MeasureMinimumWidth(new StatTile(string.Empty, "7") { Unit = "ms" }));
                Assert.AreEqual(2, StatTileWidget.MeasureMinimumWidth(new StatTile(string.Empty, string.Empty) { Unit = "ms" }));
                Assert.AreEqual(0, StatTileWidget.MeasureMinimumWidth(new StatTile(string.Empty, string.Empty)));
            })
            .Step("The gauge needs its caps, four bar cells and the limit text: it fits at 13 and goes alone at 12, the trend staying", context =>
            {
                // Four cells: 412 / 500 × 4 = 3.296 — three full cells and a two-eighths cell.
                AssertFrame(new[] { "p95" + Spaces(10), "412 ms" + Spaces(7), "▕███▎▏ 500 ms", Spaces(11) + "⣠⣾" }, 13, StatTileWidget.Render(p95, 13, ColorMode.None));
                AssertFrame(new[] { "p95" + Spaces(9), "412 ms" + Spaces(6), Spaces(10) + "⣠⣾" }, 12, StatTileWidget.Render(p95, 12, ColorMode.None));
            })
            .Step("Every part goes exactly when it stops fitting and the others stay: the full tile at 12 keeps its delta and its trend without the gauge, at 11 only the trend", context =>
            {
                // "p95", the gap and "▼ 8.3 %" need 12 columns, the gauge 13; the eight-sample ramp takes the last four columns.
                AssertFrame(new[] { "p95  ▼ 8.3 %", "412 ms" + Spaces(6), Spaces(8) + "⣀⣤⣶⣿" }, 12, StatTileWidget.Render(EveryPartTile(), 12, ColorMode.None));
                AssertFrame(new[] { "p95" + Spaces(8), "412 ms" + Spaces(5), Spaces(7) + "⣀⣤⣶⣿" }, 11, StatTileWidget.Render(EveryPartTile(), 11, ColorMode.None));
            })
            .Step("The trend needs four columns, and an empty trend is a blank row that keeps the tile's height", context =>
            {
                var tile = new StatTile("ab", "1") { Trend = new double[] { 0, 1, 2, 3 } };
                AssertFrame(new[] { "ab ", "1  " }, 3, StatTileWidget.Render(tile, 3, ColorMode.None));
                AssertFrame(new[] { "ab  ", "1   ", "  ⣠⣾" }, 4, StatTileWidget.Render(tile, 4, ColorMode.None));

                var empty = new StatTile("ab", "1") { Trend = Array.Empty<double>() };
                AssertFrame(new[] { "ab" + Spaces(4), "1" + Spaces(5), Spaces(6) }, 6, StatTileWidget.Render(empty, 6, ColorMode.None));
                AssertLine(Sgr(Dim, Spaces(6)), 6, StatTileWidget.Render(empty, 6, ColorMode.TrueColor)[2]);
            })
            .Step("The trend follows the caller's glyph set and takes the state's colour", context =>
            {
                var tile = new StatTile("ab", "1") { State = StatTileState.Ok, Trend = new double[] { 0, 1, 2, 3, 4, 5, 6, 7 } };

                AssertLine(Sgr(Green, "▁▂▃▄▅▆▇█"), 8, StatTileWidget.Render(tile, 8, ColorMode.TrueColor, SparklineGlyphSet.Blocks)[2]);
            })
            .Step("A null tile is rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => StatTileWidget.Render(null!, 10, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => StatTileWidget.MeasureMinimumWidth(null!));
            })
            .Run();
    }

    [Test]
    public async Task Verify_texts_are_escaped_and_sanitized()
    {
        await Scenario()
            .Step("Brackets render literally and control characters become spaces, in every text of the tile", context =>
            {
                var tile = new StatTile("[bold]x", "1\t2")
                {
                    Unit = "[/]",
                    Delta = new StatTileDelta(1, "\u001b[2J", upIsGood: true),
                    Gauge = new StatTileGauge(1, 2, "a\r\nb")
                };

                var lines = StatTileWidget.Render(tile, 24, ColorMode.None);

                // The label measures 7, the delta 6 (the escape byte is one space); the limit
                // text "a b" leaves 18 cells, half of them filled, the band from cell 14.
                AssertFrame(new[]
                {
                    "[bold]x" + Spaces(11) + "▲  [2J",
                    "1 2 [/]" + Spaces(17),
                    "▕" + new string('█', 9) + "·····░░░░▏ a b"
                }, 24, lines);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain("\u001b", line.Text);
                    Assert.AreEqual(24, line.Text.Length);
                }
            })
            .Step("A tag in the label never styles the value, and a closing tag in the value never leaks the value's colour", context =>
            {
                var lines = StatTileWidget.Render(new StatTile("[dim]a", "[/]b") { State = StatTileState.Warning }, 8, ColorMode.TrueColor);

                AssertFrame(new[] { Sgr(Dim, "[dim]a") + Spaces(2), Sgr(BoldYellow, "[/]b") + Spaces(4) }, 8, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_stat_tile_never_throws_and_keeps_its_width_for_hostile_inputs()
    {
        await Scenario()
            .Step("Every width 0-200 in every color mode and glyph set renders two to four lines of exactly the width from the minimum width up and nothing below it, never truncated, with no escape bytes in None and no color parameters in Monochrome", context =>
            {
                var ellipsis = MarkupText.Ellipsis.ToString();
                foreach (var input in HostileTiles())
                {
                    var minimum = StatTileWidget.MeasureMinimumWidth(input.Tile);
                    foreach (var colorMode in AllColorModes)
                    {
                        foreach (var glyphSet in new[] { SparklineGlyphSet.Braille, SparklineGlyphSet.Blocks })
                        {
                            for (var width = 0; width <= 200; width++)
                            {
                                var lines = StatTileWidget.Render(input.Tile, width, colorMode, glyphSet);

                                if (width < 1 || minimum > width)
                                {
                                    Assert.IsEmpty(lines, $"{input.Name} at {width} ({colorMode}, {glyphSet})");
                                    continue;
                                }

                                Assert.IsGreaterThanOrEqualTo(2, lines.Count, $"{input.Name} at {width} ({colorMode}, {glyphSet})");
                                Assert.IsLessThanOrEqualTo(4, lines.Count, $"{input.Name} at {width} ({colorMode}, {glyphSet})");
                                for (var index = 0; index < lines.Count; index++)
                                {
                                    var line = lines[index];
                                    Assert.AreEqual(width, line.Width, $"{input.Name} row {index} width at {width} ({colorMode}, {glyphSet})");
                                    Assert.DoesNotContain(ellipsis, line.Text, $"{input.Name} row {index} truncated at {width} ({colorMode}, {glyphSet})");

                                    if (colorMode == ColorMode.None)
                                    {
                                        Assert.AreEqual(width, line.Text.Length, $"{input.Name} row {index} text length at {width} ({glyphSet})");
                                        Assert.DoesNotContain("\u001b", line.Text, $"{input.Name} row {index} escape at {width} ({glyphSet})");
                                    }
                                    else if (colorMode == ColorMode.Monochrome)
                                    {
                                        AssertNoColorParameters(line.Text);
                                    }
                                }
                            }
                        }
                    }
                }
            })
            .Step("Identical inputs render an identical frame", context =>
            {
                var first = StatTileWidget.Render(EveryPartTile(), 40, ColorMode.TrueColor);
                var second = StatTileWidget.Render(EveryPartTile(), 40, ColorMode.TrueColor);

                Assert.HasCount(first.Count, second);
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.AreEqual(first[index].Text, second[index].Text, $"Text mismatch at row {index}");
                    Assert.AreEqual(first[index].Width, second[index].Width, $"Width mismatch at row {index}");
                }
            })
            .Run();
    }

    private static IEnumerable<(string Name, StatTile Tile)> HostileTiles()
    {
        var ramp = new double[500];
        for (var index = 0; index < ramp.Length; index++)
            ramp[index] = index;

        yield return ("empty", new StatTile(string.Empty, string.Empty));
        yield return ("long value", new StatTile("x", new string('9', 60)) { Unit = new string('u', 20) });
        yield return ("long label", new StatTile(new string('l', 60), "1"));
        yield return ("brackets", new StatTile("[red]l[/]", "[[v]]") { Unit = "]u[", Delta = new StatTileDelta(1, "[bold]", upIsGood: true), Gauge = new StatTileGauge(1, 2, "[/]") });
        yield return ("controls", new StatTile("\u001b[2J\t", "a\r\nb") { Unit = "\0", Delta = new StatTileDelta(-1, "\u001b[31m", upIsGood: false), Gauge = new StatTileGauge(3, 4, "\r\n"), Trend = new double[] { 1, 2 } });
        yield return ("nan delta", new StatTile("d", "1") { Delta = new StatTileDelta(double.NaN, "x", upIsGood: true) });
        yield return ("positive infinite delta", new StatTile("d", "1") { Delta = new StatTileDelta(double.PositiveInfinity, "∞", upIsGood: false) });
        yield return ("negative infinite delta", new StatTile("d", "1") { Delta = new StatTileDelta(double.NegativeInfinity, "∞", upIsGood: true) });
        yield return ("huge delta", new StatTile("d", "1") { Delta = new StatTileDelta(1e308, new string('9', 50), upIsGood: true) });
        yield return ("tiny delta", new StatTile("d", "1") { Delta = new StatTileDelta(-1e-308, "0.0", upIsGood: true) });
        yield return ("negative zero delta", new StatTile("d", "1") { Delta = new StatTileDelta(-0.0, "0", upIsGood: false) });
        yield return ("empty delta text", new StatTile("d", "1") { Delta = new StatTileDelta(1, string.Empty, upIsGood: true) });
        yield return ("nan trend", new StatTile("t", "1") { Trend = new[] { double.NaN, double.NaN } });
        yield return ("infinite trend", new StatTile("t", "1") { Trend = new[] { 1, double.PositiveInfinity, double.NegativeInfinity, double.NaN, 2 } });
        yield return ("single sample trend", new StatTile("t", "1") { Trend = new double[] { 42 } });
        yield return ("long trend", new StatTile("t", "1") { Trend = ramp });
        yield return ("extreme trend", new StatTile("t", "1") { Trend = new[] { -1e308, 1e308, double.MinValue, double.MaxValue } });
        yield return ("empty trend", new StatTile("t", "1") { Trend = Array.Empty<double>() });
        yield return ("negative limit", new StatTile("g", "1") { Gauge = new StatTileGauge(5, -5, "x") });
        yield return ("zero limit", new StatTile("g", "1") { Gauge = new StatTileGauge(5, 0, "0") });
        yield return ("nan limit", new StatTile("g", "1") { Gauge = new StatTileGauge(5, double.NaN, "?") });
        yield return ("infinite limit", new StatTile("g", "1") { Gauge = new StatTileGauge(5, double.PositiveInfinity, "∞") });
        yield return ("over limit", new StatTile("g", "1") { Gauge = new StatTileGauge(1e308, 1, "1") });
        yield return ("nan value", new StatTile("g", "1") { Gauge = new StatTileGauge(double.NaN, 1, "1") });
        yield return ("positive infinite value", new StatTile("g", "1") { Gauge = new StatTileGauge(double.PositiveInfinity, 1, "1") });
        yield return ("negative infinite value", new StatTile("g", "1") { Gauge = new StatTileGauge(double.NegativeInfinity, 1, "1") });
        yield return ("long limit text", new StatTile("g", "1") { Gauge = new StatTileGauge(1, 2, new string('t', 60)) });
        yield return ("empty limit text", new StatTile("g", "1") { Gauge = new StatTileGauge(1, 2, string.Empty) });
        yield return ("minimum gauge", new StatTile("rps", "120") { State = StatTileState.Ok, Gauge = new StatTileGauge(120, 50, "50 rps") { Comparison = ThresholdComparison.GreaterThanOrEqualTo } });
        yield return ("minimum gauge below", new StatTile("rps", "31") { State = StatTileState.Critical, Gauge = new StatTileGauge(31, 50, "50 rps") { Comparison = ThresholdComparison.GreaterThanOrEqualTo } });
        yield return ("minimum gauge infinite", new StatTile("rps", "∞") { Gauge = new StatTileGauge(double.PositiveInfinity, 50, "50 rps") { Comparison = ThresholdComparison.GreaterThanOrEqualTo } });
        yield return ("minimum gauge negative", new StatTile("rps", "-1") { Gauge = new StatTileGauge(double.NegativeInfinity, 50, "50 rps") { Comparison = ThresholdComparison.GreaterThanOrEqualTo } });
        yield return ("minimum gauge negative limit", new StatTile("rps", "1") { Gauge = new StatTileGauge(5, -5, "x") { Comparison = ThresholdComparison.GreaterThanOrEqualTo } });
        yield return ("minimum gauge nan limit", new StatTile("rps", "1") { Gauge = new StatTileGauge(5, double.NaN, "?") { Comparison = ThresholdComparison.GreaterThanOrEqualTo } });

        foreach (var fraction in new[] { double.NaN, 0, 1, -1, 2, 1e-9, 0.999, double.PositiveInfinity })
        {
            yield return ("warning fraction " + fraction.ToString(CultureInfo.InvariantCulture), new StatTile("g", "1") { Gauge = new StatTileGauge(1, 2, "2") { WarningFraction = fraction } });
            yield return ("minimum warning fraction " + fraction.ToString(CultureInfo.InvariantCulture), new StatTile("g", "1") { Gauge = new StatTileGauge(1, 2, "2") { Comparison = ThresholdComparison.GreaterThanOrEqualTo, WarningFraction = fraction } });
        }

        foreach (var state in new[] { StatTileState.Neutral, StatTileState.Ok, StatTileState.Warning, StatTileState.Critical })
        {
            yield return ("everything " + state, new StatTile("p95", "412")
            {
                Unit = "ms",
                State = state,
                Delta = new StatTileDelta(-8.3, "8.3 %", upIsGood: false),
                Gauge = new StatTileGauge(412, 500, "500 ms"),
                Trend = ramp
            });
        }
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
