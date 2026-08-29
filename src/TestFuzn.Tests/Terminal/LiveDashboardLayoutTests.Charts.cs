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
                var lines = LiveDashboardLayout.Render(new[] { ThresholdSnapshot() }, DefaultView, 120, 40, ColorMode.None);

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
                var lines = LiveDashboardLayout.Render(new[] { ThresholdSnapshot() }, DefaultView, 120, 40, ColorMode.TrueColor);

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

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, DefaultView, 120, 40, ColorMode.None);

                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle + " · limit 800 ms", 59), 120, lines[8]);
                AssertLine(Box("100┤" + Glyphs('⣿', 45) + " ▶ 100") + "  " + Box("800 ms┤" + Glyphs('⠉', 40) + Spaces(8)), 120, lines[9]);
                AssertLine(Box(" 50┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("410 ms┤" + Spaces(48)), 120, lines[11]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box(" 20 ms┤" + Glyphs('⣀', 40) + " ▶ 48 ms"), 120, lines[14]);

                var styled = LiveDashboardLayout.Render(new[] { snapshot }, DefaultView, 120, 40, ColorMode.TrueColor);
                Assert.Contains(Sgr(Dim, "· limit") + " " + Sgr(Red, "800 ms"), styled[8].Text);
                Assert.Contains(Sgr(Red, Glyphs('⠉', 40)), styled[9].Text);
                Assert.Contains(Sgr(Median, Glyphs('⣀', 40)) + Sgr(Percentile99, " ▶ 48 ms"), styled[14].Text);
            })
            .Step("A p95 threshold alone: one yellow line at the top of a 20..500 scale", context =>
            {
                // The p95 tile carries a gauge and a trend, so the tiles take six rows and
                // the charts are 9-16.
                var snapshot = FlatSnapshot(Judged(ThresholdMetric.ResponseTimePercentile95, 500, ThresholdComparison.LessThanOrEqualTo, 40, ThresholdState.Ok));

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, DefaultView, 120, 40, ColorMode.None);

                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle + " · limit 500 ms", 59), 120, lines[9]);
                AssertLine(Box("100┤" + Glyphs('⣿', 45) + " ▶ 100") + "  " + Box("500 ms┤" + Glyphs('⠉', 40) + Spaces(8)), 120, lines[10]);
                AssertLine(Box(" 50┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("260 ms┤" + Spaces(48)), 120, lines[12]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box(" 20 ms┤" + Glyphs('⣀', 40) + " ▶ 48 ms"), 120, lines[15]);

                var styled = LiveDashboardLayout.Render(new[] { snapshot }, DefaultView, 120, 40, ColorMode.TrueColor);
                Assert.Contains(Sgr(Dim, "· limit") + " " + Sgr(Yellow, "500 ms"), styled[9].Text);
                Assert.Contains(Sgr(Yellow, Glyphs('⠉', 40)), styled[10].Text);
            })
            .Step("No latency threshold: no line and no limit in the title, even with other thresholds declared", context =>
            {
                var snapshot = FlatSnapshot(Judged(ThresholdMetric.RequestsPerSecond, 50, ThresholdComparison.GreaterThanOrEqualTo, 100, ThresholdState.Ok));

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, DefaultView, 120, 40, ColorMode.None);

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
                var lines = LiveDashboardLayout.Render(new[] { IdleSnapshot() }, DefaultView, 120, 36, ColorMode.None);

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
                var lines = LiveDashboardLayout.Render(new[] { SteadySnapshot(new double[] { 100, 0, 100 }, new double[] { 40, 0, 40 }) }, DefaultView, 120, 40, ColorMode.None);

                Assert.EndsWith(Box("48 ms┤⡇" + Spaces(39) + "⢸ ▶ 48 ms"), lines[9].Text);
                Assert.EndsWith(Box("     │⡇" + Spaces(39) + "⢸" + Spaces(8)), lines[10].Text);
                Assert.EndsWith(Box("34 ms┤⡇" + Spaces(39) + "⢸" + Spaces(8)), lines[11].Text);
                Assert.EndsWith(Box("     │⡇" + Spaces(39) + "⢸" + Spaces(8)), lines[13].Text);
                Assert.EndsWith(Box("20 ms┤⡀" + Spaces(39) + "⢀" + Spaces(8)), lines[14].Text);
                Assert.Contains(" ▶ 100 │  │ ", lines[9].Text);
                AssertLine(Box(" ≤ 50 ms " + Spaces(104) + "# #"), 120, lines[22]);

                var styled = LiveDashboardLayout.Render(new[] { SteadySnapshot(new double[] { 100, 0, 100 }, new double[] { 40, 0, 40 }) }, DefaultView, 120, 40, ColorMode.TrueColor);
                Assert.Contains(Sgr(Dim, "48 ms┤") + Sgr(Percentile99, "⡇") + Spaces(39) + Sgr(Percentile99, "⢸") + Sgr(Percentile99, " ▶ 48 ms"), styled[9].Text);
                Assert.Contains(Sgr(Dim, "│") + Sgr(Percentile95, "⡇") + Spaces(39) + Sgr(Percentile95, "⢸"), styled[13].Text);
                Assert.Contains(Sgr(Dim, "20 ms┤") + Sgr(Median, "⡀") + Spaces(39) + Sgr(Median, "⢀"), styled[14].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_height_drop_order()
    {
        // The rich section at 120 columns: 8 header rows, 8 chart rows (6 with the bodies
        // compact), 10 heatmap rows, 6 requests rows, a 5-row step table and a 4-row ticker —
        // 41 rows with everything in, 39 above the footer at 40 rows. Neither table can ever
        // give a row back here: one row and a more line take the two rows each has.
        await Scenario()
            .Step("At 38 rows the heatmap body is at its four-row floor; at 37 the row still owed sends the panel whole, and five rows stay blank", context =>
            {
                // Four over at 38: the body gives back four rows, merged from the middle
                // outward to four — "≤ 10 ms" holds buckets 0-3, "≤ 200 ms" 4-7 (every
                // request, the same steps as at six rows), "≤ 5 s" 8-11 and "> 30 s" 12-14 —
                // the heatmap at 16-21, the requests panel at 22-27, the tables at 28-32 and
                // 33-36. Five over at 37: the floor pays four, the panel goes whole for the
                // one still owed, and the requests panel moves up to 16-21, the tables to
                // 22-26 and 27-30, with rows 31-35 blank.
                var floor = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 38, ColorMode.None);
                Assert.HasCount(38, floor);
                AssertLine(PanelTop(HeatmapTitle, 120), 120, floor[16]);
                AssertLine(Box("  > 30 s " + Spaces(107)), 120, floor[17]);
                AssertLine(Box("   ≤ 5 s " + Spaces(107)), 120, floor[18]);
                AssertLine(Box("≤ 200 ms " + Spaces(97) + "**########"), 120, floor[19]);
                AssertLine(Box(" ≤ 10 ms " + Spaces(107)), 120, floor[20]);
                AssertLine(Bottom(120), 120, floor[21]);
                AssertLine(PanelTop("Requests", 120), 120, floor[22]);
                AssertLine(PanelTop("Steps", 120), 120, floor[28]);
                AssertLine(PanelTop("Errors", 120), 120, floor[33]);
                AssertLine(Bottom(120), 120, floor[36]);
                AssertLine("q quit", 6, floor[37]);

                var whole = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 37, ColorMode.None);
                Assert.HasCount(37, whole);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, whole[15]);
                AssertLine(PanelTop("Requests", 120), 120, whole[16]);
                AssertLine(PanelTop("Steps", 120), 120, whole[22]);
                AssertLine(Box(CheckoutStepRow + Spaces(43)), 120, whole[24]);
                AssertLine(PanelTop("Errors", 120), 120, whole[27]);
                AssertLine(Bottom(120), 120, whole[30]);
                AssertLine(string.Empty, 0, whole[31]);
                AssertLine(string.Empty, 0, whole[35]);
                AssertLine("q quit", 6, whole[36]);
                foreach (var line in whole)
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
            })
            .Step("At 36 rows the heatmap still goes whole under the budget, four rows blank; at 35 it is gone by height and the tables stay with three", context =>
            {
                // Six over at 36: the floor pays four, the panel goes whole for the two still
                // owed, and rows 31-34 stay blank. At 35 the section is 31 against 34 with
                // the heatmap gone by height: the same frame with rows 31-33 blank.
                var budget = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 36, ColorMode.None);
                Assert.HasCount(36, budget);
                AssertLine(PanelTop("Requests", 120), 120, budget[16]);
                AssertLine(PanelTop("Steps", 120), 120, budget[22]);
                AssertLine(PanelTop("Errors", 120), 120, budget[27]);
                AssertLine(Bottom(120), 120, budget[30]);
                AssertLine(string.Empty, 0, budget[31]);
                AssertLine(string.Empty, 0, budget[34]);
                AssertLine("q quit", 6, budget[35]);

                var height = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 35, ColorMode.None);
                Assert.HasCount(35, height);
                AssertLine(PanelTop("Requests", 120), 120, height[16]);
                AssertLine(PanelTop("Steps", 120), 120, height[22]);
                AssertLine(Box(CheckoutStepRow + Spaces(43)), 120, height[24]);
                AssertLine(Box(AddToCartStepRow + Spaces(43)), 120, height[25]);
                AssertLine(Box(CheckoutErrorEntry + Spaces(24)), 120, height[28]);
                AssertLine(Bottom(120), 120, height[30]);
                AssertLine(string.Empty, 0, height[31]);
                AssertLine("q quit", 6, height[34]);
                foreach (var line in budget.Concat(height))
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
            })
            .Step("At 30 rows the two rows owed compact the chart bodies to four and both tables stay; at 29 the bodies are compact by height already, so the row owed takes the latency chart — which frees nothing side by side — and the requests chart after it", context =>
            {
                // Two over at 30: the pair compacts together, the charts at 8-13, the requests
                // panel at 14-19, the tables at 20-24 and 25-28, no blank row. The 45-column
                // rps body is 90 columns at 16 levels with sample i at column 9.89 i: row 1
                // (levels 9-12) is ⣤ in cell 0 (columns 0 and 1 at level 10), ⣶ from column 2
                // (v ≥ 89.3) and full from column 14 (v ≥ 98.7); row 0 (13-16) starts at
                // column 26 (v ≥ 108.1) with five ⣀ cells, ⣠ in cell 18 (column 37 reaches
                // 117.5), ⣤ to cell 23, ⣴ in cell 24 (column 49 reaches 126.9), ⣶ to cell 32
                // and ⣿ from column 66 (v ≥ 136.3), the last twelve cells. One over at 29,
                // the trends gone with the height: neither table can give a row, the latency
                // chart's going only widens the requests chart, so that goes too for its six
                // — the requests panel at 8-13, the tables at 14-18 and 19-22, rows 23-27
                // blank.
                var tall = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 30, ColorMode.None);
                Assert.HasCount(30, tall);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle, 59), 120, tall[8]);
                AssertLine(Box("141┤" + Spaces(13) + Glyphs('⣀', 5) + "⣠" + Glyphs('⣤', 5) + "⣴" + Glyphs('⣶', 8) + Glyphs('⣿', 12) + " ▶ 141") + "  " + Box("95 ms┤" + Glyphs('⣿', 41) + " ▶ 95 ms"), 120, tall[9]);
                AssertLine(Box(" 71┤⣤" + Glyphs('⣶', 6) + Glyphs('⣿', 38) + Spaces(6)) + "  " + Box("65 ms┤" + Glyphs('⣿', 41) + Spaces(8)), 120, tall[10]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("35 ms┤" + Glyphs('⣀', 41) + Spaces(8)), 120, tall[12]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, tall[13]);
                AssertLine(PanelTop("Requests", 120), 120, tall[14]);
                AssertLine(PanelTop("Steps", 120), 120, tall[20]);
                AssertLine(Box(AddToCartStepRow + Spaces(43)), 120, tall[23]);
                AssertLine(PanelTop("Errors", 120), 120, tall[25]);
                AssertLine(Box(AddToCartErrorEntry + Spaces(41)), 120, tall[27]);
                AssertLine(Bottom(120), 120, tall[28]);
                AssertLine("q quit", 6, tall[29]);

                var compact = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 29, ColorMode.None);
                Assert.HasCount(29, compact);
                AssertLine(Row(Bottom(24), Bottom(23), Bottom(23), Bottom(23), Bottom(23)), 120, compact[5]);
                AssertLine(PanelTop("Requests", 120), 120, compact[8]);
                AssertLine(Bottom(120), 120, compact[13]);
                AssertLine(PanelTop("Steps", 120), 120, compact[14]);
                AssertLine(Box(CheckoutStepRow + Spaces(43)), 120, compact[16]);
                AssertLine(PanelTop("Errors", 120), 120, compact[19]);
                AssertLine(Box(CheckoutErrorEntry + Spaces(24)), 120, compact[20]);
                AssertLine(Bottom(120), 120, compact[22]);
                AssertLine(string.Empty, 0, compact[23]);
                AssertLine(string.Empty, 0, compact[27]);
                AssertLine("q quit", 6, compact[28]);
                foreach (var line in compact)
                {
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
                    Assert.DoesNotContain(LatencyChartTitle, line.Text);
                }
            })
            .Step("At 24 rows the charts' twelve are the overrun exactly and both tables fill the frame; at 23 the Steps panel is the first table to go whole; at 20 the gauges still stay", context =>
            {
                // 29 rows against 23: the two charts. Against 22 the row still owed takes the
                // Steps panel — the tables cannot trim — leaving the ticker at 14-17 and rows
                // 18-21 blank. At 20 rows (19 above the footer) the tiles keep their gauges
                // and the ticker sits at 14-17 over one blank row.
                var exact = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 24, ColorMode.None);
                Assert.HasCount(24, exact);
                AssertLine(PanelTop("Requests", 120), 120, exact[8]);
                AssertLine(PanelTop("Steps", 120), 120, exact[14]);
                AssertLine(Box(AddToCartStepRow + Spaces(43)), 120, exact[17]);
                AssertLine(PanelTop("Errors", 120), 120, exact[19]);
                AssertLine(Box(AddToCartErrorEntry + Spaces(41)), 120, exact[21]);
                AssertLine(Bottom(120), 120, exact[22]);
                AssertLine("q quit", 6, exact[23]);
                foreach (var line in exact)
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);

                var firstTable = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 23, ColorMode.None);
                Assert.HasCount(23, firstTable);
                AssertLine(Bottom(120), 120, firstTable[13]);
                AssertLine(PanelTop("Errors", 120), 120, firstTable[14]);
                AssertLine(Box(CheckoutErrorEntry + Spaces(24)), 120, firstTable[15]);
                AssertLine(Bottom(120), 120, firstTable[17]);
                AssertLine(string.Empty, 0, firstTable[18]);
                AssertLine(string.Empty, 0, firstTable[21]);
                AssertLine("q quit", 6, firstTable[22]);
                foreach (var line in firstTable)
                    Assert.DoesNotContain("Steps", line.Text);

                var gauges = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 20, ColorMode.None);
                AssertLine(Row(Box(Spaces(20)), Box(Spaces(19)), Box(Spaces(19)), Box(Spaces(19)), Box("▕████▌·····▏ 2m 45s")), 120, gauges[4]);
                AssertLine(PanelTop("Requests", 120), 120, gauges[8]);
                AssertLine(Bottom(120), 120, gauges[13]);
                AssertLine(PanelTop("Errors", 120), 120, gauges[14]);
                AssertLine(Bottom(120), 120, gauges[17]);
                AssertLine(string.Empty, 0, gauges[18]);
                AssertLine("q quit", 6, gauges[19]);
                foreach (var line in gauges)
                {
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
                    Assert.DoesNotContain("Steps", line.Text);
                }
            })
            .Step("At 18 rows the ticker still fits after the charts and the Steps panel; at 17 it is the last table to go whole", context =>
            {
                // Below 20 rows the tiles have no gauges: 7 header rows, the charts, the
                // requests panel and the tables are 28. Against 17 the charts and the Steps
                // panel pay the eleven exactly, the ticker at 13-16; against 16 the row still
                // owed takes the ticker too, and rows 13-15 stay blank.
                var kept = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 18, ColorMode.None);
                Assert.HasCount(18, kept);
                AssertLine(Row(Bottom(24), Bottom(23), Bottom(23), Bottom(23), Bottom(23)), 120, kept[4]);
                AssertLine(PanelTop("Requests", 120), 120, kept[7]);
                AssertLine(PanelTop("Errors", 120), 120, kept[13]);
                AssertLine(Box(AddToCartErrorEntry + Spaces(41)), 120, kept[15]);
                AssertLine(Bottom(120), 120, kept[16]);
                AssertLine("q quit", 6, kept[17]);

                var gone = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 17, ColorMode.None);
                Assert.HasCount(17, gone);
                AssertLine(PanelTop("Requests", 120), 120, gone[7]);
                AssertLine(Bottom(120), 120, gone[12]);
                AssertLine(string.Empty, 0, gone[13]);
                AssertLine(string.Empty, 0, gone[15]);
                AssertLine("q quit", 6, gone[16]);
                foreach (var line in gone)
                {
                    Assert.DoesNotContain("Steps", line.Text);
                    Assert.DoesNotContain("Errors", line.Text);
                }
            })
            .Step("A window of 12 to 14 rows shows the title with the phase, the two-line tiles and the requests panel whole; 10 or 11 rows cut the requests panel after its header lines", context =>
            {
                var fourteen = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 14, ColorMode.None);
                Assert.HasCount(14, fourteen);
                AssertLine("Checkout flow  ● Running · sim 1/2: Gradual Load 10→50 rps" + Spaces(49) + "  ⚡ TestFuzn", 120, fourteen[0]);
                AssertLine(Row(Bottom(24), Bottom(23), Bottom(23), Bottom(23), Bottom(23)), 120, fourteen[4]);
                AssertLine(string.Empty, 0, fourteen[5]);
                AssertLine(PanelTop("Requests", 120), 120, fourteen[6]);
                AssertLine("│ failed     32  1.0  88 ms  102 ms  99 ms  110 ms  140 ms  160 ms  201 ms" + Spaces(44) + " │", 120, fourteen[10]);
                AssertLine(Bottom(120), 120, fourteen[11]);
                AssertLine(string.Empty, 0, fourteen[12]);
                AssertLine("q quit", 6, fourteen[13]);

                var twelve = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 12, ColorMode.None);
                AssertLine("│ failed     32  1.0  88 ms  102 ms  99 ms  110 ms  140 ms  160 ms  201 ms" + Spaces(44) + " │", 120, twelve[10]);
                AssertLine("q quit", 6, twelve[11]);

                var eleven = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 11, ColorMode.None);
                AssertLine("│ ok      12480  141  12 ms   38 ms  35 ms   48 ms   72 ms   94 ms  312 ms" + Spaces(44) + " │", 120, eleven[9]);
                AssertLine("q quit", 6, eleven[10]);

                var ten = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 10, ColorMode.None);
                AssertLine(PanelTop("Requests", 120), 120, ten[6]);
                AssertLine("│ warmup 1200 ok · 3 failed" + Spaces(91) + " │", 120, ten[7]);
                AssertLine("│         count  rps    min    mean    p50     p75     p95     p99     max" + Spaces(44) + " │", 120, ten[8]);
                AssertLine("q quit", 6, ten[9]);
            })
            .Step("Wrapped tiles reach the budget steps although the height allows everything: at 60×36 the heatmap goes, the stacked charts compact and the latency chart follows, leaving the rps chart over both tables; at 60×35 the rps chart goes too", context =>
            {
                // 13 header rows (title, two tile rows of 4 and 5, a two-line timeline, a
                // blank), two 8-row stacked charts, the 10-row heatmap, 6 requests rows, the
                // 5-row step table and the 5-row ticker (at 60 columns the 59-column Checkout
                // entry breaks after "refused" and wraps under its rate) are 55 against 35,
                // twenty over: the heatmap body's four, the panel's six, the two stacked
                // bodies' four and the latency chart's six — exactly the twenty. So the
                // four-row rps chart is rows 13-18, the requests panel 19-24, the step table
                // 25-29 (its narrowest tier: the name, count, rps, p95 and fail%) and the
                // ticker 30-34. One row shorter the heatmap is gone by height and the 45
                // rows are eleven over: the bodies' four, the latency chart's six, and the
                // rps chart's six for the one still owed — no chart, the requests panel at
                // 13-18, the tables at 19-23 and 24-28, rows 29-33 blank.
                var heatmapAllowed = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 60, 36, ColorMode.None);
                Assert.HasCount(36, heatmapAllowed);
                AssertLine(PanelTop(RequestsChartTitle, 60), 60, heatmapAllowed[13]);
                AssertLine(Bottom(60), 60, heatmapAllowed[18]);
                AssertLine(PanelTop("Requests", 60), 60, heatmapAllowed[19]);
                AssertLine(PanelTop("Steps", 60), 60, heatmapAllowed[25]);
                AssertLine(Box(" step         count  rps    p95  fail%      " + Spaces(12)), 60, heatmapAllowed[26]);
                AssertLine(Box(" Checkout      6260  9.6  96 ms  █░░░░ 0.5% " + Spaces(12)), 60, heatmapAllowed[27]);
                AssertLine(PanelTop("Errors", 60), 60, heatmapAllowed[30]);
                AssertLine(Box("30× (2.0/s)  Checkout · Connection refused" + Spaces(14)), 60, heatmapAllowed[31]);
                AssertLine(Box(Spaces(13) + "(localhost:7058)" + Spaces(27)), 60, heatmapAllowed[32]);
                AssertLine(Box(" 2× (0.0/s)  Add to cart · Timeout after 30s" + Spaces(12)), 60, heatmapAllowed[33]);
                AssertLine(Bottom(60), 60, heatmapAllowed[34]);
                AssertLine("q quit", 6, heatmapAllowed[35]);
                foreach (var line in heatmapAllowed)
                {
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
                    Assert.DoesNotContain(LatencyChartTitle, line.Text);
                }

                var shorter = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 60, 35, ColorMode.None);
                Assert.HasCount(35, shorter);
                AssertLine(PanelTop("Requests", 60), 60, shorter[13]);
                AssertLine(PanelTop("Steps", 60), 60, shorter[19]);
                AssertLine(PanelTop("Errors", 60), 60, shorter[24]);
                AssertLine(Bottom(60), 60, shorter[28]);
                AssertLine(string.Empty, 0, shorter[29]);
                AssertLine(string.Empty, 0, shorter[33]);
                AssertLine("q quit", 6, shorter[34]);
                foreach (var line in shorter)
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
            })
            .Step("The heatmap needs the charts' width: at 59 columns and 40 rows neither shows", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 59, 40, ColorMode.None);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
                }
            })
            .Step("An unbounded height keeps every panel: heatmap, six-row charts, every step and error row", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 0, ColorMode.None);

                Assert.HasCount(42, lines);
                AssertLine(PanelTop(HeatmapTitle, 120), 120, lines[16]);
                AssertLine(Box(CheckoutStepRow + Spaces(43)), 120, lines[34]);
                AssertLine(Box(AddToCartStepRow + Spaces(43)), 120, lines[35]);
                AssertLine(Box(AddToCartErrorEntry + Spaces(41)), 120, lines[39]);
                AssertLine("q quit", 6, lines[41]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_third_step_costs_the_heatmap_one_more_row()
    {
        await Scenario()
            .Step("With three steps at 120×40 the heatmap gives back three rows, and the step table keeps all three rows under it", context =>
            {
                // The rich section's 41 rows and the third step's are 42 against 39: the
                // heatmap body pays the three, eight rows down to five — "≤ 2 ms" holds
                // buckets 0-1, "≤ 10 ms" 2-3, "≤ 200 ms" 4-7 (every request, the same steps
                // as at six rows), "≤ 5 s" 8-11 and "> 30 s" 12-14 — so the heatmap is rows
                // 16-22, the requests panel 23-28, the step table 29-34 and the ticker 35-38.
                // Browse sorts last: no failure, and the columns are the two other rows'
                // widths, so its row is " Browse" padded to the 12-column name, 6000, 60,
                // 12 ms, 20 ms, 0, the empty track with "0%" in the 11-column bar and a flat
                // ten-sample trend — five middle-level ⣤ cells at the right of the twelve.
                var lines = LiveDashboardLayout.Render(new[] { ThreeStepSnapshot() }, DefaultView, 120, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine(PanelTop(HeatmapTitle, 120), 120, lines[16]);
                AssertLine(Box("  > 30 s " + Spaces(107)), 120, lines[17]);
                AssertLine(Box("   ≤ 5 s " + Spaces(107)), 120, lines[18]);
                AssertLine(Box("≤ 200 ms " + Spaces(97) + "**########"), 120, lines[19]);
                AssertLine(Box(" ≤ 10 ms " + Spaces(107)), 120, lines[20]);
                AssertLine(Box("  ≤ 2 ms " + Spaces(107)), 120, lines[21]);
                AssertLine(Bottom(120), 120, lines[22]);
                AssertLine(PanelTop("Requests", 120), 120, lines[23]);
                AssertLine(Bottom(120), 120, lines[28]);
                AssertLine(PanelTop("Steps", 120), 120, lines[29]);
                AssertLine(Box(StepsHeaderRow + Spaces(43)), 120, lines[30]);
                AssertLine(Box(CheckoutStepRow + Spaces(43)), 120, lines[31]);
                AssertLine(Box(AddToCartStepRow + Spaces(43)), 120, lines[32]);
                AssertLine(Box(" Browse        6000   60  12 ms  20 ms       0  ░░░░░ 0%            ⣤⣤⣤⣤⣤" + Spaces(43)), 120, lines[33]);
                AssertLine(Bottom(120), 120, lines[34]);
                AssertLine(PanelTop("Errors", 120), 120, lines[35]);
                AssertLine(Bottom(120), 120, lines[38]);
                AssertLine("q quit", 6, lines[39]);
                foreach (var line in lines)
                    Assert.DoesNotContain("more", line.Text);
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
                var fitting = LiveDashboardLayout.Render(new[] { RampSnapshot() }, DefaultView, 40, 0, ColorMode.None);
                AssertLine("Ramp  ● Running · Gradual Load 10→50 rps", 40, fitting[0]);

                var dropped = LiveDashboardLayout.Render(new[] { RampSnapshot() }, DefaultView, 39, 0, ColorMode.None);
                AssertLine("Ramp  ● Running", 15, dropped[0]);

                var styled = LiveDashboardLayout.Render(new[] { RampSnapshot() }, DefaultView, 40, 0, ColorMode.TrueColor);
                AssertLine(Sgr(Bold, "Ramp") + "  " + Sgr(Yellow, "● Running") + " " + Sgr(Dim, "·") + " " + Sgr("38;2;255;207;107", "Gradual Load 10→50 rps"), 40, styled[0]);
            })
            .Step("The name and the badge come first: a title too wide for the window truncates as before, without the phase", context =>
            {
                var snapshot = new LiveMetricsSnapshot { ScenarioName = new string('n', 30), PhaseLabel = "init" };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, DefaultView, 40, 0, ColorMode.None);

                AssertLine(new string('n', 30) + "  ● Runni…", 40, lines[0]);
            })
            .Step("Beside the logo the title keeps the logo's columns: the phase fits the columns left of it or goes", context =>
            {
                var fitting = LiveDashboardLayout.Render(new[] { FailedSnapshot() }, DefaultView, 80, 0, ColorMode.None);
                AssertLine("Checkout flow  ● Failed · completed" + Spaces(32) + "  ⚡ TestFuzn", 80, fitting[0]);

                var snapshot = new LiveMetricsSnapshot { ScenarioName = new string('n', 40), PhaseLabel = "sim 1/2: Gradual Load 10→50 rps" };
                var dropped = LiveDashboardLayout.Render(new[] { snapshot }, DefaultView, 80, 0, ColorMode.None);
                AssertLine(new string('n', 40) + "  ● Running" + Spaces(16) + "  ⚡ TestFuzn", 80, dropped[0]);
            })
            .Step("An empty phase label adds nothing, and a label never joins a title that has a timeline", context =>
            {
                var fresh = LiveDashboardLayout.Render(new[] { new LiveMetricsSnapshot { ScenarioName = "Fresh" } }, DefaultView, 40, 0, ColorMode.None);
                AssertLine("Fresh  ● Running", 16, fresh[0]);

                var withTimeline = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 40)) }, DefaultView, 120, 40, ColorMode.None);
                AssertLine("Steady  ● Running" + Spaces(90) + "  ⚡ TestFuzn", 120, withTimeline[0]);
                Assert.StartsWith(" Fixed Load 100 rps ", withTimeline[6].Text);
            })
            .Run();
    }
}
