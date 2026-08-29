using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Golden frames for <see cref="ChartWidget"/>, derived by hand from the sample values: a level
/// is 1 + round(position × (levels − 1)) over the shared finite range, braille rows hold four
/// levels (left sub-column dots 7, 3, 2, 1 = 0x40, 0x04, 0x02, 0x01 bottom-up; right sub-column
/// dots 8, 6, 5, 4 = 0x80, 0x20, 0x10, 0x08) and block rows eight. Plateau series keep every
/// cell one of a few patterns so each frame can be checked by eye against the comments.
/// </summary>
[TestClass]
public class ChartWidgetTests : Test
{
    private const string Green = "38;5;2";
    private const string Red = "38;5;9";
    private const string Yellow = "38;5;11";
    private const string Dim = "2";
    private const string Green16 = "32";
    private const string Red16 = "91";
    private const string Yellow16 = "93";

    private static readonly ChartOptions BodyOnly = new ChartOptions { ShowAxis = false, ShowNewestValue = false };

    /// <summary>
    /// ok: 32 samples at 20 then 32 at 80 (area, green); failed: 0 throughout except samples 40
    /// and 41 at 40 (line, red). Shared scale 0..80 over 24 levels: 20 → level 7 (row 4 gets
    /// three dots, row 5 is full), 80 → 24 (full), 0 → 1 (the bottom dot), 40 → 13 (the bottom
    /// dot of row 2, which is also the mid label's row: (6 − 1) / 2). Axis "80"/"40"/"0" is 3
    /// wide, the annotation " ▶ 80" 5, so the body is 32 characters = 64 columns, one sample
    /// each.
    /// </summary>
    private static ChartSeries[] OkFailedSeries()
    {
        var ok = new double[64];
        var failed = new double[64];
        for (var index = 0; index < 64; index++)
            ok[index] = index < 32 ? 20 : 80;

        failed[40] = 40;
        failed[41] = 40;

        return new[]
        {
            new ChartSeries(ok) { Style = "green", Kind = ChartSeriesKind.Area },
            new ChartSeries(failed) { Style = "red", Kind = ChartSeriesKind.Line }
        };
    }

    /// <summary>
    /// p99 (red), p95 (yellow) and p50 (green) as stacked areas, 70 samples each at 60/40/20
    /// then 70 at 100/70/30. Shared scale 20..100 over 32 levels: 20 → 1, 30 → 5, 40 → 9,
    /// 60 → 17, 70 → 20, 100 → 32. Axis "100"/"60"/"20" is 4 wide, the annotation " ▶ 100" 6,
    /// so the body is 70 characters = 140 columns, one sample each.
    /// </summary>
    private static ChartSeries[] LatencyBandSeries()
    {
        var p99 = new double[140];
        var p95 = new double[140];
        var p50 = new double[140];
        for (var index = 0; index < 140; index++)
        {
            p99[index] = index < 70 ? 60 : 100;
            p95[index] = index < 70 ? 40 : 70;
            p50[index] = index < 70 ? 20 : 30;
        }

        return new[]
        {
            new ChartSeries(p99) { Style = "red" },
            new ChartSeries(p95) { Style = "yellow" },
            new ChartSeries(p50) { Style = "green" }
        };
    }

    [Test]
    public async Task Verify_two_overlaid_series_golden_frame_at_40x6()
    {
        await Scenario()
            .Step("Color mode None renders the area, the line with its joined spike, the axis and the annotation", context =>
            {
                var lines = ChartWidget.Render(OkFailedSeries(), 40, 6, ColorMode.None);

                // The failed line joins 1 → 13 across columns 39/40 (levels 1-7 and 8-13, the tie
                // at 7 to the older column) and 13 → 1 across 41/42 (7-13 and 1-6). A cell the
                // line passes through shows the line alone: row 5 loses the ok area under the
                // baseline except at character 20, which the spike jumps over.
                AssertFrame(new[]
                {
                    "80┤" + new string(' ', 16) + new string('⣿', 16) + " ▶ 80",
                    "  │" + new string(' ', 16) + new string('⣿', 16) + "     ",
                    "40┤" + new string(' ', 16) + "⣿⣿⣿⣿⣀" + new string('⣿', 11) + "     ",
                    "  │" + new string(' ', 16) + new string('⣿', 16) + "     ",
                    "  │" + new string('⣶', 16) + "⣿⣿⣿⢰⠙⡄" + new string('⣿', 10) + "     ",
                    " 0┤" + new string('⣀', 19) + "⣸⣿⣇" + new string('⣀', 10) + "     "
                }, 40, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("TrueColor styles each run in its series' palette color, the axis dim, the annotation like the first series", context =>
            {
                var lines = ChartWidget.Render(OkFailedSeries(), 40, 6, ColorMode.TrueColor);

                AssertFrame(new[]
                {
                    Sgr(Dim, "80┤") + new string(' ', 16) + Sgr(Green, new string('⣿', 16)) + Sgr(Green, " ▶ 80"),
                    "  " + Sgr(Dim, "│") + new string(' ', 16) + Sgr(Green, new string('⣿', 16)) + "     ",
                    Sgr(Dim, "40┤") + new string(' ', 16) + Sgr(Green, "⣿⣿⣿⣿") + Sgr(Red, "⣀") + Sgr(Green, new string('⣿', 11)) + "     ",
                    "  " + Sgr(Dim, "│") + new string(' ', 16) + Sgr(Green, "⣿⣿⣿⣿") + Sgr(Red, "⣿") + Sgr(Green, new string('⣿', 11)) + "     ",
                    "  " + Sgr(Dim, "│") + Sgr(Green, new string('⣶', 16) + "⣿⣿⣿") + Sgr(Red, "⢰⠙⡄") + Sgr(Green, new string('⣿', 10)) + "     ",
                    " " + Sgr(Dim, "0┤") + Sgr(Red, new string('⣀', 19) + "⣸") + Sgr(Green, "⣿") + Sgr(Red, "⣇" + new string('⣀', 10)) + "     "
                }, 40, lines);
            })
            .Step("Colors16 downgrades the palette colors to the classic codes", context =>
            {
                var lines = ChartWidget.Render(OkFailedSeries(), 40, 6, ColorMode.Colors16);

                AssertFrame(new[]
                {
                    Sgr(Dim, "80┤") + new string(' ', 16) + Sgr(Green16, new string('⣿', 16)) + Sgr(Green16, " ▶ 80"),
                    "  " + Sgr(Dim, "│") + new string(' ', 16) + Sgr(Green16, new string('⣿', 16)) + "     ",
                    Sgr(Dim, "40┤") + new string(' ', 16) + Sgr(Green16, "⣿⣿⣿⣿") + Sgr(Red16, "⣀") + Sgr(Green16, new string('⣿', 11)) + "     ",
                    "  " + Sgr(Dim, "│") + new string(' ', 16) + Sgr(Green16, "⣿⣿⣿⣿") + Sgr(Red16, "⣿") + Sgr(Green16, new string('⣿', 11)) + "     ",
                    "  " + Sgr(Dim, "│") + Sgr(Green16, new string('⣶', 16) + "⣿⣿⣿") + Sgr(Red16, "⢰⠙⡄") + Sgr(Green16, new string('⣿', 10)) + "     ",
                    " " + Sgr(Dim, "0┤") + Sgr(Red16, new string('⣀', 19) + "⣸") + Sgr(Green16, "⣿") + Sgr(Red16, "⣇" + new string('⣀', 10)) + "     "
                }, 40, lines);
            })
            .Step("Monochrome keeps the dim axis and drops the colors", context =>
            {
                var lines = ChartWidget.Render(OkFailedSeries(), 40, 6, ColorMode.Monochrome);

                AssertLine(Sgr(Dim, "80┤") + new string(' ', 16) + new string('⣿', 16) + " ▶ 80", 40, lines[0]);
                AssertLine(" " + Sgr(Dim, "0┤") + new string('⣀', 19) + "⣸⣿⣇" + new string('⣀', 10) + "     ", 40, lines[5]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_three_latency_bands_golden_frame_at_80x8()
    {
        await Scenario()
            .Step("Color mode None stacks the three areas under the 100/60/20 axis, the 60 label on row 3 = (8 - 1) / 2 where p99's 60 plateau plots, the p99 annotation on the top row", context =>
            {
                var lines = ChartWidget.Render(LatencyBandSeries(), 80, 8, ColorMode.None);

                AssertFrame(new[]
                {
                    "100┤" + new string(' ', 35) + new string('⣿', 35) + " ▶ 100",
                    "   │" + new string(' ', 35) + new string('⣿', 35) + "      ",
                    "   │" + new string(' ', 35) + new string('⣿', 35) + "      ",
                    " 60┤" + new string('⣀', 35) + new string('⣿', 35) + "      ",
                    "   │" + new string('⣿', 70) + "      ",
                    "   │" + new string('⣿', 70) + "      ",
                    "   │" + new string('⣿', 70) + "      ",
                    " 20┤" + new string('⣿', 70) + "      "
                }, 80, lines);
            })
            .Step("TrueColor gives each cell to the area owning most of its dots, ties to the later series", context =>
            {
                var lines = ChartWidget.Render(LatencyBandSeries(), 80, 8, ColorMode.TrueColor);

                // First half: p99 (17) owns rows 3-5 outright — p95 (9) repaints only the bottom
                // dot of row 5 — and p95 owns rows 6-7, where p50 (1) repaints only the bottom
                // dot. Second half: p99 (32) keeps rows 0-2, p95 (20) fills rows 3-6 and p50 (5)
                // takes row 7 whole while its single dot in row 6 loses to p95.
                AssertFrame(new[]
                {
                    Sgr(Dim, "100┤") + new string(' ', 35) + Sgr(Red, new string('⣿', 35)) + Sgr(Red, " ▶ 100"),
                    "   " + Sgr(Dim, "│") + new string(' ', 35) + Sgr(Red, new string('⣿', 35)) + "      ",
                    "   " + Sgr(Dim, "│") + new string(' ', 35) + Sgr(Red, new string('⣿', 35)) + "      ",
                    " " + Sgr(Dim, "60┤") + Sgr(Red, new string('⣀', 35)) + Sgr(Yellow, new string('⣿', 35)) + "      ",
                    "   " + Sgr(Dim, "│") + Sgr(Red, new string('⣿', 35)) + Sgr(Yellow, new string('⣿', 35)) + "      ",
                    "   " + Sgr(Dim, "│") + Sgr(Red, new string('⣿', 35)) + Sgr(Yellow, new string('⣿', 35)) + "      ",
                    "   " + Sgr(Dim, "│") + Sgr(Yellow, new string('⣿', 70)) + "      ",
                    " " + Sgr(Dim, "20┤") + Sgr(Yellow, new string('⣿', 35)) + Sgr(Green, new string('⣿', 35)) + "      "
                }, 80, lines);
            })
            .Step("Colors16 renders the same bands in the classic codes", context =>
            {
                var lines = ChartWidget.Render(LatencyBandSeries(), 80, 8, ColorMode.Colors16);

                AssertLine(Sgr(Dim, "100┤") + new string(' ', 35) + Sgr(Red16, new string('⣿', 35)) + Sgr(Red16, " ▶ 100"), 80, lines[0]);
                AssertLine(" " + Sgr(Dim, "60┤") + Sgr(Red16, new string('⣀', 35)) + Sgr(Yellow16, new string('⣿', 35)) + "      ", 80, lines[3]);
                AssertLine(" " + Sgr(Dim, "20┤") + Sgr(Yellow16, new string('⣿', 35)) + Sgr(Green16, new string('⣿', 35)) + "      ", 80, lines[7]);
            })
            .Run();
    }

    /// <summary>
    /// 34 samples — 16 at 0, 16 at 100, then two at 50, the midpoint — so the newest value
    /// plots on the mid label's row. Axis "100"/"50"/"0" is 4 wide, the annotation " ▶ 50" 5,
    /// the body 17 characters = 34 columns, one sample each: characters 0-7 at level 1, 8-15
    /// full, 16 at the midpoint level.
    /// </summary>
    private static ChartSeries[] MidpointSeries()
    {
        var values = new double[34];
        for (var index = 0; index < values.Length; index++)
            values[index] = index < 16 ? 0 : (index < 32 ? 100 : 50);

        return new[] { new ChartSeries(values) { Style = "green" } };
    }

    [Test]
    public async Task Verify_mid_label_sits_on_the_midpoint_row_at_either_height_parity()
    {
        await Scenario()
            .Step("Height 6: 50 is level 13 of 24, so row 5 - (13 - 1) / 4 = 2 = (6 - 1) / 2 carries the label, its tick, its gridline and the annotation", context =>
            {
                var options = new ChartOptions { ShowGridlines = true };
                var lines = ChartWidget.Render(MidpointSeries(), 26, 6, ColorMode.None, options: options);

                AssertFrame(new[]
                {
                    "100┤" + new string('┈', 8) + new string('⣿', 8) + "┈" + "     ",
                    "   │" + new string(' ', 8) + new string('⣿', 8) + " " + "     ",
                    " 50┤" + new string('┈', 8) + new string('⣿', 8) + "⣀" + " ▶ 50",
                    "   │" + new string(' ', 8) + new string('⣿', 9) + "     ",
                    "   │" + new string(' ', 8) + new string('⣿', 9) + "     ",
                    "  0┤" + new string('⣀', 8) + new string('⣿', 9) + "     "
                }, 26, lines);

                var styled = ChartWidget.Render(MidpointSeries(), 26, 6, ColorMode.TrueColor, options: options);
                AssertLine(" " + Sgr(Dim, "50┤") + Sgr(Dim, new string('┈', 8)) + Sgr(Green, new string('⣿', 8) + "⣀") + Sgr(Green, " ▶ 50"), 26, styled[2]);
            })
            .Step("Height 4: 50 is level 9 of 16, row 3 - 2 = 1 = (4 - 1) / 2", context =>
            {
                AssertFrame(new[]
                {
                    "100┤" + new string(' ', 8) + new string('⣿', 8) + " " + "     ",
                    " 50┤" + new string(' ', 8) + new string('⣿', 8) + "⣀" + " ▶ 50",
                    "   │" + new string(' ', 8) + new string('⣿', 9) + "     ",
                    "  0┤" + new string('⣀', 8) + new string('⣿', 9) + "     "
                }, 26, ChartWidget.Render(MidpointSeries(), 26, 4, ColorMode.None));
            })
            .Step("Height 5: 50 is level 11 of 20, row 4 - 2 = 2 = (5 - 1) / 2, three dots up its row", context =>
            {
                AssertFrame(new[]
                {
                    "100┤" + new string(' ', 8) + new string('⣿', 8) + " " + "     ",
                    "   │" + new string(' ', 8) + new string('⣿', 8) + " " + "     ",
                    " 50┤" + new string(' ', 8) + new string('⣿', 8) + "⣶" + " ▶ 50",
                    "   │" + new string(' ', 8) + new string('⣿', 9) + "     ",
                    "  0┤" + new string('⣀', 8) + new string('⣿', 9) + "     "
                }, 26, ChartWidget.Render(MidpointSeries(), 26, 5, ColorMode.None));
            })
            .Step("A stretched series ending on the midpoint annotates the mid label's row and no other", context =>
            {
                var lines = ChartWidget.Render(new[] { new ChartSeries(new double[] { 0, 100, 50 }) { Style = "green" } }, 26, 6, ColorMode.None);

                Assert.HasCount(6, lines);
                Assert.StartsWith(" 50┤", lines[2].Text);
                Assert.EndsWith(" ▶ 50", lines[2].Text);
                for (var row = 0; row < lines.Count; row++)
                {
                    Assert.AreEqual(26, lines[row].Width);
                    if (row != 2)
                        Assert.DoesNotContain("▶", lines[row].Text, $"Annotation on row {row}");
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_series_share_one_x_axis()
    {
        await Scenario()
            .Step("A shorter series occupies only the newest columns, its newest sample at the right edge like the longer one's", context =>
            {
                var series = new[]
                {
                    new ChartSeries(new double[] { 0, 1, 2, 3, 4, 5, 6, 7 }) { Style = "green" },
                    new ChartSeries(new double[] { 7, 7 }) { Style = "red", Kind = ChartSeriesKind.Line }
                };

                // Eight columns, one sample each for the long series (levels 1..8); the two
                // samples of the short one land in columns 6 and 7 at level 8.
                AssertFrame(new[] { "  ⣠⠉", "⣠⣾⣿⣿" }, 4, ChartWidget.Render(series, 4, 2, ColorMode.None, options: BodyOnly));
                AssertFrame(new[] { "  " + Sgr(Green, "⣠") + Sgr(Red, "⠉"), Sgr(Green, "⣠⣾⣿⣿") }, 4, ChartWidget.Render(series, 4, 2, ColorMode.TrueColor, options: BodyOnly));
            })
            .Step("When the longest series stretches, a shorter one stretches on the same positions", context =>
            {
                var series = new[]
                {
                    new ChartSeries(new double[] { 0, 0, 0, 7 }),
                    new ChartSeries(new double[] { 7, 7 }) { Kind = ChartSeriesKind.Line }
                };

                // Four positions over eight columns: the short series reads gap, gap, 7, 7, so
                // it draws from column 5 (the first column past position 2) at level 8.
                AssertFrame(new[] { "  ⠈⠉", "⣀⣀⣠⣿" }, 4, ChartWidget.Render(series, 4, 2, ColorMode.None, options: BodyOnly));
            })
            .Run();
    }

    [Test]
    public async Task Verify_stretch_and_scroll_windows()
    {
        await Scenario()
            .Step("A short series stretches across every column, the oldest sample first", context =>
            {
                // Two samples over eight columns interpolate to 0..7, one level per column.
                AssertFrame(new[] { "  ⣠⣾", "⣠⣾⣿⣿" }, 4, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 0, 7 }) }, 4, 2, ColorMode.None, options: BodyOnly));
            })
            .Step("Interpolation follows the samples, not a straight line end to end", context =>
            {
                // 0, 0, 0, 7 over eight columns: 0 up to column 4, then 1, 4 and 7.
                AssertFrame(new[] { "   ⣸", "⣀⣀⣠⣿" }, 4, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 0, 0, 0, 7 }) }, 4, 2, ColorMode.None, options: BodyOnly));
            })
            .Step("A longer series scrolls to the newest samples, which alone set the scale", context =>
                {
                    // The two 100s have scrolled off: the visible 0..7 scale exactly as before.
                    AssertFrame(new[] { "  ⣠⣾", "⣠⣾⣿⣿" }, 4, ChartWidget.Render(
                        new[] { new ChartSeries(new double[] { 100, 100, 0, 1, 2, 3, 4, 5, 6, 7 }) }, 4, 2, ColorMode.None, options: BodyOnly));
                })
            .Step("The time window keeps only the newest samples and stretches them", context =>
            {
                var options = new ChartOptions { ShowAxis = false, ShowNewestValue = false, TimeWindow = 2 };

                AssertFrame(new[] { "  ⣠⣾", "⣠⣾⣿⣿" }, 4, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 0, 0, 0, 7 }) }, 4, 2, ColorMode.None, options: options));
            })
            .Step("A time window below 1 means no limit", context =>
            {
                var options = new ChartOptions { ShowAxis = false, ShowNewestValue = false, TimeWindow = 0 };

                AssertFrame(new[] { "   ⣸", "⣀⣀⣠⣿" }, 4, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 0, 0, 0, 7 }) }, 4, 2, ColorMode.None, options: options));
            })
            .Step("A single sample fills the chart flat at the middle level, a flat line runs across the middle", context =>
            {
                AssertFrame(new[] { "    ", "⣿⣿⣿⣿" }, 4, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 42 }) }, 4, 2, ColorMode.None, options: BodyOnly));
                AssertFrame(new[] { "    ", "⠉⠉⠉⠉" }, 4, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 5, 5, 5 }) { Kind = ChartSeriesKind.Line } }, 4, 2, ColorMode.None, options: BodyOnly));
            })
            .Step("An empty series renders an empty body", context =>
            {
                AssertFrame(new[] { "    ", "    " }, 4, ChartWidget.Render(
                    new[] { new ChartSeries(Array.Empty<double>()) }, 4, 2, ColorMode.None, options: BodyOnly));
                AssertFrame(new[] { "    ", "    " }, 4, ChartWidget.Render(
                    Array.Empty<ChartSeries>(), 4, 2, ColorMode.None, options: BodyOnly));
            })
            .Run();
    }

    [Test]
    public async Task Verify_degenerate_heights()
    {
        await Scenario()
            .Step("Height 0 renders nothing, as does width 0", context =>
            {
                Assert.IsEmpty(ChartWidget.Render(OkFailedSeries(), 40, 0, ColorMode.None));
                Assert.IsEmpty(ChartWidget.Render(OkFailedSeries(), 0, 6, ColorMode.None));
                Assert.IsEmpty(ChartWidget.Render(OkFailedSeries(), 40, -1, ColorMode.None));
            })
            .Step("Height 1 is the sparkline look — four braille levels — with the annotation and no axis", context =>
            {
                // 0..3 stretched over eight columns: levels 1, 1, 2, 2, 3, 3, 4, 4.
                AssertFrame(new[] { "⣀⣤⣶⣿ ▶ 3" }, 8, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 0, 1, 2, 3 }) }, 8, 1, ColorMode.None));
            })
            .Step("Height 2 carries the max and min labels only", context =>
            {
                // Axis "10"/"0" plus " ▶ 10" leaves 2 body columns, so the annotation is dropped:
                // 0..10 stretched over 14 columns is levels 1,2,2,3,3,4,4,5,5,6,6,7,7,8.
                AssertFrame(new[]
                {
                    "10┤   ⢀⣠⣴⣾",
                    " 0┤⣠⣴⣾⣿⣿⣿⣿"
                }, 10, ChartWidget.Render(new[] { new ChartSeries(new double[] { 0, 10 }) }, 10, 2, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_axis_gridlines_and_gaps()
    {
        await Scenario()
            .Step("Gridlines run through the empty cells of the label rows, and NaN samples are gaps that interpolation widens", context =>
            {
                // 1, NaN, 3 over twelve columns: only the first and last column carry a value
                // (level 1 and 12); every row of a 3-row chart is a label row.
                var options = new ChartOptions { ShowGridlines = true };
                var series = new[] { new ChartSeries(new[] { 1, double.NaN, 3 }) { Style = "green", Kind = ChartSeriesKind.Line } };

                AssertFrame(new[]
                {
                    "3┤┈┈┈┈┈⠈",
                    "2┤┈┈┈┈┈┈",
                    "1┤⡀┈┈┈┈┈"
                }, 8, ChartWidget.Render(series, 8, 3, ColorMode.None, options: options));

                var styled = ChartWidget.Render(series, 8, 3, ColorMode.TrueColor, options: options);
                AssertLine(Sgr(Dim, "3┤") + Sgr(Dim, "┈┈┈┈┈") + Sgr(Green, "⠈"), 8, styled[0]);
                AssertLine(Sgr(Dim, "1┤") + Sgr(Green, "⡀") + Sgr(Dim, "┈┈┈┈┈"), 8, styled[2]);
            })
            .Step("Infinities are gaps too and stay out of the scale", context =>
            {
                var series = new[] { new ChartSeries(new[] { double.PositiveInfinity, 0, 7, double.NegativeInfinity }) };

                // Four samples over eight columns: columns 0-2 and 5-7 interpolate from an
                // infinity, only columns 3 (2.0 → level 3) and 4 (5.0 → level 6) draw on the
                // 0..7 scale.
                AssertFrame(new[] { "  ⡄ ", " ⢰⡇ " }, 4, ChartWidget.Render(series, 4, 2, ColorMode.None, options: BodyOnly));
            })
            .Step("Without a finite value the axis is blank ticks and there is no annotation", context =>
            {
                var series = new[] { new ChartSeries(new[] { double.NaN, double.NaN }) };

                AssertFrame(new[] { "┤     ", "┤     ", "┤     " }, 6, ChartWidget.Render(series, 6, 3, ColorMode.None));
            })
            .Step("A custom formatter shapes the labels and its brackets render literally", context =>
            {
                var options = new ChartOptions { ValueFormatter = value => "[" + value.ToString("0") + "]" };

                // "[3]"/"[0]" make a 4-wide axis and " ▶ [3]" would leave no body, so the
                // annotation goes: 0..3 over twelve columns is levels 1,2,2,3,4,4,5,5,6,7,7,8.
                AssertFrame(new[]
                {
                    "[3]┤   ⣀⣴⣾",
                    "[0]┤⣠⣴⣿⣿⣿⣿"
                }, 10, ChartWidget.Render(new[] { new ChartSeries(new double[] { 0, 3 }) }, 10, 2, ColorMode.None, options: options));
            })
            .Step("Control characters in a label sanitize to spaces and never reach the output", context =>
            {
                var options = new ChartOptions { ValueFormatter = value => "\u001b[31m" + value.ToString("0") + "\t" };

                var lines = ChartWidget.Render(new[] { new ChartSeries(new double[] { 0, 3 }) }, 20, 3, ColorMode.None, options: options);

                Assert.HasCount(3, lines);
                foreach (var line in lines)
                {
                    Assert.AreEqual(20, line.Width);
                    Assert.AreEqual(20, line.Text.Length);
                    Assert.DoesNotContain("\u001b", line.Text);
                    Assert.DoesNotContain("\t", line.Text);
                }

                Assert.StartsWith(" [31m3 ┤", lines[0].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_series_compositing_rules()
    {
        await Scenario()
            .Step("A line joins consecutive samples with a run split between their columns", context =>
            {
                // 0 → 7 over two columns: the older column takes levels 1-4, the newer 5-8.
                AssertFrame(new[] { "⢸", "⡇" }, 1, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 0, 7 }) { Kind = ChartSeriesKind.Line } }, 1, 2, ColorMode.None, options: BodyOnly));
            })
            .Step("A cell a line passes through shows the line alone, in the line's style", context =>
            {
                var series = new[]
                {
                    new ChartSeries(new double[] { 3, 3, 3, 3 }) { Style = "green" },
                    new ChartSeries(new double[] { 0, 0, 0, 0 }) { Style = "red", Kind = ChartSeriesKind.Line }
                };

                AssertFrame(new[] { "⣀⣀" }, 2, ChartWidget.Render(series, 2, 1, ColorMode.None, options: BodyOnly));
                AssertFrame(new[] { Sgr(Red, "⣀⣀") }, 2, ChartWidget.Render(series, 2, 1, ColorMode.TrueColor, options: BodyOnly));
            })
            .Step("Lines sharing a cell merge their dots in the later line's style", context =>
            {
                var series = new[]
                {
                    new ChartSeries(new double[] { 0, 0, 0, 0 }) { Style = "green", Kind = ChartSeriesKind.Line },
                    new ChartSeries(new double[] { 3, 3, 3, 3 }) { Style = "red", Kind = ChartSeriesKind.Line }
                };

                AssertFrame(new[] { Sgr(Red, "⣉⣉") }, 2, ChartWidget.Render(series, 2, 1, ColorMode.TrueColor, options: BodyOnly));
            })
            .Step("An area under a taller one shows through once it owns at least half the cell", context =>
            {
                var tall = new ChartSeries(new double[] { 3, 3, 3, 3, 3, 3, 3, 3 }) { Style = "green" };

                // Level 1 of 4 owns two dots of eight: the taller area keeps the cell.
                AssertFrame(new[] { Sgr(Green, "⣿⣿⣿⣿") }, 4, ChartWidget.Render(
                    new[] { tall, new ChartSeries(new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }) { Style = "red" } }, 4, 1, ColorMode.TrueColor, options: BodyOnly));

                // Level 2 owns four of eight: the tie goes to the later series. A first series
                // holding a single 0 pins the scale to 0..3 so the 1s land on level 2; the
                // taller series repaints its one dot.
                var anchor = new ChartSeries(new[] { 0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN });
                AssertFrame(new[] { Sgr(Red, "⣿⣿⣿⣿") }, 4, ChartWidget.Render(
                    new[] { anchor, tall, new ChartSeries(new double[] { 1, 1, 1, 1, 1, 1, 1, 1 }) { Style = "red" } }, 4, 1, ColorMode.TrueColor, options: BodyOnly));
            })
            .Step("An unresolvable style renders unstyled instead of as literal text", context =>
            {
                var series = new[] { new ChartSeries(new double[] { 0, 7 }) { Style = "nonsense" } };
                var options = new ChartOptions { ShowAxis = false, ShowNewestValue = false, AxisStyle = "also nonsense" };

                AssertFrame(new[] { "  ⣠⣾", "⣠⣾⣿⣿" }, 4, ChartWidget.Render(series, 4, 2, ColorMode.TrueColor, options: options));
            })
            .Step("A style with a bracket inside renders unstyled instead of opening a run that leaks past its glyphs", context =>
            {
                var series = new[] { new ChartSeries(new double[] { 0, 1, 2, 3 }) { Style = "green][blue" } };

                AssertFrame(new[] { "⣀⣤⣶⣿ ▶ 3" }, 8, ChartWidget.Render(series, 8, 1, ColorMode.TrueColor));

                var options = new ChartOptions { AxisStyle = "dim][red" };
                AssertFrame(new[] { "10┤   ⢀⣠⣴⣾", " 0┤⣠⣴⣾⣿⣿⣿⣿" }, 10, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 0, 10 }) }, 10, 2, ColorMode.TrueColor, options: options));
            })
            .Step("Null series are rejected while a default series is an empty one", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => ChartWidget.Render(null!, 10, 2, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => new ChartSeries(null!));

                AssertFrame(new[] { "    ", "    " }, 4, ChartWidget.Render(new[] { default(ChartSeries) }, 4, 2, ColorMode.None, options: BodyOnly));
            })
            .Run();
    }

    [Test]
    public async Task Verify_block_glyph_fallback()
    {
        await Scenario()
            .Step("Blocks draw one sample per column at eight levels per row", context =>
            {
                // 0..7 over sixteen levels: 1, 3, 5, 7, 10, 12, 14, 16.
                var series = new[] { new ChartSeries(new double[] { 0, 1, 2, 3, 4, 5, 6, 7 }) };

                AssertFrame(new[] { "    ▂▄▆█", "▁▃▅▇████" }, 8, ChartWidget.Render(series, 8, 2, ColorMode.None, SparklineGlyphSet.Blocks, BodyOnly));
            })
            .Step("A block line shows each sample's partial block in its own row, unjoined", context =>
            {
                var series = new[] { new ChartSeries(new double[] { 0, 1, 2, 3, 4, 5, 6, 7 }) { Kind = ChartSeriesKind.Line } };

                AssertFrame(new[] { "    ▂▄▆█", "▁▃▅▇    " }, 8, ChartWidget.Render(series, 8, 2, ColorMode.None, SparklineGlyphSet.Blocks, BodyOnly));
            })
            .Step("The axis and annotation frame a block body the same way", context =>
            {
                var series = new[] { new ChartSeries(new double[] { 0, 1, 2, 3, 4, 5, 6, 7 }) { Style = "green" } };

                AssertFrame(new[]
                {
                    "7┤    ▂▄▆█ ▶ 7",
                    "0┤▁▃▅▇████    "
                }, 14, ChartWidget.Render(series, 14, 2, ColorMode.None, SparklineGlyphSet.Blocks));

                var styled = ChartWidget.Render(series, 14, 2, ColorMode.TrueColor, SparklineGlyphSet.Blocks);
                AssertLine(Sgr(Dim, "7┤") + "    " + Sgr(Green, "▂▄▆█") + Sgr(Green, " ▶ 7"), 14, styled[0]);
                AssertLine(Sgr(Dim, "0┤") + Sgr(Green, "▁▃▅▇████") + "    ", 14, styled[1]);
            })
            .Step("Height 1 in blocks is the block sparkline look", context =>
            {
                AssertFrame(new[] { "▁▂▃▄▅▆▇█" }, 8, ChartWidget.Render(
                    new[] { new ChartSeries(new double[] { 0, 1, 2, 3, 4, 5, 6, 7 }) }, 8, 1, ColorMode.None, SparklineGlyphSet.Blocks, BodyOnly));
            })
            .Run();
    }

    [Test]
    public async Task Verify_width_degradation_order()
    {
        // Axis "100"/"50"/"0" with its tick is 4 columns and the annotation " ▶ 100" is 6; the
        // goldens below are derived for a four-column minimum body.
        const int axisWidth = 4;
        const int annotationWidth = 6;
        var series = new[] { new ChartSeries(new double[] { 0, 100 }) };

        await Scenario()
            .Step("The annotation goes first when the body would fall below MinimumBodyWidth", context =>
            {
                var width = axisWidth + annotationWidth + ChartWidget.MinimumBodyWidth - 1;
                var lines = ChartWidget.Render(series, width, 3, ColorMode.None);

                Assert.HasCount(3, lines);
                Assert.StartsWith("100┤", lines[0].Text);
                Assert.StartsWith(" 50┤", lines[1].Text);
                Assert.StartsWith("  0┤", lines[2].Text);
                foreach (var line in lines)
                {
                    Assert.AreEqual(width, line.Width);
                    Assert.DoesNotContain("▶", line.Text);
                }
            })
            .Step("Both stay once the body keeps MinimumBodyWidth columns", context =>
            {
                var width = axisWidth + annotationWidth + ChartWidget.MinimumBodyWidth;
                var lines = ChartWidget.Render(series, width, 3, ColorMode.None);

                Assert.StartsWith("100┤", lines[0].Text);
                Assert.EndsWith(" ▶ 100", lines[0].Text);
                Assert.AreEqual(width, lines[0].Width);
            })
            .Step("The axis keeps exactly MinimumBodyWidth body columns before it goes", context =>
            {
                var width = axisWidth + ChartWidget.MinimumBodyWidth;

                // 0..100 over eight columns and twelve levels: 1,3,4,6,7,9,10,12.
                AssertFrame(new[] { "100┤  ⢀⣼", " 50┤ ⢠⣾⣿", "  0┤⣰⣿⣿⣿" }, width, ChartWidget.Render(series, width, 3, ColorMode.None));
            })
            .Step("Then the axis goes, leaving the body the whole width", context =>
            {
                var width = axisWidth + ChartWidget.MinimumBodyWidth - 1;
                var lines = ChartWidget.Render(series, width, 3, ColorMode.None);

                Assert.HasCount(3, lines);
                foreach (var line in lines)
                {
                    Assert.AreEqual(width, line.Width);
                    Assert.AreEqual(width, line.Text.Length);
                    Assert.DoesNotContain("┤", line.Text);
                    Assert.DoesNotContain("│", line.Text);
                    Assert.DoesNotContain("▶", line.Text);
                }

                // 0..100 over fourteen columns and twelve levels: 1,2,3,4,4,5,6,7,8,9,9,10,11,12.
                AssertFrame(new[] { "    ⢀⣠⣾", "  ⢀⣴⣿⣿⣿", "⣠⣾⣿⣿⣿⣿⣿" }, width, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_optional_parts_can_be_turned_off()
    {
        await Scenario()
            .Step("Without the annotation its columns go to the body", context =>
            {
                var series = new[] { new ChartSeries(new double[] { 0, 10 }) };

                // With it: 0..10 over fourteen columns (levels 1,2,2,3,3,4,4,5,5,6,6,7,7,8).
                AssertFrame(new[]
                {
                    "10┤   ⢀⣠⣴⣾ ▶ 10",
                    " 0┤⣠⣴⣾⣿⣿⣿⣿     "
                }, 15, ChartWidget.Render(series, 15, 2, ColorMode.None));

                // Without it: over twenty-four columns (1,1,2,2,2,3,3,3,3,4,4,4,5,5,5,6,6,6,6,7,7,7,8,8).
                AssertFrame(new[]
                {
                    "10┤      ⣀⣠⣤⣴⣶⣿",
                    " 0┤⣀⣤⣴⣶⣾⣿⣿⣿⣿⣿⣿⣿"
                }, 15, ChartWidget.Render(series, 15, 2, ColorMode.None, options: new ChartOptions { ShowNewestValue = false }));
            })
            .Step("A null axis style renders the axis, ticks and gridlines plain while the series keep theirs", context =>
            {
                var blocks = new[] { new ChartSeries(new double[] { 0, 1, 2, 3, 4, 5, 6, 7 }) { Style = "green" } };
                var plainAxis = ChartWidget.Render(blocks, 14, 2, ColorMode.TrueColor, SparklineGlyphSet.Blocks, new ChartOptions { AxisStyle = null });

                AssertLine("7┤    " + Sgr(Green, "▂▄▆█") + Sgr(Green, " ▶ 7"), 14, plainAxis[0]);
                AssertLine("0┤" + Sgr(Green, "▁▃▅▇████") + "    ", 14, plainAxis[1]);

                var gaps = new[] { new ChartSeries(new[] { 1, double.NaN, 3 }) { Style = "green", Kind = ChartSeriesKind.Line } };
                var plainGridlines = ChartWidget.Render(gaps, 8, 3, ColorMode.TrueColor, options: new ChartOptions { ShowGridlines = true, AxisStyle = null });

                AssertLine("3┤┈┈┈┈┈" + Sgr(Green, "⠈"), 8, plainGridlines[0]);
                AssertLine("2┤┈┈┈┈┈┈", 8, plainGridlines[1]);
                AssertLine("1┤" + Sgr(Green, "⡀") + "┈┈┈┈┈", 8, plainGridlines[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_chart_never_throws_and_keeps_its_width_for_hostile_inputs()
    {
        await Scenario()
            .Step("Every width 1-200 and height 0-20 renders height lines of exactly the width, never truncated, with no escape bytes in color mode None", context =>
            {
                var ellipsis = MarkupText.Ellipsis.ToString();
                foreach (var series in HostileSeriesSets())
                {
                    foreach (var glyphSet in new[] { SparklineGlyphSet.Braille, SparklineGlyphSet.Blocks })
                    {
                        for (var width = 1; width <= 200; width++)
                        {
                            for (var height = 0; height <= 20; height++)
                            {
                                var lines = ChartWidget.Render(series.Series, width, height, ColorMode.None, glyphSet, series.Options);

                                Assert.HasCount(height, lines, $"{series.Name} at {width}x{height} ({glyphSet})");
                                for (var index = 0; index < lines.Count; index++)
                                {
                                    Assert.AreEqual(width, lines[index].Width, $"{series.Name} row {index} width at {width}x{height} ({glyphSet})");
                                    Assert.AreEqual(width, lines[index].Text.Length, $"{series.Name} row {index} text length at {width}x{height} ({glyphSet})");
                                    Assert.DoesNotContain(ellipsis, lines[index].Text, $"{series.Name} row {index} truncated at {width}x{height} ({glyphSet})");
                                    Assert.DoesNotContain("\u001b", lines[index].Text, $"{series.Name} row {index} escape at {width}x{height} ({glyphSet})");
                                }
                            }
                        }
                    }
                }
            })
            .Step("TrueColor keeps the declared width, never truncated, at a spread of sizes down the degradation ladder", context =>
            {
                var ellipsis = MarkupText.Ellipsis.ToString();
                foreach (var series in HostileSeriesSets())
                {
                    foreach (var glyphSet in new[] { SparklineGlyphSet.Braille, SparklineGlyphSet.Blocks })
                    {
                        foreach (var width in new[] { 1, 3, 5, 9, 20, 31, 64, 99, 200 })
                        {
                            foreach (var height in new[] { 1, 2, 3, 6, 20 })
                            {
                                var lines = ChartWidget.Render(series.Series, width, height, ColorMode.TrueColor, glyphSet, series.Options);

                                Assert.HasCount(height, lines, $"{series.Name} at {width}x{height} ({glyphSet})");
                                foreach (var line in lines)
                                {
                                    Assert.AreEqual(width, line.Width, $"{series.Name} width at {width}x{height} ({glyphSet})");
                                    Assert.DoesNotContain(ellipsis, line.Text, $"{series.Name} truncated at {width}x{height} ({glyphSet})");
                                }
                            }
                        }
                    }
                }
            })
            .Step("Identical inputs render an identical frame", context =>
            {
                var first = ChartWidget.Render(OkFailedSeries(), 120, 12, ColorMode.TrueColor, options: new ChartOptions { ShowGridlines = true });
                var second = ChartWidget.Render(OkFailedSeries(), 120, 12, ColorMode.TrueColor, options: new ChartOptions { ShowGridlines = true });

                Assert.HasCount(first.Count, second);
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.AreEqual(first[index].Text, second[index].Text, $"Text mismatch at row {index}");
                    Assert.AreEqual(first[index].Width, second[index].Width, $"Width mismatch at row {index}");
                }
            })
            .Run();
    }

    private static IEnumerable<(string Name, ChartSeries[] Series, ChartOptions? Options)> HostileSeriesSets()
    {
        var ramp = new double[500];
        for (var index = 0; index < ramp.Length; index++)
            ramp[index] = index;

        var negative = new double[50];
        for (var index = 0; index < negative.Length; index++)
            negative[index] = -50 + index;

        var flat = new double[30];
        for (var index = 0; index < flat.Length; index++)
            flat[index] = 7;

        var gridlines = new ChartOptions { ShowGridlines = true };
        var windowed = new ChartOptions { TimeWindow = 5, ShowGridlines = true };
        var noAxis = new ChartOptions { ShowAxis = false };

        yield return ("no series", Array.Empty<ChartSeries>(), null);
        yield return ("empty series", new[] { new ChartSeries(Array.Empty<double>()) }, gridlines);
        yield return ("default series", new[] { default(ChartSeries) }, null);
        yield return ("NaN only", new[] { new ChartSeries(new[] { double.NaN, double.NaN, double.NaN }) { Kind = ChartSeriesKind.Line } }, gridlines);
        yield return ("infinities", new[] { new ChartSeries(new[] { 1, double.PositiveInfinity, double.NegativeInfinity, double.NaN, 2 }) { Style = "green" } }, null);
        yield return ("negative ramp", new[] { new ChartSeries(negative) { Style = "red", Kind = ChartSeriesKind.Line } }, gridlines);
        yield return ("flat", new[] { new ChartSeries(flat) { Style = "yellow" } }, windowed);
        yield return ("single sample", new[] { new ChartSeries(new double[] { 42 }) { Kind = ChartSeriesKind.Line } }, null);
        yield return ("extreme spread", new[] { new ChartSeries(new[] { -1e308, 1e308, double.MinValue, double.MaxValue }) }, noAxis);
        yield return ("long ramp", new[] { new ChartSeries(ramp) { Style = "green" }, new ChartSeries(ramp) { Style = "red", Kind = ChartSeriesKind.Line } }, windowed);
        yield return ("mismatched lengths", new[] { new ChartSeries(new double[] { 5, 3 }) { Style = "green" }, new ChartSeries(ramp) { Style = "bold red", Kind = ChartSeriesKind.Line }, new ChartSeries(flat) { Style = "nonsense" } }, gridlines);
        yield return ("ok and failed", OkFailedSeries(), gridlines);
        yield return ("latency bands", LatencyBandSeries(), windowed);
    }

    private static string Sgr(string parameters, string text)
    {
        return "\u001b[" + parameters + "m" + text + "\u001b[0m";
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
