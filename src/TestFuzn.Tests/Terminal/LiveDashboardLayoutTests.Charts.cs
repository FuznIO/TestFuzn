using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// The chart, heatmap, height-order and phase-label goldens of <see cref="LiveDashboardLayout"/>,
/// over the snapshots of the main file: the threshold limit lines and their legend, the single
/// height drop order step by step, what a 10-to-14-row window shows, the title line carrying
/// the phase when no timeline does, and the gaps an idle interval leaves. Derived by hand as
/// the main file's summary describes; the flat series here keep every cell one of a few
/// braille patterns.
/// </summary>
public partial class LiveDashboardLayoutTests
{
    /// <summary>
    /// A run whose target answers nothing: three ring samples without a request or a latency,
    /// every series zero and every bucket vector empty, no plan — the console manager's view
    /// of a stalled scenario past init.
    /// </summary>
    private static LiveMetricsSnapshot IdleSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Idle run",
            Phase = LoadTestPhase.Measurement,
            PhaseLabel = "Fixed Load 100 rps",
            Duration = TimeSpan.FromSeconds(3),
            Samples = new[] { Sample(1, 0, 0, 0), Sample(2, 0, 0, 0), Sample(3, 0, 0, 0) },
            RequestsPerSecondSeries = new double[3],
            OkDeltaSeries = new double[3],
            FailedDeltaSeries = new double[3],
            ResponseTimePercentile95Series = new double[3],
            ResponseTimeMedianSeries = new double[3],
            ResponseTimePercentile99Series = new double[3],
            LatencyBucketSeries = new IReadOnlyList<int>[] { new int[LatencyBuckets.Count], new int[LatencyBuckets.Count], new int[LatencyBuckets.Count] }
        };
    }

    /// <summary>The steady run's flat twelve samples at 100 rps and 40 ms with the given thresholds.</summary>
    private static LiveMetricsSnapshot FlatSnapshot(params LiveThreshold[] thresholds)
    {
        return SteadySnapshot(Repeat(100, 12), Repeat(40, 12), thresholds: thresholds);
    }

    [Test]
    public async Task Verify_declared_latency_thresholds_draw_limit_lines_with_a_legend()
    {
        await Scenario()
            .Step("Both a p95 and a p99 threshold: two flat lines share the bands' scale, the p99 limit topping it, and the title names both limits", context =>
            {
                // The threshold snapshot's bands: p95 40 ms × 11 then 412, the median half
                // and the p99 1.2 times that — 20 and 48 flat, then 206 and 494.4 — under the
                // limits 500 and 800 over the same twelve samples. The scale runs from the
                // median's 20 to the p99 limit's 800, 33.9 ms a level: the 800 line is the
                // top dot of row 0 (⠉ in every cell), the 500 line level 15 — the third dot
                // of row 2, ⠒ — and the flat stretch sits at levels 1 (p50), 2 (p95) and 2
                // (p99): the median line takes the cell, ⣀ in row 5, and hides the bands'
                // second dot under it. The axis "800 ms" / "410 ms" / "20 ms" is 7 columns
                // and the annotation " ▶ 494 ms" 9, so the body is 39 characters = 78
                // columns and sample i sits at column 7 i; the last seven columns climb to
                // the newest sample. The median line reads 1, 2, 3, 3, 4, 5, 6, 6 over
                // columns 70-77 — never more than a level a column, so no joining dots — and
                // owns every cell it passes: ⡠⠒⠁ closing row 5 (cells 35-37: levels 1 and 2,
                // 3 and 3, 4 alone) and ⢀⠤ closing row 4 (cells 37-38: level 5 in the right
                // column, then 6 and 6). The bands climb to p95 13 and p99 15: row 5's last
                // cell is the p95 fill, ⣿; row 4's cell 36 is ⣴ — p99 at 6 and 7 under p95
                // at 5 and 6, three p95 dots to two — and row 3 is ⣰⣿ (p99 at 9 and 11 over
                // p95 at 8 and 9, then p95 at 11 and 13 under p99 at 13 and 15). The
                // requests chart is 100 × 11 then 112 on a 0..112 scale: level 22, two dots
                // of row 0, then ⣴⣶⣿ up to the newest. The tiles take six rows here (the rps
                // gauge and trend), so the charts are rows 9-16.
                var lines = LiveDashboardLayout.Render(new[] { ThresholdSnapshot() }, 120, 40, ColorMode.None);

                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle + " · limits 500 ms / 800 ms", 59), 120, lines[9]);
                AssertLine(Box("112┤" + Glyphs('⣤', 42) + "⣴⣶⣿ ▶ 112") + "  " + Box("800 ms┤" + Glyphs('⠉', 39) + Spaces(9)), 120, lines[10]);
                AssertLine(Box("   │" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("      │" + Spaces(48)), 120, lines[11]);
                AssertLine(Box(" 56┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("410 ms┤" + Glyphs('⠒', 39) + " ▶ 494 ms"), 120, lines[12]);
                AssertLine(Box("   │" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("      │" + Spaces(37) + "⣰⣿" + Spaces(9)), 120, lines[13]);
                AssertLine(Box("   │" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("      │" + Spaces(36) + "⣴⢀⠤" + Spaces(9)), 120, lines[14]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box(" 20 ms┤" + Glyphs('⣀', 35) + "⡠⠒⠁⣿" + Spaces(9)), 120, lines[15]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, lines[16]);
            })
            .Step("TrueColor: the p95 line is yellow and the p99 line red, as their limits in the title, the bands keep their own colours and the median line its own over them", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { ThresholdSnapshot() }, 120, 40, ColorMode.TrueColor);

                Assert.Contains(Sgr(Dim, "· limits") + " " + Sgr(Yellow, "500 ms") + " " + Sgr(Dim, "/") + " " + Sgr(Red, "800 ms"), lines[9].Text);
                Assert.Contains(Sgr(Dim, "800 ms┤") + Sgr(Red, Glyphs('⠉', 39)), lines[10].Text);
                Assert.Contains(Sgr(Dim, "410 ms┤") + Sgr(Yellow, Glyphs('⠒', 39)) + Sgr(Percentile99, " ▶ 494 ms"), lines[12].Text);
                Assert.Contains(Spaces(37) + Sgr(Percentile99, "⣰") + Sgr(Percentile95, "⣿"), lines[13].Text);
                Assert.Contains(Spaces(36) + Sgr(Percentile95, "⣴") + Sgr(Median, "⢀⠤"), lines[14].Text);
                Assert.Contains(" " + Sgr(Dim, "20 ms┤") + Sgr(Median, Glyphs('⣀', 35) + "⡠⠒⠁") + Sgr(Percentile95, "⣿"), lines[15].Text);
            })
            .Step("A p99 threshold alone: one red line, a 'limit' title, and the annotation on the bands' row", context =>
            {
                // Flat 100 rps and 40 ms: the bands sit at levels 2 and 2 on the 20..800
                // scale under the median line at level 1, which takes every bottom-row
                // cell (⣀), and the p99 limit tops the scale (⠉ in the top row). The axis
                // is 7 columns and " ▶ 48 ms" 8, so the body is 40; the p99 tile is the
                // only gauge, so the tiles take five rows and the charts are 8-15.
                var snapshot = FlatSnapshot(Judged(ThresholdMetric.ResponseTimePercentile99, 800, ThresholdComparison.LessThanOrEqualTo, 48, ThresholdState.Ok));

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 120, 40, ColorMode.None);

                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle + " · limit 800 ms", 59), 120, lines[8]);
                AssertLine(Box("100┤" + Glyphs('⣿', 45) + " ▶ 100") + "  " + Box("800 ms┤" + Glyphs('⠉', 40) + Spaces(8)), 120, lines[9]);
                AssertLine(Box(" 50┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("410 ms┤" + Spaces(48)), 120, lines[11]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box(" 20 ms┤" + Glyphs('⣀', 40) + " ▶ 48 ms"), 120, lines[14]);

                var styled = LiveDashboardLayout.Render(new[] { snapshot }, 120, 40, ColorMode.TrueColor);
                Assert.Contains(Sgr(Dim, "· limit") + " " + Sgr(Red, "800 ms"), styled[8].Text);
                Assert.Contains(Sgr(Red, Glyphs('⠉', 40)), styled[9].Text);
                Assert.Contains(Sgr(Median, Glyphs('⣀', 40)) + Sgr(Percentile99, " ▶ 48 ms"), styled[14].Text);
            })
            .Step("A p95 threshold alone: one yellow line at the top of a 20..500 scale", context =>
            {
                // The p95 tile carries a gauge and a trend, so the tiles take six rows and
                // the charts are 9-16.
                var snapshot = FlatSnapshot(Judged(ThresholdMetric.ResponseTimePercentile95, 500, ThresholdComparison.LessThanOrEqualTo, 40, ThresholdState.Ok));

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 120, 40, ColorMode.None);

                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle + " · limit 500 ms", 59), 120, lines[9]);
                AssertLine(Box("100┤" + Glyphs('⣿', 45) + " ▶ 100") + "  " + Box("500 ms┤" + Glyphs('⠉', 40) + Spaces(8)), 120, lines[10]);
                AssertLine(Box(" 50┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("260 ms┤" + Spaces(48)), 120, lines[12]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box(" 20 ms┤" + Glyphs('⣀', 40) + " ▶ 48 ms"), 120, lines[15]);

                var styled = LiveDashboardLayout.Render(new[] { snapshot }, 120, 40, ColorMode.TrueColor);
                Assert.Contains(Sgr(Dim, "· limit") + " " + Sgr(Yellow, "500 ms"), styled[9].Text);
                Assert.Contains(Sgr(Yellow, Glyphs('⠉', 40)), styled[10].Text);
            })
            .Step("No latency threshold: no line and no limit in the title, even with other thresholds declared", context =>
            {
                var snapshot = FlatSnapshot(Judged(ThresholdMetric.RequestsPerSecond, 50, ThresholdComparison.GreaterThanOrEqualTo, 100, ThresholdState.Ok));

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 120, 40, ColorMode.None);

                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle, 59), 120, lines[9]);
                foreach (var line in lines)
                {
                    Assert.DoesNotContain("⠉", line.Text);
                    Assert.DoesNotContain("limit", line.Text);
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_idle_intervals_are_gaps_in_the_bands_and_blanks_in_the_heatmap()
    {
        await Scenario()
            .Step("A run without a request: the rps chart's zeros fill the lower half at the middle level, the latency chart and the heatmap are blank behind their labels", context =>
            {
                // Three zero samples have no spread, so every level is the middle one and
                // the ok area fills rows 3-5; the axis reads 0 on every label row and the
                // annotation sits on the middle level's row. No latency is a gap, so the
                // latency chart has no finite value: tick-only axis, no labels, no
                // annotation. An empty bucket vector is a blank heatmap column. The tiles
                // carry a trend row at 36 rows, no plan means no timeline, so the phase is
                // on the title line and the charts are rows 7-14, the heatmap 15-24.
                var lines = LiveDashboardLayout.Render(new[] { IdleSnapshot() }, 120, 36, ColorMode.None);

                Assert.HasCount(36, lines);
                AssertLine("Idle run  ● Running · Fixed Load 100 rps" + Spaces(67) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle, 59), 120, lines[7]);
                AssertLine(Box("0┤" + Spaces(53)) + "  " + Box("┤" + Spaces(54)), 120, lines[8]);
                AssertLine(Box(" │" + Spaces(53)) + "  " + Box("│" + Spaces(54)), 120, lines[9]);
                AssertLine(Box("0┤" + Spaces(53)) + "  " + Box("┤" + Spaces(54)), 120, lines[10]);
                AssertLine(Box(" │" + Glyphs('⣿', 49) + " ▶ 0") + "  " + Box("│" + Spaces(54)), 120, lines[11]);
                AssertLine(Box(" │" + Glyphs('⣿', 49) + Spaces(4)) + "  " + Box("│" + Spaces(54)), 120, lines[12]);
                AssertLine(Box("0┤" + Glyphs('⣿', 49) + Spaces(4)) + "  " + Box("┤" + Spaces(54)), 120, lines[13]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, lines[14]);
                AssertLine(PanelTop(HeatmapTitle, 120), 120, lines[15]);
                AssertLine(Box("  > 30 s " + Spaces(107)), 120, lines[16]);
                AssertLine(Box("≤ 200 ms " + Spaces(107)), 120, lines[20]);
                AssertLine(Box(" ≤ 50 ms " + Spaces(107)), 120, lines[21]);
                AssertLine(Box("  ≤ 2 ms " + Spaces(107)), 120, lines[23]);
                AssertLine(Bottom(120), 120, lines[24]);
                AssertLine(PanelTop("Requests", 120), 120, lines[25]);
                AssertLine(Bottom(120), 120, lines[29]);
                AssertLine(string.Empty, 0, lines[30]);
                AssertLine("q quit", 6, lines[35]);
            })
            .Step("An idle interval between two live ones is a gap in every band, never a dip to zero, and a blank heatmap column", context =>
            {
                // 100 rps and 40 ms, nothing, then 100 rps and 40 ms again: the bands are
                // 48 / 40 / 20 flat with a gap in the middle, on a 20..48 scale (p99 level
                // 24, p95 17, p50 1). Three samples over 82 columns put the outer ones at
                // columns 0 and 81 and every other column interpolates from the gap, so each
                // band row is the left half of its first cell (⡇) and the right half of its
                // last (⢸) with 39 blank cells between, and the bottom row the median line's
                // single dot at each end — ⡀ and ⢀, the gap leaving it no neighbour to join
                // — over the bands' fill. The axis "48 ms" / "34 ms" / "20 ms" is 6 columns
                // and " ▶ 48 ms" 8, so the body is 41. The heatmap's three columns are two
                // full ≤ 50 ms cells around a blank one.
                var lines = LiveDashboardLayout.Render(new[] { SteadySnapshot(new double[] { 100, 0, 100 }, new double[] { 40, 0, 40 }) }, 120, 40, ColorMode.None);

                Assert.EndsWith(Box("48 ms┤⡇" + Spaces(39) + "⢸ ▶ 48 ms"), lines[9].Text);
                Assert.EndsWith(Box("     │⡇" + Spaces(39) + "⢸" + Spaces(8)), lines[10].Text);
                Assert.EndsWith(Box("34 ms┤⡇" + Spaces(39) + "⢸" + Spaces(8)), lines[11].Text);
                Assert.EndsWith(Box("     │⡇" + Spaces(39) + "⢸" + Spaces(8)), lines[13].Text);
                Assert.EndsWith(Box("20 ms┤⡀" + Spaces(39) + "⢀" + Spaces(8)), lines[14].Text);
                Assert.Contains(" ▶ 100 │  │ ", lines[9].Text);
                AssertLine(Box(" ≤ 50 ms " + Spaces(104) + "# #"), 120, lines[22]);

                var styled = LiveDashboardLayout.Render(new[] { SteadySnapshot(new double[] { 100, 0, 100 }, new double[] { 40, 0, 40 }) }, 120, 40, ColorMode.TrueColor);
                Assert.Contains(Sgr(Dim, "48 ms┤") + Sgr(Percentile99, "⡇") + Spaces(39) + Sgr(Percentile99, "⢸") + Sgr(Percentile99, " ▶ 48 ms"), styled[9].Text);
                Assert.Contains(Sgr(Dim, "│") + Sgr(Percentile95, "⡇") + Spaces(39) + Sgr(Percentile95, "⢸"), styled[13].Text);
                Assert.Contains(Sgr(Dim, "20 ms┤") + Sgr(Median, "⡀") + Spaces(39) + Sgr(Median, "⢀"), styled[14].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_height_drop_order()
    {
        await Scenario()
            .Step("At 36 rows the heatmap stays and the tables pay for it: the step rows, the error rows, then the step table go, the error ticker keeps its frame", context =>
            {
                // 8 header rows, 8 chart rows, 10 heatmap rows, 6 requests rows, 5 step rows
                // and 4 error rows are 41 against 35: minus two step rows, two error rows and
                // the three-row step table is 34, one blank row before the footer.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 36, ColorMode.None);

                Assert.HasCount(36, lines);
                AssertLine(PanelTop(HeatmapTitle, 120), 120, lines[16]);
                AssertLine(PanelTop("Requests", 120), 120, lines[26]);
                AssertLine(PanelTop("Errors", 120), 120, lines[32]);
                AssertLine(Bottom(120), 120, lines[33]);
                AssertLine(string.Empty, 0, lines[34]);
                AssertLine("q quit", 6, lines[35]);
                foreach (var line in lines)
                    Assert.DoesNotContain("Steps", line.Text);
            })
            .Step("At 35 rows the heatmap goes and both tables come back whole", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 35, ColorMode.None);

                AssertLine(PanelTop("Requests", 120), 120, lines[16]);
                AssertLine(PanelTop("Steps", 120), 120, lines[22]);
                AssertLine("│ Add to cart   6252   71  18 ms  40 ms       2  █░░░░ <0.1%" + Spaces(58) + " │", 120, lines[24]);
                AssertLine("│ Checkout      6260  9.6  58 ms  90 ms      30  █░░░░ 0.5%" + Spaces(59) + " │", 120, lines[25]);
                AssertLine("│ 30× Checkout · Connection refused (localhost:7058)" + Spaces(66) + " │", 120, lines[28]);
                AssertLine(Bottom(120), 120, lines[30]);
                AssertLine("q quit", 6, lines[34]);
                foreach (var line in lines)
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
            })
            .Step("At 30 rows the chart bodies are six rows and the step rows go; at 29 the bodies are four rows, the trends go with them, and one step row is back", context =>
            {
                var tall = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 30, ColorMode.None);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, tall[15]);
                AssertLine(Box(StepsHeaderOnly + Spaces(74)), 120, tall[23]);
                AssertLine("│  2× Add to cart · Timeout after 30s" + Spaces(81) + " │", 120, tall[27]);

                var compact = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 29, ColorMode.None);
                AssertLine(Row(Bottom(24), Bottom(23), Bottom(23), Bottom(23), Bottom(23)), 120, compact[5]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle, 59), 120, compact[8]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, compact[13]);
                AssertLine("│ Add to cart   6252   71  18 ms  40 ms       2  █░░░░ <0.1%" + Spaces(58) + " │", 120, compact[22]);
                AssertLine(Bottom(120), 120, compact[23]);
                AssertLine("│ 30× Checkout · Connection refused (localhost:7058)" + Spaces(66) + " │", 120, compact[25]);
                AssertLine("q quit", 6, compact[28]);
                foreach (var line in compact)
                    Assert.DoesNotContain("│ Checkout ", line.Text);
            })
            .Step("At 24 rows the four-row charts stay and the error ticker keeps only its frame; at 20 the charts go too and the gauges stay", context =>
            {
                var withCharts = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 24, ColorMode.None);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle, 59), 120, withCharts[8]);
                AssertLine(PanelTop("Requests", 120), 120, withCharts[14]);
                AssertLine(PanelTop("Errors", 120), 120, withCharts[20]);
                AssertLine(Bottom(120), 120, withCharts[21]);
                AssertLine(string.Empty, 0, withCharts[22]);

                var without = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 20, ColorMode.None);
                AssertLine(Row(Box(Spaces(20)), Box(Spaces(19)), Box(Spaces(19)), Box(Spaces(19)), Box("▕████▌·····▏ 2m 45s")), 120, without[4]);
                AssertLine(PanelTop("Requests", 120), 120, without[8]);
                AssertLine(Bottom(120), 120, without[13]);
                AssertLine(string.Empty, 0, without[14]);
                AssertLine("q quit", 6, without[19]);
                foreach (var line in without)
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
            })
            .Step("A window of 12 to 14 rows shows the title with the phase, the two-line tiles and the requests panel whole; 10 or 11 rows cut the requests panel after its header lines", context =>
            {
                var fourteen = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 14, ColorMode.None);
                Assert.HasCount(14, fourteen);
                AssertLine("Checkout flow  ● Running · sim 1/2: Gradual Load 10→50 rps" + Spaces(49) + "  ⚡ TestFuzn", 120, fourteen[0]);
                AssertLine(Row(Bottom(24), Bottom(23), Bottom(23), Bottom(23), Bottom(23)), 120, fourteen[4]);
                AssertLine(string.Empty, 0, fourteen[5]);
                AssertLine(PanelTop("Requests", 120), 120, fourteen[6]);
                AssertLine("│ failed     32  1.0  88 ms  102 ms  99 ms  110 ms  140 ms  160 ms  201 ms" + Spaces(44) + " │", 120, fourteen[10]);
                AssertLine(Bottom(120), 120, fourteen[11]);
                AssertLine(string.Empty, 0, fourteen[12]);
                AssertLine("q quit", 6, fourteen[13]);

                var twelve = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 12, ColorMode.None);
                AssertLine("│ failed     32  1.0  88 ms  102 ms  99 ms  110 ms  140 ms  160 ms  201 ms" + Spaces(44) + " │", 120, twelve[10]);
                AssertLine("q quit", 6, twelve[11]);

                var eleven = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 11, ColorMode.None);
                AssertLine("│ ok      12480  141  12 ms   38 ms  35 ms   48 ms   72 ms   94 ms  312 ms" + Spaces(44) + " │", 120, eleven[9]);
                AssertLine("q quit", 6, eleven[10]);

                var ten = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 10, ColorMode.None);
                AssertLine(PanelTop("Requests", 120), 120, ten[6]);
                AssertLine("│ warmup 1200 ok · 3 failed" + Spaces(91) + " │", 120, ten[7]);
                AssertLine("│         count  rps    min    mean    p50     p75     p95     p99     max" + Spaces(44) + " │", 120, ten[8]);
                AssertLine("q quit", 6, ten[9]);
            })
            .Step("Wrapped tiles reach the budget steps past the tables: at 60×36 the heatmap goes although the height allows it, at 60×35 the latency chart follows", context =>
            {
                // 13 header rows (title, two tile rows of 4 and 5, a two-line timeline, a
                // blank), two 8-row stacked charts, the 10-row heatmap, 6 requests rows and
                // the 9 table rows are 54 against 35: the tables' 9 go, then the heatmap's
                // 10 — exactly the budget. One row shorter the heatmap is gone by height and
                // the 44 rows are ten over: the tables' 9, then the latency chart's 8.
                var heatmapAllowed = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 60, 36, ColorMode.None);
                AssertLine(PanelTop(RequestsChartTitle, 60), 60, heatmapAllowed[13]);
                AssertLine(PanelTop(LatencyChartTitle, 60), 60, heatmapAllowed[21]);
                AssertLine(PanelTop("Requests", 60), 60, heatmapAllowed[29]);
                AssertLine(Bottom(60), 60, heatmapAllowed[34]);
                AssertLine("q quit", 6, heatmapAllowed[35]);
                foreach (var line in heatmapAllowed)
                    Assert.DoesNotContain(HeatmapTitle, line.Text);

                var shorter = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 60, 35, ColorMode.None);
                AssertLine(PanelTop(RequestsChartTitle, 60), 60, shorter[13]);
                AssertLine(PanelTop("Requests", 60), 60, shorter[21]);
                AssertLine(string.Empty, 0, shorter[27]);
                AssertLine("q quit", 6, shorter[34]);
                foreach (var line in shorter)
                    Assert.DoesNotContain(LatencyChartTitle, line.Text);
            })
            .Step("The heatmap needs the charts' width: at 59 columns and 40 rows neither shows", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 59, 40, ColorMode.None);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
                }
            })
            .Step("An unbounded height keeps every panel: heatmap, six-row charts, every step and error row", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 0, ColorMode.None);

                Assert.HasCount(42, lines);
                AssertLine(PanelTop(HeatmapTitle, 120), 120, lines[16]);
                AssertLine("│ Checkout      6260  9.6  58 ms  90 ms      30  █░░░░ 0.5%" + Spaces(59) + " │", 120, lines[35]);
                AssertLine("│  2× Add to cart · Timeout after 30s" + Spaces(81) + " │", 120, lines[39]);
                AssertLine("q quit", 6, lines[41]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_phase_label_falls_back_to_the_title_line()
    {
        await Scenario()
            .Step("Without a timeline the phase follows the badge after a dim dot while the line keeps its width, and is dropped one column short", context =>
            {
                // "Ramp  ● Running · Gradual Load 10→50 rps" is exactly 40 columns.
                var fitting = LiveDashboardLayout.Render(new[] { RampSnapshot() }, 40, 0, ColorMode.None);
                AssertLine("Ramp  ● Running · Gradual Load 10→50 rps", 40, fitting[0]);

                var dropped = LiveDashboardLayout.Render(new[] { RampSnapshot() }, 39, 0, ColorMode.None);
                AssertLine("Ramp  ● Running", 15, dropped[0]);

                var styled = LiveDashboardLayout.Render(new[] { RampSnapshot() }, 40, 0, ColorMode.TrueColor);
                AssertLine(Sgr(Bold, "Ramp") + "  " + Sgr(Yellow, "● Running") + " " + Sgr(Dim, "·") + " " + Sgr("38;2;255;207;107", "Gradual Load 10→50 rps"), 40, styled[0]);
            })
            .Step("The name and the badge come first: a title too wide for the window truncates as before, without the phase", context =>
            {
                var snapshot = new LiveMetricsSnapshot { ScenarioName = new string('n', 30), PhaseLabel = "init" };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 40, 0, ColorMode.None);

                AssertLine(new string('n', 30) + "  ● Runni…", 40, lines[0]);
            })
            .Step("Beside the logo the title keeps the logo's columns: the phase fits the columns left of it or goes", context =>
            {
                var fitting = LiveDashboardLayout.Render(new[] { FailedSnapshot() }, 80, 0, ColorMode.None);
                AssertLine("Checkout flow  ● Failed · completed" + Spaces(32) + "  ⚡ TestFuzn", 80, fitting[0]);

                var snapshot = new LiveMetricsSnapshot { ScenarioName = new string('n', 40), PhaseLabel = "sim 1/2: Gradual Load 10→50 rps" };
                var dropped = LiveDashboardLayout.Render(new[] { snapshot }, 80, 0, ColorMode.None);
                AssertLine(new string('n', 40) + "  ● Running" + Spaces(16) + "  ⚡ TestFuzn", 80, dropped[0]);
            })
            .Step("An empty phase label adds nothing, and a label never joins a title that has a timeline", context =>
            {
                var fresh = LiveDashboardLayout.Render(new[] { new LiveMetricsSnapshot { ScenarioName = "Fresh" } }, 40, 0, ColorMode.None);
                AssertLine("Fresh  ● Running", 16, fresh[0]);

                var withTimeline = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 40)) }, 120, 40, ColorMode.None);
                AssertLine("Steady  ● Running" + Spaces(90) + "  ⚡ TestFuzn", 120, withTimeline[0]);
                Assert.StartsWith(" Fixed Load 100 rps ", withTimeline[6].Text);
            })
            .Run();
    }
}
