using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// The interaction goldens of <see cref="LiveDashboardLayout"/>: the footer's hints per view
/// and while the help is up, the quit hint surviving every width the widget would drop it at,
/// the paused badge on the title line, and the time window reaching the charts and the
/// heatmap but not the tiles. Derived by hand as the main file's summary describes; a footer
/// hint is its key in bold, a space and its description, the hints joined by a dim " · ".
/// </summary>
public partial class LiveDashboardLayoutTests
{
    /// <summary>The step detail's footer, 82 columns, and the error log's, 70.</summary>
    private const string StepDetailFooter = "Esc back · ↑↓ step · 1 overview · 3 errors · p pause · +- window · ? help · q quit";
    private const string ErrorLogFooter = "Esc back · ↑↓ scroll · 1 overview · 2 step · p pause · ? help · q quit";

    /// <summary>The footer while the help is up, whatever the view: 28 columns.</summary>
    private const string HelpFooter = "? close · Esc close · q quit";

    /// <summary>The overview's footer with its last one, two, three and four hints given up: 84, 72, 62 and 28 columns.</summary>
    private const string OverviewFooterAt84 = "1 overview · 2 step · 3 errors · ↑↓ select · ⏎ detail · p pause · +- window · q quit";
    private const string OverviewFooterAt62 = "1 overview · 2 step · 3 errors · ↑↓ select · ⏎ detail · q quit";
    private const string OverviewFooterAt28 = "1 overview · 2 step · q quit";

    /// <summary>
    /// A run 130 samples long, more than any chart body at 120 columns holds, whose series
    /// are two-level so a window shows as a step: the ok deltas 100 samples at 0 then 30 at
    /// 100 (no failures), the rates 120 samples at 100 rps then 10 at 112 — a delta of +12
    /// against the sample ten back, inside any window on the ladder and outside a window of
    /// five — the latencies flat (p95 40 ms under a p99 of 48 over a median of 20), and
    /// every interval's 100 requests in the ≤ 50 ms bucket for the first 100 samples and the
    /// ≤ 500 ms bucket for the last 30. No plan, no ring sample, no error.
    /// </summary>
    private static LiveMetricsSnapshot WindowSnapshot()
    {
        var buckets = new IReadOnlyList<int>[130];
        for (var index = 0; index < buckets.Length; index++)
            buckets[index] = Counts((index < 100 ? 5 : 8, 100));

        return new LiveMetricsSnapshot
        {
            ScenarioName = "Window",
            Phase = LoadTestPhase.Measurement,
            PhaseLabel = "Fixed Load 100 rps",
            Duration = TimeSpan.FromSeconds(130),
            RequestCountOk = 3000,
            Ok = new LiveStats { RequestCount = 3000 },
            IntervalRequestsPerSecond = 112,
            RequestsPerSecondSeries = Repeat(100, 120).Concat(Repeat(112, 10)).ToArray(),
            OkDeltaSeries = Repeat(0, 100).Concat(Repeat(100, 30)).ToArray(),
            FailedDeltaSeries = new double[130],
            ResponseTimePercentile95Series = Repeat(40, 130),
            ResponseTimeMedianSeries = Repeat(20, 130),
            ResponseTimePercentile99Series = Repeat(48, 130),
            LatencyBucketSeries = buckets
        };
    }

    [Test]
    public async Task Verify_footer_hints_follow_the_view()
    {
        await Scenario()
            .Step("At 120 columns the overview, the step detail and the error log each close with their own hints, the quit hint last", context =>
            {
                var overview = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 40, ColorMode.None);
                AssertLine(OverviewFooter, 93, overview[39]);

                var detail = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { View = LiveDashboardView.StepDetail }, 120, 40, ColorMode.None);
                AssertLine(StepDetailFooter, 82, detail[39]);

                var log = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { View = LiveDashboardView.ErrorLog }, 120, 40, ColorMode.None);
                AssertLine(ErrorLogFooter, 70, log[39]);
            })
            .Step("While the help is up the footer says how to close it, whatever the view", context =>
            {
                foreach (var view in new[] { LiveDashboardView.Overview, LiveDashboardView.StepDetail, LiveDashboardView.ErrorLog })
                {
                    var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { View = view, ShowHelp = true }, 120, 40, ColorMode.None);
                    AssertLine(HelpFooter, 28, lines[39]);
                }
            })
            .Step("TrueColor: every key is bold and every separator dim", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 40, ColorMode.TrueColor);

                var separator = Sgr(Dim, " · ");
                AssertLine(
                    Sgr(Bold, "1") + " overview" + separator + Sgr(Bold, "2") + " step" + separator + Sgr(Bold, "3") + " errors" + separator
                    + Sgr(Bold, "↑↓") + " select" + separator + Sgr(Bold, "⏎") + " detail" + separator + Sgr(Bold, "p") + " pause" + separator
                    + Sgr(Bold, "+-") + " window" + separator + Sgr(Bold, "?") + " help" + separator + Sgr(Bold, "q") + " quit",
                    93, lines[39]);
            })
            .Step("Until the error log has a body of its own it lays out the overview body: only the footer differs — while the step detail has its own, the notice under the title without a selection", context =>
            {
                var overview = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 40, ColorMode.None);
                var detail = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { View = LiveDashboardView.StepDetail }, 120, 40, ColorMode.None);
                var log = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { View = LiveDashboardView.ErrorLog, ErrorLogScroll = 3 }, 120, 40, ColorMode.None);

                for (var row = 0; row < 39; row++)
                    AssertLine(overview[row].Text, overview[row].Width, log[row]);

                AssertLine(LiveDashboardLayout.NoStepSelectedNoticeText, 27, detail[1]);
                AssertLine(string.Empty, 0, detail[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_footer_gives_up_the_view_hints_from_the_right_and_never_the_quit_hint()
    {
        await Scenario()
            .Step("The overview's 93-column footer loses its hints from the right at each width the widget would drop one, and the quit hint stays last throughout", context =>
            {
                // The hints are 10, 6, 8, 9, 8, 7, 9 and 6 columns and the quit hint 6, three
                // between neighbours: with all eight before it the footer is 93 columns, with
                // seven 84, six 72, five 62, four 51, three 39, two 28, one 19, none 6.
                AssertFooter(OverviewFooter, 93, 93);
                AssertFooter(OverviewFooterAt84, 84, 92);
                AssertFooter(OverviewFooterAt84, 84, 84);
                AssertFooter(OverviewFooterAt72, 72, 83);
                AssertFooter(OverviewFooterAt72, 72, 72);
                AssertFooter(OverviewFooterAt62, 62, 71);
                AssertFooter(OverviewFooterAt51, 51, 61);
                AssertFooter(OverviewFooterAt51, 51, 51);
                AssertFooter(OverviewFooterAt39, 39, 50);
                AssertFooter(OverviewFooterAt28, 28, 38);
                AssertFooter("1 overview · q quit", 19, 27);
                AssertFooter("1 overview · q quit", 19, 19);
                AssertFooter("q quit", 6, 18);
                AssertFooter("q quit", 6, 6);
            })
            .Step("Below the quit hint's six columns it is cut with the widget's ellipsis, down to the ellipsis alone", context =>
            {
                AssertFooter("q qu…", 5, 5);
                AssertFooter("q q…", 4, 4);
                AssertFooter("…", 1, 1);
            })
            .Step("The other views' footers degrade the same way: the step detail at 60 columns, the error log at 40, the help at 20", context =>
            {
                // Esc back · ↑↓ step · 1 overview · 3 errors · q quit is 51; with p pause 61.
                var detail = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { View = LiveDashboardView.StepDetail }, 60, 24, ColorMode.None);
                AssertLine("Esc back · ↑↓ step · 1 overview · 3 errors · q quit", 51, detail[23]);

                // Esc back · ↑↓ scroll · q quit is 29; with 1 overview 42.
                var log = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { View = LiveDashboardView.ErrorLog }, 40, 24, ColorMode.None);
                AssertLine("Esc back · ↑↓ scroll · q quit", 29, log[23]);

                // ? close · q quit is 16; with Esc close 28.
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { ShowHelp = true }, 20, 24, ColorMode.None);
                AssertLine("? close · q quit", 16, help[23]);
            })
            .Run();
    }

    /// <summary>The overview footer of a frame rendered at the given width, on the last of 24 rows.</summary>
    private static void AssertFooter(string expectedText, int expectedWidth, int width)
    {
        var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, width, 24, ColorMode.None);
        AssertLine(expectedText, expectedWidth, lines[23]);
    }

    /// <summary>
    /// The width of the overview footer at the given frame width, from the rule the class
    /// summary gives: the widest of 93, 84, 72, 62, 51, 39, 28, 19 and 6 that fits, and the
    /// width itself below 6 (the quit hint cut with an ellipsis).
    /// </summary>
    private static int ExpectedOverviewFooterWidth(int width)
    {
        foreach (var footerWidth in new[] { 93, 84, 72, 62, 51, 39, 28, 19, 6 })
        {
            if (footerWidth <= width)
                return footerWidth;
        }

        return width;
    }

    [Test]
    public async Task Verify_paused_badge_follows_the_status_badge()
    {
        await Scenario()
            .Step("Paused, the title line carries the badge after a middle dot — the rest of the frame as it was", context =>
            {
                // "Checkout flow  ● Running · ⏸ paused" is 35 columns of the 107 left of the logo.
                var paused = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { IsPaused = true }, 120, 40, ColorMode.None);
                var running = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 40, ColorMode.None);

                AssertLine("Checkout flow  ● Running · " + LiveDashboardLayout.PausedBadgeText + Spaces(72) + "  ⚡ TestFuzn", 120, paused[0]);
                Assert.HasCount(40, paused);
                for (var row = 1; row < 40; row++)
                    AssertLine(running[row].Text, running[row].Width, paused[row]);
            })
            .Step("TrueColor: the badge is bold yellow after a dim dot, and the spinner glyph replaces the running dot as ever", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { IsPaused = true }, 120, 40, ColorMode.TrueColor, SparklineGlyphSet.Braille, "⠋");

                Assert.StartsWith(Sgr(Bold, "Checkout flow") + "  " + Sgr(Yellow, "⠋ Running") + " " + Sgr(Dim, "·") + " " + Sgr(BoldYellow, "⏸ paused"), lines[0].Text);
                Assert.AreEqual(120, lines[0].Width);
            })
            .Step("The badge comes before the phase label a title line carries without a timeline, and the label is what yields when the width is short", context =>
            {
                var withLabel = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, DefaultView with { IsPaused = true }, 120, 14, ColorMode.None);
                AssertLine("Browse catalog  ● Running · ⏸ paused · warmup: Fixed Load 50 rps" + Spaces(43) + "  ⚡ TestFuzn", 120, withLabel[0]);

                // 36 columns fit 40; the label after them would not.
                var narrow = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, DefaultView with { IsPaused = true }, 40, 14, ColorMode.None);
                AssertLine("Browse catalog  ● Running · ⏸ paused", 36, narrow[0]);
            })
            .Step("A finished badge takes it too, and every column of a multi-scenario frame shows it", context =>
            {
                // 46 columns of the 87 left of the logo at 100.
                var failed = LiveDashboardLayout.Render(new[] { FailedSnapshot() }, DefaultView with { IsPaused = true }, 100, 0, ColorMode.None);
                AssertLine("Checkout flow  ● Failed · ⏸ paused · completed" + Spaces(41) + "  ⚡ TestFuzn", 100, failed[0]);

                var columns = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView with { IsPaused = true }, 160, 40, ColorMode.None);
                Assert.StartsWith("Checkout flow  ● Running · ⏸ paused", columns[0].Text);
                Assert.Contains("  Browse catalog  ● Running · ⏸ paused", columns[0].Text);
                Assert.AreEqual(2, columns[0].Text.Split(LiveDashboardLayout.PausedBadgeText).Length - 1);
            })
            .Run();
    }

    [Test]
    public async Task Verify_time_window_applies_to_the_charts_and_the_heatmap_but_not_the_tiles()
    {
        // No plan, no ring sample: the title, five tile rows and a blank put the chart panels
        // on rows 7-14 (the bodies on 8-13) and the heatmap on 15-24 (its eight rows on
        // 16-23) under an unbounded height. Side by side, the requests chart's 55-column
        // panel interior holds the axis "100┤" / " 50┤" / "  0┤" (4), the annotation " ▶ 100"
        // (6) and a 45-cell body of 90 sample columns; the latency chart's holds "48 ms┤" /
        // "34 ms┤" / "20 ms┤" (6), " ▶ 48 ms" (8) and 41 cells. The heatmap body is 107
        // columns after its 8-column labels and a space.
        await Scenario()
            .Step("Without a window the requests chart scrolls: its newest 90 samples are 60 at 0 — the bottom dot alone — and 30 at 100, full", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { WindowSnapshot() }, DefaultView, 120, 0, ColorMode.None);

                AssertRequestsChart(lines, 8,
                    "100┤" + Spaces(30) + Glyphs('⣿', 15) + " ▶ 100",
                    "   │" + Spaces(30) + Glyphs('⣿', 15) + Spaces(6),
                    " 50┤" + Spaces(30) + Glyphs('⣿', 15) + Spaces(6),
                    "   │" + Spaces(30) + Glyphs('⣿', 15) + Spaces(6),
                    "   │" + Spaces(30) + Glyphs('⣿', 15) + Spaces(6),
                    "  0┤" + Glyphs('⣀', 30) + Glyphs('⣿', 15) + Spaces(6));
            })
            .Step("A window of 60 samples stretches the newest 60 over the 90 columns: 30 at 0 and 30 at 100, the step interpolated across the two columns between samples 29 and 30", context =>
            {
                // Column c reads sample position c × 59 / 89: columns 0-43 lie among the
                // zeros (43 → 28.5), column 44 (29.17) reads 16.9 — level 1 + round(0.169 ×
                // 23) = 5 — column 45 (29.83) reads 83.1, level 20, and from column 46 (30.5)
                // on every column is 100, level 24. Cell 22 holds columns 44 and 45: on the
                // bottom row (levels 1-4) both are full, ⣿; on the row above (5-8) the left
                // column has level 5 alone and the right all four, ⣸; on the next three rows
                // (9-20) only the right column, ⢸; on the top row (21-24) neither.
                var lines = LiveDashboardLayout.Render(new[] { WindowSnapshot() }, DefaultView with { TimeWindow = 60 }, 120, 0, ColorMode.None);

                AssertRequestsChart(lines, 8,
                    "100┤" + Spaces(23) + Glyphs('⣿', 22) + " ▶ 100",
                    "   │" + Spaces(22) + "⢸" + Glyphs('⣿', 22) + Spaces(6),
                    " 50┤" + Spaces(22) + "⢸" + Glyphs('⣿', 22) + Spaces(6),
                    "   │" + Spaces(22) + "⢸" + Glyphs('⣿', 22) + Spaces(6),
                    "   │" + Spaces(22) + "⣸" + Glyphs('⣿', 22) + Spaces(6),
                    "  0┤" + Glyphs('⣀', 22) + Glyphs('⣿', 23) + Spaces(6));
            })
            .Step("The flat latency chart reads the same with and without the window: the p99 band full, the p95 band under it and the median line along the floor", context =>
            {
                var whole = LiveDashboardLayout.Render(new[] { WindowSnapshot() }, DefaultView, 120, 0, ColorMode.None);
                var windowed = LiveDashboardLayout.Render(new[] { WindowSnapshot() }, DefaultView with { TimeWindow = 60 }, 120, 0, ColorMode.None);

                foreach (var lines in new[] { whole, windowed })
                {
                    Assert.EndsWith("  " + Box("48 ms┤" + Glyphs('⣿', 41) + " ▶ 48 ms"), lines[8].Text);
                    Assert.EndsWith("  " + Box("     │" + Glyphs('⣿', 41) + Spaces(8)), lines[9].Text);
                    Assert.EndsWith("  " + Box("34 ms┤" + Glyphs('⣿', 41) + Spaces(8)), lines[10].Text);
                    Assert.EndsWith("  " + Box("20 ms┤" + Glyphs('⣀', 41) + Spaces(8)), lines[13].Text);
                }
            })
            .Step("The heatmap follows the window: every 107 newest samples without one, the newest 60 at the right of the body with one, each interval's 100 requests the top step", context =>
            {
                // Every drawn sample's total is 100, the median too, so a bucket with the
                // whole interval is step 6: '#'. Unwindowed, samples 23-99 fill the first 77
                // columns of the ≤ 50 ms row and 100-129 the last 30 of the ≤ 1 s row; the
                // window keeps samples 70-129, the first 47 columns blank.
                var whole = LiveDashboardLayout.Render(new[] { WindowSnapshot() }, DefaultView, 120, 0, ColorMode.None);
                AssertLine(PanelTop(HeatmapTitle, 120), 120, whole[15]);
                AssertLine(Box("   ≤ 1 s " + Spaces(77) + Glyphs('#', 30)), 120, whole[19]);
                AssertLine(Box(" ≤ 50 ms " + Glyphs('#', 77) + Spaces(30)), 120, whole[21]);
                AssertLine(Box("  ≤ 2 ms " + Spaces(107)), 120, whole[23]);

                var windowed = LiveDashboardLayout.Render(new[] { WindowSnapshot() }, DefaultView with { TimeWindow = 60 }, 120, 0, ColorMode.None);
                AssertLine(Box("   ≤ 1 s " + Spaces(77) + Glyphs('#', 30)), 120, windowed[19]);
                AssertLine(Box(" ≤ 50 ms " + Spaces(47) + Glyphs('#', 30) + Spaces(30)), 120, windowed[21]);
                AssertLine(Box("  ≤ 2 ms " + Spaces(107)), 120, windowed[23]);
            })
            .Step("The tiles keep their own windows: the rows are the same under a window of 60, of 5 — narrower than the delta's ten samples, whose ▲ 12 still shows — and of 300, wider than the run", context =>
            {
                var whole = LiveDashboardLayout.Render(new[] { WindowSnapshot() }, DefaultView, 120, 0, ColorMode.None);
                Assert.StartsWith(Box("rps" + Spaces(13) + "▲ 12"), whole[2].Text);

                foreach (var timeWindow in new[] { 60, 5, 300 })
                {
                    var windowed = LiveDashboardLayout.Render(new[] { WindowSnapshot() }, DefaultView with { TimeWindow = timeWindow }, 120, 0, ColorMode.None);
                    for (var row = 0; row <= 6; row++)
                        AssertLine(whole[row].Text, whole[row].Width, windowed[row]);

                    Assert.StartsWith(Box("rps" + Spaces(13) + "▲ 12"), windowed[2].Text);
                }

                // A window wider than the run shows every sample, like none.
                var wide = LiveDashboardLayout.Render(new[] { WindowSnapshot() }, DefaultView with { TimeWindow = 300 }, 120, 0, ColorMode.None);
                Assert.HasCount(whole.Count, wide);
                for (var row = 0; row < whole.Count; row++)
                    AssertLine(whole[row].Text, whole[row].Width, wide[row]);
            })
            .Run();
    }

    /// <summary>The six body rows of the requests chart panel, the left of the side-by-side pair, from the given frame row.</summary>
    private static void AssertRequestsChart(IReadOnlyList<RenderedLine> lines, int firstRow, params string[] rows)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            Assert.StartsWith(Box(rows[index]) + "  ", lines[firstRow + index].Text, $"Chart row {index}");
            Assert.AreEqual(120, lines[firstRow + index].Width);
        }
    }
}
