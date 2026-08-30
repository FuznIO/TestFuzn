using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// The step detail goldens of <see cref="LiveDashboardLayout"/> — the view state's
/// <see cref="LiveDashboardView.StepDetail"/> — over the snapshots of the main file: the
/// title and header lines, the step's four tiles, its two charts and its share of the error
/// ticker at 120×40 and 80×24, the empty-series and no-errors steps, the header's position
/// following a sort change, the notice line for a selection that names no step, the failure
/// reason under the tiles, the step rps delta against a warmup zero, the count tile's skipped
/// unit, the time window on the step charts, the detail's own height drop order, the TrueColor
/// styling and a hostile sweep. Derived by hand as the main file's summary describes; the
/// chart bodies are worked column by column in the comments, a level being 1 + round(position
/// × (levels − 1)) over the chart's shared range.
/// </summary>
public partial class LiveDashboardLayoutTests
{
    private const string StepLatencyChartTitle = "latency — p95";

    /// <summary>The step detail's footer with its last hint given up: 73 columns.</summary>
    private const string StepDetailFooterAt73 = "Esc back · ↑↓ step · 1 overview · 3 errors · p pause · +- window · q quit";

    /// <summary>The rich scenario's title line in the detail, which carries the phase label since the detail shows no timeline: 58 columns.</summary>
    private const string RichDetailTitle = "Checkout flow  ● Running · sim 1/2: Gradual Load 10→50 rps";

    /// <summary>The header lines of the rich scenario's steps: Checkout is displayed first, Add to cart second.</summary>
    private const string CheckoutDetailHeader = "step 1/2 · Checkout · Esc back";
    private const string AddToCartDetailHeader = "step 2/2 · Add to cart · Esc back";

    /// <summary>The step detail's view state on the given selection, every sample in the window, not paused, no help.</summary>
    private static LiveDashboardViewState DetailView(int? selectedStepIndex)
    {
        return DefaultView with { View = LiveDashboardView.StepDetail, SelectedStepIndex = selectedStepIndex };
    }

    /// <summary>
    /// The width of the step detail footer at the given frame width, from the rule the class
    /// summary gives: the widest of 82, 73, 61, 51, 40, 27, 17 and 6 that fits, and the width
    /// itself below 6 (the quit hint cut with an ellipsis).
    /// </summary>
    private static int ExpectedStepDetailFooterWidth(int width)
    {
        foreach (var footerWidth in new[] { 82, 73, 61, 51, 40, 27, 17, 6 })
        {
            if (footerWidth <= width)
                return footerWidth;
        }

        return width;
    }

    /// <summary>A scenario with one step that has not run: no series, no request, no error.</summary>
    private static LiveMetricsSnapshot FreshStepSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Fresh",
            Steps = new[] { new LiveStepMetrics { Name = "Fresh step" } }
        };
    }

    /// <summary>
    /// The window run's step: the 130-sample series of <see cref="WindowSnapshot"/> — the ok
    /// deltas 100 samples at 0 then 30 at 100, no failures, the rates 120 at 100 then 10 at
    /// 112 — as one step's own, with its p95 flat at 40 ms, so the step's requests chart is
    /// the overview's window chart cell for cell. No plan, no ring sample, no error.
    /// </summary>
    private static LiveMetricsSnapshot WindowStepSnapshot()
    {
        var window = WindowSnapshot();
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Window",
            PhaseLabel = "Fixed Load 100 rps",
            Steps = new[]
            {
                new LiveStepMetrics
                {
                    Name = "Window step",
                    RequestCountOk = 3000,
                    RequestsPerSecond = 112,
                    RequestsPerSecondSeries = window.RequestsPerSecondSeries,
                    OkDeltaSeries = window.OkDeltaSeries,
                    FailedDeltaSeries = window.FailedDeltaSeries,
                    ResponseTimePercentile95Series = window.ResponseTimePercentile95Series
                }
            }
        };
    }

    /// <summary>The failed run of the main file with one step, "Checkout" — 95 ok and 5 failed requests, no series — so the detail has a step to show the failure reason under.</summary>
    private static LiveMetricsSnapshot FailedStepSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Checkout flow",
            PhaseLabel = "completed",
            Duration = TimeSpan.FromSeconds(300),
            IsCompleted = true,
            Status = TestStatus.Failed,
            StatusDetail = "Assert.IsLessThan failed. p95 too high: 240 ms",
            Steps = new[] { new LiveStepMetrics { Name = "Checkout", RequestCountOk = 95, RequestCountFailed = 5 } }
        };
    }

    /// <summary>
    /// A run whose step "Ramp" sat at zero for <see cref="LiveDashboardLayout.DeltaSampleDistance"/>
    /// samples — its warmup, the series a step carries while the scenario warms up — and then
    /// climbed 40, 41, … one per sample over the given count of measurement samples; the
    /// scenario's own rate series is the same numbers, as if it too had been idle, so the two
    /// tiles' delta rules can be told apart on the same series. No plan, no sample, no error.
    /// </summary>
    private static LiveMetricsSnapshot WarmupZeroSnapshot(int measurementSampleCount)
    {
        var series = new double[LiveDashboardLayout.DeltaSampleDistance + measurementSampleCount];
        for (var index = 0; index < measurementSampleCount; index++)
            series[LiveDashboardLayout.DeltaSampleDistance + index] = 40 + index;

        return new LiveMetricsSnapshot
        {
            ScenarioName = "Warmup",
            RequestsPerSecondSeries = series,
            Steps = new[] { new LiveStepMetrics { Name = "Ramp", RequestCountOk = 100, RequestsPerSecond = series[series.Length - 1], RequestsPerSecondSeries = series } }
        };
    }

    /// <summary>The many-errors run with its step S declared, so the detail of S shows every one of its errors; S itself has no series and no request.</summary>
    private static LiveMetricsSnapshot ManyErrorsStepSnapshot(int errorCount)
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Many",
            Errors = ManyErrorsSnapshot(errorCount).Errors,
            Steps = new[] { new LiveStepMetrics { Name = "S" } }
        };
    }

    /// <summary>
    /// The hostile run's forty-two errors with four steps the detail must survive, three of
    /// them the steps of its most hostile entries — "Long" (the 5000-character message, the
    /// count past the capacity and the rate of a billion), "Control [step]" (the markup and
    /// control characters) and the 200-character name — with series of NaN, infinities, a
    /// value no TimeSpan holds and a negative delta, 300-sample series of billions, counts at
    /// the integer's maximum, a skipped count likewise, and a bracketed name with a line
    /// break over all-zero series.
    /// </summary>
    private static LiveMetricsSnapshot HostileDetailSnapshot()
    {
        var hostile = HostileSnapshot();
        var billions = new double[ScenarioLiveMetrics.SampleCapacity];
        for (var index = 0; index < billions.Length; index++)
            billions[index] = index % 7 == 0 ? double.NaN : 1e9 * index;

        return new LiveMetricsSnapshot
        {
            ScenarioName = "Hostile",
            Samples = hostile.Samples,
            Errors = hostile.Errors,
            DistinctErrorCount = hostile.DistinctErrorCount,
            Steps = new[]
            {
                new LiveStepMetrics { Name = "Long", RequestCountOk = 5, RequestCountFailed = -1, RequestsPerSecond = double.NaN, RequestsPerSecondSeries = new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1 }, OkDeltaSeries = new[] { double.NaN, double.PositiveInfinity, 1e300, -5 }, FailedDeltaSeries = new[] { 1, double.NaN, double.NegativeInfinity, 2 }, ResponseTimePercentile95Series = new[] { double.NaN, 1e300, -5, 0 } },
                new LiveStepMetrics { Name = "Control [step]", RequestCountOk = int.MaxValue, RequestCountFailed = int.MaxValue, SkippedCount = int.MaxValue, RequestsPerSecond = double.PositiveInfinity, RequestsPerSecondSeries = billions, OkDeltaSeries = billions, FailedDeltaSeries = billions, ResponseTimePercentile95Series = billions },
                new LiveStepMetrics { Name = new string('n', 200), RequestCountOk = 1, ResponseTimePercentile95Series = new[] { TimeSpan.MaxValue.TotalMilliseconds, 1 } },
                new LiveStepMetrics { Name = "[bold]Step\r\n1[/]", RequestCountOk = 2000000000, RequestCountFailed = 2000000000, SkippedCount = 3, OkDeltaSeries = new double[3], FailedDeltaSeries = new double[3], ResponseTimePercentile95Series = new double[3] }
            }
        };
    }

    [Test]
    public async Task Verify_step_detail_golden_frame_at_120x40()
    {
        // Checkout, declared second, is displayed first: the title carries the phase label
        // (no timeline in the detail), the header says step 1/2, and the four tiles share the
        // width less three gaps — 117 / 4 = 29 and one over, boxes of 30, 29, 29, 29 (inner
        // 26, 25, 25, 25). The rps tile: the newest rate 9.6, no delta (ten samples, one
        // short), and the ten-sample trend in the right five columns at levels 3 3 3 3 4 4 4
        // 4 4 1 over 9.6..70 — ⣶⣶⣿⣿⣇, the Steps table's. The p95 tile: 96 ms, Neutral (the
        // trailing median is 93.5 ms, no spike), the trend 1 1 2 2 3 3 4 4 4 4 over 90..96 —
        // ⣀⣤⣶⣿⣿. fail%: 30 of 6260 is 0.48 %, "0.5" in Warning yellow; count: 6260, no
        // skipped iterations. The title, the header, five tile rows and a blank are 8 rows,
        // the charts 8 and the one-entry ticker 3: 19 against 39, nothing to give, so the
        // charts are rows 8-15, the ticker 16-18 and rows 19-38 blank.
        //
        // The requests chart's 55-column interior: the axis "70" / "35" / "0" (3), the
        // annotation " ▶ 9" (4, the newest ok count) and a 48-cell body of 96 columns over
        // the ok deltas 44, 48, 52, 55, 60, 64, 65, 69, 70, 9 and the failed 0, 0, 0, 1, 0,
        // 0, 1, 0, 0, 1 on a 0..70 scale: a level is 1 + round(v × 23 / 70), and sample i
        // sits at column 10.56 i with the columns between interpolated. Level 16 needs
        // v ≥ 44.13, 17 ≥ 47.17, 18 ≥ 50.22, 19 ≥ 53.26, 20 ≥ 56.30, 21 ≥ 59.35, 22 ≥ 62.39,
        // 23 ≥ 65.43 and 24 ≥ 68.48, so the ramp reads 15 at column 0, 16 on 1-8, 17 on
        // 9-16, 18 on 17-25, 19 on 26-34, 20 on 35-40, 21 on 41-48, 22 on 49-64, 23 on
        // 65-72 and 24 on 73-84; the collapse from 70 to 9 over the last eleven columns
        // reads 23, 21, 19, 17, 15, 13, 12, 10, 8, 6 and 4 on columns 85-95. A failed count
        // of 1 is level 1, the dot every ok column paints too, so the glyphs are the ok
        // area's: row 0 (levels 21-24) starts at cell 20 with a right-hand dot, climbs ⣀ ⣠
        // ⣤ ⣴ ⣶ ⣾ to ⣿ from cell 37 and ends ⣷ ⡀ over the collapse; row 1 (17-20) starts at
        // cell 4 and is full from cell 18 to ⣷ ⡀ at cells 43-44; row 2 (13-16) is ⣾ then full
        // to ⣷ ⡀ at 44-45; row 3 (9-12) full to ⡄ at cell 46; row 4 (5-8) full to ⡄ at 47;
        // row 5 (1-4) full. The latency chart: the axis "96 ms" / "93 ms" / "90 ms" (6), the
        // annotation " ▶ 96 ms" (8) and 41 cells of 82 columns over 90, 90, 91, 92, 93, 94,
        // 95, 95, 96, 96 on a 90..96 scale — sample i at column 9 i exactly, a level
        // 1 + round((v − 90) × 23 / 6) — so the levels climb by segment: 1 on columns 0-10,
        // 2 on 11-12, 3 on 13-14, 4 on 15-17, 5 on 18-19, 6 on 20-21, 7 on 22-24, 8 on 25-26,
        // 9 on 27-28, 10 on 29-31, 11 on 32-33, 12 on 34-35, 13 on 36-38, 14 on 39-40, 15 on
        // 41-43, 16 on 44-45, 17 on 46-47, 18 on 48-50, 19 on 51-52, 20 on 53-63, 21 on
        // 64-66, 22 on 67-68, 23 on 69-70 and 24 from 71: every row is a run of blanks, four
        // rising cells and a run of ⣿ — 32, ⣀⣠⣴⣾, 5 on row 0; 23, ⣀⣤⣴⣾, 14 on row 1; 18,
        // ⣀⣠⣴⣶, 19 on row 2; 13, ⢀⣠⣤⣶, 24 on row 3; 9, ⣀⣤⣶⣾, 28 on row 4; and ⣀⣀⣀⣀⣀⣠⣴⣾
        // then 33 on row 5.
        await Scenario()
            .Step("The frame is the title with the phase, the header, the tiles, a blank, the charts side by side, the ticker with Checkout's error, blank rows and the step detail's footer", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(1), 120, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine(RichDetailTitle + Spaces(49) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine(CheckoutDetailHeader, 30, lines[1]);
                AssertLine(Row(Top(30), Top(29), Top(29), Top(29)), 120, lines[2]);
                AssertLine(Row(Box("rps" + Spaces(23)), Box("p95" + Spaces(22)), Box("fail%" + Spaces(20)), Box("count" + Spaces(20))), 120, lines[3]);
                AssertLine(Row(Box("9.6" + Spaces(23)), Box("96 ms" + Spaces(20)), Box("0.5 %" + Spaces(20)), Box("6260" + Spaces(21))), 120, lines[4]);
                AssertLine(Row(Box(Spaces(21) + "⣶⣶⣿⣿⣇"), Box(Spaces(20) + "⣀⣤⣶⣿⣿"), Box(Spaces(25)), Box(Spaces(25))), 120, lines[5]);
                AssertLine(Row(Bottom(30), Bottom(29), Bottom(29), Bottom(29)), 120, lines[6]);
                AssertLine(string.Empty, 0, lines[7]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(StepLatencyChartTitle, 59), 120, lines[8]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, lines[15]);
                AssertLine(PanelTop("Errors", 120), 120, lines[16]);
                AssertLine(Box(CheckoutErrorEntry + Spaces(24)), 120, lines[17]);
                AssertLine(Bottom(120), 120, lines[18]);
                AssertLine(string.Empty, 0, lines[19]);
                AssertLine(string.Empty, 0, lines[38]);
                AssertLine(StepDetailFooter, 82, lines[39]);
                AssertMaximumWidth(120, lines);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain("Add to cart", line.Text);
                    Assert.DoesNotContain("Steps", line.Text);
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
                    Assert.DoesNotContain("Requests", line.Text);
                }
            })
            .Step("The requests chart draws the step's ok ramp and its collapse over the failed floor, and the latency chart the p95 climb as one band", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(1), 120, 40, ColorMode.None);

                AssertLine(Box("70┤" + Spaces(20) + "⢀⣀⣀⣀⣠" + Glyphs('⣤', 7) + "⣴⣶⣶⣶⣾" + Glyphs('⣿', 5) + "⣷⡀" + Spaces(8)) + "  " + Box("96 ms┤" + Spaces(32) + "⣀⣠⣴⣾" + Glyphs('⣿', 5) + " ▶ 96 ms"), 120, lines[9]);
                AssertLine(Box("  │" + Spaces(4) + "⢀⣀⣀⣀⣠" + Glyphs('⣤', 4) + Glyphs('⣶', 4) + "⣾" + Glyphs('⣿', 25) + "⣷⡀" + Spaces(7)) + "  " + Box("     │" + Spaces(23) + "⣀⣤⣴⣾" + Glyphs('⣿', 14) + Spaces(8)), 120, lines[10]);
                AssertLine(Box("35┤⣾" + Glyphs('⣿', 43) + "⣷⡀" + Spaces(6)) + "  " + Box("93 ms┤" + Spaces(18) + "⣀⣠⣴⣶" + Glyphs('⣿', 19) + Spaces(8)), 120, lines[11]);
                AssertLine(Box("  │" + Glyphs('⣿', 46) + "⡄" + Spaces(5)) + "  " + Box("     │" + Spaces(13) + "⢀⣠⣤⣶" + Glyphs('⣿', 24) + Spaces(8)), 120, lines[12]);
                AssertLine(Box("  │" + Glyphs('⣿', 47) + "⡄" + Spaces(4)) + "  " + Box("     │" + Spaces(9) + "⣀⣤⣶⣾" + Glyphs('⣿', 28) + Spaces(8)), 120, lines[13]);
                AssertLine(Box(" 0┤" + Glyphs('⣿', 48) + " ▶ 9") + "  " + Box("90 ms┤⣀⣀⣀⣀⣀⣠⣴⣾" + Glyphs('⣿', 33) + Spaces(8)), 120, lines[14]);
            })
            .Step("TrueColor: the header's position in the accent, the name bold, the dots and the hint dim; the fail% value yellow; the requests body green and the p95 band, its legend and its annotation in the p95 colour", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(1), 120, 40, ColorMode.TrueColor);

                AssertLine(Sgr(BoldAccent, "step 1/2") + " " + Sgr(Dim, "·") + " " + Sgr(Bold, "Checkout") + " " + Sgr(Dim, "·") + " " + Sgr(Dim, "Esc back"), 30, lines[1]);
                Assert.Contains(Box(Sgr(Bold, "9.6") + Spaces(23)), lines[4].Text);
                Assert.Contains(Box(Sgr(Bold, "96") + " " + Sgr(Dim, "ms") + Spaces(20)), lines[4].Text);
                Assert.Contains(Box(Sgr(BoldYellow, "0.5") + " " + Sgr(Dim, "%") + Spaces(20)), lines[4].Text);
                Assert.Contains(Box(Sgr(Bold, "6260") + Spaces(21)), lines[4].Text);
                Assert.Contains(Sgr(Dim, Spaces(21) + "⣶⣶⣿⣿⣇"), lines[5].Text);
                Assert.Contains(Sgr(BoldAccent, "latency") + " " + Sgr(Dim, "—") + " " + Sgr(Percentile95, "p95"), lines[8].Text);
                Assert.Contains(Sgr(Green, "⢀⣀⣀⣀⣠" + Glyphs('⣤', 7) + "⣴⣶⣶⣶⣾" + Glyphs('⣿', 5) + "⣷⡀"), lines[9].Text);
                Assert.Contains(Sgr(Dim, "96 ms┤") + Spaces(32) + Sgr(Percentile95, "⣀⣠⣴⣾" + Glyphs('⣿', 5)) + Sgr(Percentile95, " ▶ 96 ms"), lines[9].Text);
                Assert.Contains(Sgr(Dim, "0┤") + Sgr(Green, Glyphs('⣿', 48)) + Sgr(Green, " ▶ 9"), lines[14].Text);
                Assert.Contains(Sgr(Dim, "90 ms┤") + Sgr(Percentile95, "⣀⣀⣀⣀⣀⣠⣴⣾" + Glyphs('⣿', 33)), lines[14].Text);
                Assert.DoesNotContain(Sgr(Red, "⣿"), lines[14].Text);
                Assert.Contains(Sgr(Red, "30×"), lines[17].Text);
                AssertMaximumWidth(120, lines);
            })
            .Step("The help changes the footer and the twelve rows its panel covers — 13 to 24, the rows around them untouched (LiveDashboardLayoutTests.ErrorLog.cs pins the panel) — and the paused badge sits on the title line before the phase", context =>
            {
                var detail = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(1), 120, 40, ColorMode.None);
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(1) with { ShowHelp = true }, 120, 40, ColorMode.None);
                for (var row = 0; row < 13; row++)
                    AssertLine(detail[row].Text, detail[row].Width, help[row]);
                for (var row = 13; row < 25; row++)
                    Assert.AreEqual(120, help[row].Width, $"Covered row {row}");
                for (var row = 25; row < 39; row++)
                    AssertLine(detail[row].Text, detail[row].Width, help[row]);
                Assert.Contains("? help", help[13].Text);
                AssertLine(HelpFooter, 28, help[39]);

                var paused = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(1) with { IsPaused = true }, 120, 40, ColorMode.None);
                AssertLine("Checkout flow  ● Running · " + LiveDashboardLayout.PausedBadgeText + " · sim 1/2: Gradual Load 10→50 rps" + Spaces(38) + "  ⚡ TestFuzn", 120, paused[0]);
                for (var row = 1; row < 40; row++)
                    AssertLine(detail[row].Text, detail[row].Width, paused[row]);
            })
            .Step("Add to cart, declared first and displayed second, has its own tiles, charts and error: the header says step 2/2 and the ticker's count column is its own width", context =>
            {
                // The rps tile: 71 (71.4 in the whole-number format from 10 up), the trend
                // 1 1 2 2 3 3 4 4 4 4 over 44..71.4 — ⣀⣤⣶⣿⣿; the p95 tile: 38 ms, Neutral,
                // the trend 4 4 3 3 2 2 2 2 1 1 over 38..44 — ⣿⣶⣤⣤⣀; fail%: 2 of 6252 is
                // 0.03 %, "<0.1" Warning; count 6252. The requests chart's ok deltas top at
                // 71 with no failure, so the axis reads 71 / 36 / 0 and the annotation " ▶ 71"
                // sits on the top row; the p95 chart's 44..38 fall puts " ▶ 38 ms" on the
                // bottom row under a 44 ms / 41 ms / 38 ms axis. The entry is the only one,
                // so its count is one column wide: 74 columns.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(0), 120, 40, ColorMode.None);

                AssertLine(AddToCartDetailHeader, 33, lines[1]);
                AssertLine(Row(Box("71" + Spaces(24)), Box("38 ms" + Spaces(20)), Box("<0.1 %" + Spaces(19)), Box("6252" + Spaces(21))), 120, lines[4]);
                AssertLine(Row(Box(Spaces(21) + "⣀⣤⣶⣿⣿"), Box(Spaces(20) + "⣿⣶⣤⣤⣀"), Box(Spaces(25)), Box(Spaces(25))), 120, lines[5]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(StepLatencyChartTitle, 59), 120, lines[8]);
                Assert.StartsWith("│ 71┤", lines[9].Text);
                Assert.Contains(" ▶ 71 │  │ 44 ms┤", lines[9].Text);
                Assert.Contains("│ 36┤", lines[11].Text);
                Assert.Contains("│ 41 ms┤", lines[11].Text);
                Assert.StartsWith("│  0┤", lines[14].Text);
                Assert.Contains("│ 38 ms┤", lines[14].Text);
                Assert.EndsWith(" ▶ 38 ms │", lines[14].Text);
                AssertLine(PanelTop("Errors", 120), 120, lines[16]);
                AssertLine(Box("2× (0.0/s)  Add to cart · Timeout after 30s   first 45s ago · last 20s ago" + Spaces(42)), 120, lines[17]);
                AssertLine(Bottom(120), 120, lines[18]);
                AssertLine(StepDetailFooter, 82, lines[39]);
                AssertMaximumWidth(120, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("Connection refused", line.Text);
            })
            .Step("With two scenarios the detail is still one full-width section for the first: never columns", context =>
            {
                // At 160 columns the four tiles share 157: boxes of 40, 39, 39, 39.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DetailView(1), 160, 40, ColorMode.None);

                AssertLine(RichDetailTitle + Spaces(89) + "  ⚡ TestFuzn", 160, lines[0]);
                AssertLine(CheckoutDetailHeader, 30, lines[1]);
                AssertLine(Row(Top(40), Top(39), Top(39), Top(39)), 160, lines[2]);
                AssertMaximumWidth(160, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("Browse catalog", line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_detail_golden_frame_at_80x24()
    {
        // Below 30 rows the tiles carry no trend: four boxes of 20, 19, 19, 19 (inner 16, 15,
        // 15, 15), two lines each. The logo still fits at 80 (the 58-column title in the 67
        // left of it). Below 100 columns the charts stack at the full width and below 30
        // rows their bodies are four rows; at 80 columns the ticker carries the ages and the
        // 92-column Checkout entry wraps onto two lines. The title, the header, four tile
        // rows and a blank are 7 rows, the two stacked six-row panels 12 and the ticker 4:
        // 23 against 23, nothing to give. The four-row bodies label their top, second and
        // bottom rows ("70" / "35" / "0" and "96 ms" / "93 ms" / "90 ms"); the newest ok
        // count, 9, is level 3 of 16 and its annotation sits on the bottom row, the newest
        // p95, 96, level 16 on the top row. The bodies are the widget's — pinned cell by
        // cell at 120×40 — so this golden holds their axes, annotations and widths.
        await Scenario()
            .Step("At 80 columns and 24 rows the tiles drop their trends, the charts stack with compact bodies, the wrapped error fills the last rows and the footer gives up its help hint", context =>
                {
                    var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(1), 80, 24, ColorMode.None);

                    Assert.HasCount(24, lines);
                    AssertLine(RichDetailTitle + Spaces(9) + "  ⚡ TestFuzn", 80, lines[0]);
                    AssertLine(CheckoutDetailHeader, 30, lines[1]);
                    AssertLine(Row(Top(20), Top(19), Top(19), Top(19)), 80, lines[2]);
                    AssertLine(Row(Box("rps" + Spaces(13)), Box("p95" + Spaces(12)), Box("fail%" + Spaces(10)), Box("count" + Spaces(10))), 80, lines[3]);
                    AssertLine(Row(Box("9.6" + Spaces(13)), Box("96 ms" + Spaces(10)), Box("0.5 %" + Spaces(10)), Box("6260" + Spaces(11))), 80, lines[4]);
                    AssertLine(Row(Bottom(20), Bottom(19), Bottom(19), Bottom(19)), 80, lines[5]);
                    AssertLine(string.Empty, 0, lines[6]);
                    AssertLine(PanelTop(RequestsChartTitle, 80), 80, lines[7]);
                    Assert.StartsWith("│ 70┤", lines[8].Text);
                    Assert.StartsWith("│ 35┤", lines[9].Text);
                    Assert.StartsWith("│   │", lines[10].Text);
                    Assert.StartsWith("│  0┤", lines[11].Text);
                    Assert.EndsWith(Spaces(4) + " │", lines[8].Text);
                    Assert.EndsWith(" ▶ 9 │", lines[11].Text);
                    AssertLine(Bottom(80), 80, lines[12]);
                    AssertLine(PanelTop(StepLatencyChartTitle, 80), 80, lines[13]);
                    Assert.StartsWith("│ 96 ms┤", lines[14].Text);
                    Assert.EndsWith(" ▶ 96 ms │", lines[14].Text);
                    Assert.StartsWith("│ 93 ms┤", lines[15].Text);
                    Assert.StartsWith("│      │", lines[16].Text);
                    Assert.StartsWith("│ 90 ms┤", lines[17].Text);
                    Assert.EndsWith(Spaces(8) + " │", lines[17].Text);
                    AssertLine(Bottom(80), 80, lines[18]);
                    AssertLine(PanelTop("Errors", 80), 80, lines[19]);
                    AssertLine(Box("30× (2.0/s)  Checkout · Connection refused (localhost:7058)   first 1m 12s" + Spaces(2)), 80, lines[20]);
                    AssertLine(Box(Spaces(13) + "ago · last 2s ago" + Spaces(46)), 80, lines[21]);
                    AssertLine(Bottom(80), 80, lines[22]);
                    AssertLine(StepDetailFooterAt73, 73, lines[23]);
                    AssertMaximumWidth(80, lines);
                    for (var row = 7; row <= 22; row++)
                        Assert.AreEqual(80, lines[row].Width, $"Row {row} is not a full panel row");
                })
            .Run();
    }

    [Test]
    public async Task Verify_step_detail_of_a_step_without_series_shows_no_data_and_blank_charts()
    {
        // The fresh step has no series, no request and no error: every tile is no data (the
        // count 0), the trend rows blank; the charts have no finite value — tick-only axes,
        // blank bodies, no annotation — and there is no ticker. No phase label on the title.
        await Scenario()
            .Step("Every tile reads no data or zero over a blank trend row, both chart bodies are blank behind tick-only axes, and nothing follows them", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { FreshStepSnapshot() }, DetailView(0), 120, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine("Fresh  ● Running" + Spaces(91) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine("step 1/1 · Fresh step · Esc back", 32, lines[1]);
                AssertLine(Row(Box("rps" + Spaces(23)), Box("p95" + Spaces(22)), Box("fail%" + Spaces(20)), Box("count" + Spaces(20))), 120, lines[3]);
                AssertLine(Row(Box("—" + Spaces(25)), Box("—" + Spaces(24)), Box("—" + Spaces(24)), Box("0" + Spaces(24))), 120, lines[4]);
                AssertLine(Row(Box(Spaces(26)), Box(Spaces(25)), Box(Spaces(25)), Box(Spaces(25))), 120, lines[5]);
                AssertLine(Row(Bottom(30), Bottom(29), Bottom(29), Bottom(29)), 120, lines[6]);
                AssertLine(string.Empty, 0, lines[7]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(StepLatencyChartTitle, 59), 120, lines[8]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("┤" + Spaces(54)), 120, lines[9]);
                AssertLine(Box("│" + Spaces(54)) + "  " + Box("│" + Spaces(54)), 120, lines[10]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("┤" + Spaces(54)), 120, lines[11]);
                AssertLine(Box("│" + Spaces(54)) + "  " + Box("│" + Spaces(54)), 120, lines[12]);
                AssertLine(Box("│" + Spaces(54)) + "  " + Box("│" + Spaces(54)), 120, lines[13]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("┤" + Spaces(54)), 120, lines[14]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, lines[15]);
                AssertLine(string.Empty, 0, lines[16]);
                AssertLine(string.Empty, 0, lines[38]);
                AssertLine(StepDetailFooter, 82, lines[39]);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain("Errors", line.Text);
                    Assert.DoesNotContain("▶", line.Text);
                }
            })
            .Step("TrueColor: the no-data values are bare bold, Neutral, and the zero count too", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { FreshStepSnapshot() }, DetailView(0), 120, 40, ColorMode.TrueColor);

                AssertLine(Row(Box(Sgr(Bold, "—") + Spaces(25)), Box(Sgr(Bold, "—") + Spaces(24)), Box(Sgr(Bold, "—") + Spaces(24)), Box(Sgr(Bold, "0") + Spaces(24))), 120, lines[4]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_detail_of_a_step_without_errors_has_no_errors_panel()
    {
        // Browse, the three-step run's third step, sorts last: step 3/3. Its rate is flat at
        // 60 (no delta over ten equal samples; a flat trend at the middle level, ⣤ in the
        // right five columns) and its p95 flat at 20 ms (Neutral, the same flat trend); no
        // failure, so fail% is 0.0 in Ok green; count 6000. It carries no delta series, so
        // the requests chart is blank, while the flat p95 fills the lower half of its chart
        // at the middle level (12 of 24: rows 3-5) with every axis label 20 ms and the
        // annotation on the row of level 12, the fourth. No error names Browse, so nothing
        // follows the charts.
        await Scenario()
            .Step("The header counts Browse third, the fail% tile is a green zero, the p95 chart is a flat half-height band and no Errors panel follows", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { ThreeStepSnapshot() }, DetailView(2), 120, 40, ColorMode.None);

                AssertLine("step 3/3 · Browse · Esc back", 28, lines[1]);
                AssertLine(Row(Box("60" + Spaces(24)), Box("20 ms" + Spaces(20)), Box("0.0 %" + Spaces(20)), Box("6000" + Spaces(21))), 120, lines[4]);
                AssertLine(Row(Box(Spaces(21) + "⣤⣤⣤⣤⣤"), Box(Spaces(20) + "⣤⣤⣤⣤⣤"), Box(Spaces(25)), Box(Spaces(25))), 120, lines[5]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(StepLatencyChartTitle, 59), 120, lines[8]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("20 ms┤" + Spaces(49)), 120, lines[9]);
                AssertLine(Box("│" + Spaces(54)) + "  " + Box("     │" + Spaces(49)), 120, lines[10]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("20 ms┤" + Spaces(49)), 120, lines[11]);
                AssertLine(Box("│" + Spaces(54)) + "  " + Box("     │" + Glyphs('⣿', 41) + " ▶ 20 ms"), 120, lines[12]);
                AssertLine(Box("│" + Spaces(54)) + "  " + Box("     │" + Glyphs('⣿', 41) + Spaces(8)), 120, lines[13]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("20 ms┤" + Glyphs('⣿', 41) + Spaces(8)), 120, lines[14]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, lines[15]);
                AssertLine(string.Empty, 0, lines[16]);
                AssertLine(StepDetailFooter, 82, lines[39]);

                foreach (var line in lines)
                    Assert.DoesNotContain("Errors", line.Text);

                var styled = LiveDashboardLayout.Render(new[] { ThreeStepSnapshot() }, DetailView(2), 120, 40, ColorMode.TrueColor);
                Assert.Contains(Box(Sgr(BoldGreen, "0.0") + " " + Sgr(Dim, "%") + Spaces(20)), styled[4].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_detail_shows_the_failure_reason_under_the_tiles()
    {
        // The failed run's one step has no series: rps and p95 read no data, fail% is 5 of
        // 100 — "5.0" in Critical red — and count 100, over a blank trend row. The reason
        // follows the tiles' bottom border, before the blank, as the overview draws it under
        // the timeline: the title (35 columns with the phase, the logo after it), the header,
        // five tile rows, the reason and a blank are 9 rows, so the charts — blank behind
        // tick-only axes, and no error — are rows 9-16.
        await Scenario()
            .Step("The reason line sits between the tiles and the blank line, in the overview's format, and the rest of the detail is as it would be without it", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { FailedStepSnapshot() }, DetailView(0), 120, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine("Checkout flow  ● Failed · completed" + Spaces(72) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine("step 1/1 · Checkout · Esc back", 30, lines[1]);
                AssertLine(Row(Top(30), Top(29), Top(29), Top(29)), 120, lines[2]);
                AssertLine(Row(Box("—" + Spaces(25)), Box("—" + Spaces(24)), Box("5.0 %" + Spaces(20)), Box("100" + Spaces(22))), 120, lines[4]);
                AssertLine(Row(Bottom(30), Bottom(29), Bottom(29), Bottom(29)), 120, lines[6]);
                AssertLine("✗ Assert.IsLessThan failed. p95 too high: 240 ms", 48, lines[7]);
                AssertLine(string.Empty, 0, lines[8]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(StepLatencyChartTitle, 59), 120, lines[9]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("┤" + Spaces(54)), 120, lines[10]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, lines[16]);
                AssertLine(string.Empty, 0, lines[17]);
                AssertLine(StepDetailFooter, 82, lines[39]);
                Assert.AreEqual(1, CountLinesContaining(lines, "✗"));
                AssertMaximumWidth(120, lines);
            })
            .Step("TrueColor: the reason is red, as in the overview, and the fail% value with it", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { FailedStepSnapshot() }, DetailView(0), 120, 40, ColorMode.TrueColor);

                AssertLine(Sgr(Red, "✗ Assert.IsLessThan failed. p95 too high: 240 ms"), 48, lines[7]);
                Assert.Contains(Box(Sgr(BoldRed, "5.0") + " " + Sgr(Dim, "%") + Spaces(20)), lines[4].Text);
            })
            .Step("The clipping cuts the reason before the tiles: at 8 rows it is the last content row, at 7 the tiles' bottom border is", context =>
            {
                // Below 30 rows the tiles carry no trend: the title, the header, four tile
                // rows, the reason and a blank are 8 fixed rows, the compact side-by-side
                // charts 6, and there is no error to give — against 7 rows above the footer
                // the charts go and the clipping takes the blank; against 6 it takes the
                // reason too.
                var reasonLast = LiveDashboardLayout.Render(new[] { FailedStepSnapshot() }, DetailView(0), 120, 8, ColorMode.None);
                Assert.HasCount(8, reasonLast);
                AssertLine(Row(Bottom(30), Bottom(29), Bottom(29), Bottom(29)), 120, reasonLast[5]);
                AssertLine("✗ Assert.IsLessThan failed. p95 too high: 240 ms", 48, reasonLast[6]);
                AssertLine(StepDetailFooter, 82, reasonLast[7]);

                var reasonCut = LiveDashboardLayout.Render(new[] { FailedStepSnapshot() }, DetailView(0), 120, 7, ColorMode.None);
                Assert.HasCount(7, reasonCut);
                AssertLine(Row(Bottom(30), Bottom(29), Bottom(29), Bottom(29)), 120, reasonCut[5]);
                AssertLine(StepDetailFooter, 82, reasonCut[6]);
                Assert.AreEqual(0, CountLinesContaining(reasonCut, "✗"));
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_rps_delta_is_hidden_against_a_warmup_zero()
    {
        // The step's rps tile is the first of four boxes at 120 columns, inner width 26: the
        // label line carries the delta right-aligned when there is one. The overview's rps
        // tile is the first of five, inner width 20, its label on row 2 and its value on 3.
        await Scenario()
            .Step("Ten measurement samples in, the step's delta would compare 49 against the last warmup zero: the label carries no arrow while the value reads 49", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { WarmupZeroSnapshot(10) }, DetailView(0), 120, 40, ColorMode.None);

                AssertLine("step 1/1 · Ramp · Esc back", 26, lines[1]);
                Assert.StartsWith(Box("rps" + Spaces(23)), lines[3].Text);
                Assert.StartsWith(Box("49" + Spaces(24)), lines[4].Text);
                Assert.DoesNotContain("▲", lines[3].Text);
            })
            .Step("One sample later both endpoints are measurement rates, 40 and 50, and the delta shows: ▲ 10, green", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { WarmupZeroSnapshot(11) }, DetailView(0), 120, 40, ColorMode.None);
                Assert.StartsWith(Box("rps" + Spaces(19) + "▲ 10"), lines[3].Text);
                Assert.StartsWith(Box("50" + Spaces(24)), lines[4].Text);

                var styled = LiveDashboardLayout.Render(new[] { WarmupZeroSnapshot(11) }, DetailView(0), 120, 40, ColorMode.TrueColor);
                Assert.Contains(Sgr(Green, "▲ 10"), styled[3].Text);
            })
            .Step("The overview's rps tile keeps the plain rule over the same series — the scenario's series carries real warmup rates — so its ▲ 49 against the zero stands", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { WarmupZeroSnapshot(10) }, DefaultView, 120, 40, ColorMode.None);
                Assert.StartsWith(Box("rps" + Spaces(13) + "▲ 49"), lines[2].Text);
                Assert.StartsWith(Box("49" + Spaces(18)), lines[3].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_count_tile_shows_the_skipped_count_when_it_fits()
    {
        await Scenario()
            .Step("Step 1's four billion requests and three skipped iterations fit the 25-column box as '4000000000 skipped 3', the unit dim; Control [step]'s skipped count of two billion would make the line 29 columns, so its value stands alone", context =>
            {
                // FitUnit: the value, a space and the unit against the box's inner width —
                // 10 + 1 + 9 = 20 ≤ 25 for Step 1, 10 + 1 + 18 = 29 > 25 for Control [step].
                var fitting = LiveDashboardLayout.Render(new[] { HostileDetailSnapshot() }, DetailView(3), 120, 40, ColorMode.None);
                Assert.Contains(Box("4000000000 skipped 3" + Spaces(5)), fitting[4].Text);

                var styled = LiveDashboardLayout.Render(new[] { HostileDetailSnapshot() }, DetailView(3), 120, 40, ColorMode.TrueColor);
                Assert.Contains(Box(Sgr(Bold, "4000000000") + " " + Sgr(Dim, "skipped 3") + Spaces(5)), styled[4].Text);

                var dropped = LiveDashboardLayout.Render(new[] { HostileDetailSnapshot() }, DetailView(1), 120, 40, ColorMode.None);
                Assert.Contains(Box("4294967294" + Spaces(15)), dropped[4].Text);
                Assert.DoesNotContain("skipped", dropped[4].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_detail_header_counts_the_displayed_position()
    {
        await Scenario()
            .Step("The position is the step's row in the displayed order, so a sort change moves it while the selection stays: Add to cart is 2/6 while Checkout's p95 is higher and 1/6 once its own overtakes; Logout, without a p95 reading, is always last", context =>
            {
                var before = LiveDashboardLayout.Render(new[] { PainSnapshot() }, DetailView(2), 120, 40, ColorMode.None);
                AssertLine("step 2/6 · Add to cart · Esc back", 33, before[1]);

                var after = LiveDashboardLayout.Render(new[] { PainSnapshot(addToCartPercentile95: 100) }, DetailView(2), 120, 40, ColorMode.None);
                AssertLine("step 1/6 · Add to cart · Esc back", 33, after[1]);

                var logout = LiveDashboardLayout.Render(new[] { PainSnapshot() }, DetailView(4), 120, 40, ColorMode.None);
                AssertLine("step 6/6 · Logout · Esc back", 28, logout[1]);
                Assert.Contains(Box("—" + Spaces(24)), logout[4].Text);
            })
            .Step("A step name with markup and control characters renders literally, and a long one is cut with an ellipsis before the hint", context =>
            {
                // The hostile steps sort Control [step] (half its requests failed, a p95
                // reading), Step 1 (half failed, none), the 200-n step (no failure, a
                // reading) and Long (no failure — its counts do not add up — no reading).
                var lines = LiveDashboardLayout.Render(new[] { HostileDetailSnapshot() }, DetailView(3), 120, 40, ColorMode.TrueColor);
                AssertLine(Sgr(BoldAccent, "step 2/4") + " " + Sgr(Dim, "·") + " " + Sgr(Bold, "[bold]Step 1[/]") + " " + Sgr(Dim, "·") + " " + Sgr(Dim, "Esc back"), 37, lines[1]);

                var cut = LiveDashboardLayout.Render(new[] { HostileDetailSnapshot() }, DetailView(2), 60, 40, ColorMode.None);
                AssertLine("step 3/4 · " + new string('n', 48) + "…", 60, cut[1]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_detail_shows_a_notice_when_the_selection_names_no_step()
    {
        await Scenario()
            .Step("Without a scenario the notice is the first row, over blank rows and the step detail's footer", context =>
            {
                var lines = LiveDashboardLayout.Render(Array.Empty<LiveMetricsSnapshot>(), DetailView(0), 40, 5, ColorMode.None);

                Assert.HasCount(5, lines);
                AssertLine(LiveDashboardLayout.NoStepSelectedNoticeText, 27, lines[0]);
                AssertLine(string.Empty, 0, lines[1]);
                AssertLine(string.Empty, 0, lines[3]);
                AssertLine("Esc back · ↑↓ step · 1 overview · q quit", 40, lines[4]);
            })
            .Step("Without a step, with a stale index past the steps, a negative one, or no selection at all, the notice follows the title line and nothing else renders", context =>
            {
                var noSteps = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, DetailView(0), 120, 40, ColorMode.None);
                Assert.HasCount(40, noSteps);
                AssertLine("Browse catalog  ● Running · warmup: Fixed Load 50 rps" + Spaces(54) + "  ⚡ TestFuzn", 120, noSteps[0]);
                AssertLine(LiveDashboardLayout.NoStepSelectedNoticeText, 27, noSteps[1]);
                AssertLine(string.Empty, 0, noSteps[2]);
                AssertLine(string.Empty, 0, noSteps[38]);
                AssertLine(StepDetailFooter, 82, noSteps[39]);

                foreach (var selection in new int?[] { 7, -1, null })
                {
                    var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(selection), 120, 40, ColorMode.None);

                    Assert.HasCount(40, lines, $"Row count for selection {selection}");
                    AssertLine(RichDetailTitle + Spaces(49) + "  ⚡ TestFuzn", 120, lines[0]);
                    AssertLine(LiveDashboardLayout.NoStepSelectedNoticeText, 27, lines[1]);
                    for (var row = 2; row < 39; row++)
                        AssertLine(string.Empty, 0, lines[row]);
                    AssertLine(StepDetailFooter, 82, lines[39]);
                }
            })
            .Step("TrueColor: the notice is dim", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(null), 120, 40, ColorMode.TrueColor);

                AssertLine(Sgr(Dim, LiveDashboardLayout.NoStepSelectedNoticeText), 27, lines[1]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_time_window_applies_to_the_step_charts_but_not_the_step_tiles()
    {
        // The window step's requests chart is the overview's window chart, cell for cell: a
        // 55-column panel interior with the axis "100┤" / " 50┤" / "  0┤", the annotation
        // " ▶ 100" and a 45-cell body of 90 columns. The p95 chart is flat at 40 ms: every
        // level the middle one, so the area fills rows 3-5 of the six, every axis label reads
        // 40 ms and the annotation sits on row 3, with and without the window. The title, the
        // header, five tile rows and a blank put the charts on rows 8-15 under an unbounded
        // height; no error, so the footer follows on row 16.
        await Scenario()
            .Step("Without a window the requests chart scrolls: its newest 90 samples are 60 at 0 — the bottom dot alone — and 30 at 100, full; the p95 band is flat at half height", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { WindowStepSnapshot() }, DetailView(0), 120, 0, ColorMode.None);

                Assert.HasCount(17, lines);
                AssertLine("Window  ● Running · Fixed Load 100 rps" + Spaces(69) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine("step 1/1 · Window step · Esc back", 33, lines[1]);
                AssertRequestsChart(lines, 9,
                    "100┤" + Spaces(30) + Glyphs('⣿', 15) + " ▶ 100",
                    "   │" + Spaces(30) + Glyphs('⣿', 15) + Spaces(6),
                    " 50┤" + Spaces(30) + Glyphs('⣿', 15) + Spaces(6),
                    "   │" + Spaces(30) + Glyphs('⣿', 15) + Spaces(6),
                    "   │" + Spaces(30) + Glyphs('⣿', 15) + Spaces(6),
                    "  0┤" + Glyphs('⣀', 30) + Glyphs('⣿', 15) + Spaces(6));
                Assert.EndsWith("  " + Box("40 ms┤" + Spaces(49)), lines[9].Text);
                Assert.EndsWith("  " + Box("     │" + Spaces(49)), lines[10].Text);
                Assert.EndsWith("  " + Box("40 ms┤" + Spaces(49)), lines[11].Text);
                Assert.EndsWith("  " + Box("     │" + Glyphs('⣿', 41) + " ▶ 40 ms"), lines[12].Text);
                Assert.EndsWith("  " + Box("     │" + Glyphs('⣿', 41) + Spaces(8)), lines[13].Text);
                Assert.EndsWith("  " + Box("40 ms┤" + Glyphs('⣿', 41) + Spaces(8)), lines[14].Text);
                AssertLine(StepDetailFooter, 82, lines[16]);
            })
            .Step("A window of 60 samples stretches the newest 60 over the 90 columns, the step interpolated across the two columns between samples 29 and 30, and the flat band reads the same", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { WindowStepSnapshot() }, DetailView(0) with { TimeWindow = 60 }, 120, 0, ColorMode.None);

                AssertRequestsChart(lines, 9,
                    "100┤" + Spaces(23) + Glyphs('⣿', 22) + " ▶ 100",
                    "   │" + Spaces(22) + "⢸" + Glyphs('⣿', 22) + Spaces(6),
                    " 50┤" + Spaces(22) + "⢸" + Glyphs('⣿', 22) + Spaces(6),
                    "   │" + Spaces(22) + "⢸" + Glyphs('⣿', 22) + Spaces(6),
                    "   │" + Spaces(22) + "⣸" + Glyphs('⣿', 22) + Spaces(6),
                    "  0┤" + Glyphs('⣀', 22) + Glyphs('⣿', 23) + Spaces(6));
                Assert.EndsWith("  " + Box("     │" + Glyphs('⣿', 41) + " ▶ 40 ms"), lines[12].Text);
                Assert.EndsWith("  " + Box("40 ms┤" + Glyphs('⣿', 41) + Spaces(8)), lines[14].Text);
            })
            .Step("The tiles keep their own windows: the rows are the same under a window of 60, of 5 — narrower than the delta's ten samples, whose ▲ 12 still shows — and of 300, wider than the run", context =>
            {
                var whole = LiveDashboardLayout.Render(new[] { WindowStepSnapshot() }, DetailView(0), 120, 0, ColorMode.None);
                Assert.StartsWith(Box("rps" + Spaces(19) + "▲ 12"), whole[3].Text);

                foreach (var timeWindow in new[] { 60, 5, 300 })
                {
                    var windowed = LiveDashboardLayout.Render(new[] { WindowStepSnapshot() }, DetailView(0) with { TimeWindow = timeWindow }, 120, 0, ColorMode.None);
                    for (var row = 0; row <= 7; row++)
                        AssertLine(whole[row].Text, whole[row].Width, windowed[row]);
                }

                var wide = LiveDashboardLayout.Render(new[] { WindowStepSnapshot() }, DetailView(0) with { TimeWindow = 300 }, 120, 0, ColorMode.None);
                Assert.HasCount(whole.Count, wide);
                for (var row = 0; row < whole.Count; row++)
                    AssertLine(whole[row].Text, whole[row].Width, wide[row]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_step_detail_height_drop_order()
    {
        // The many-errors run's step S with twenty errors at 120 columns: the title ("Many
        // ● Running", no phase), the header ("step 1/1 · S · Esc back"), the tiles — every
        // value no data or 0, three lines each from 30 rows of height (a blank trend row),
        // two below — and a blank are 8 fixed rows from 30 rows and 7 below; the charts 8
        // rows tall and 6 compact; the ticker's twenty one-line entries 22 rows, "20×" making
        // every count two columns wide.
        await Scenario()
            .Step("At 30 rows the chart bodies compact first, then the entries go least recently active first: twelve stay over '+8 more'", context =>
            {
                // 8 + 8 + 22 = 38 against 29, nine over: the pair compacts together for two,
                // and the entries pay the seven — E20 for the more line, then E19 to E13 a
                // row each — so the charts are rows 8-13 and the ticker 14-28.
                var lines = LiveDashboardLayout.Render(new[] { ManyErrorsStepSnapshot(20) }, DetailView(0), 120, 30, ColorMode.None);

                Assert.HasCount(30, lines);
                AssertLine("Many  ● Running" + Spaces(92) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine("step 1/1 · S · Esc back", 23, lines[1]);
                AssertLine(Row(Box(Spaces(26)), Box(Spaces(25)), Box(Spaces(25)), Box(Spaces(25))), 120, lines[5]);
                AssertLine(string.Empty, 0, lines[7]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(StepLatencyChartTitle, 59), 120, lines[8]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, lines[13]);
                AssertLine(PanelTop("Errors", 120), 120, lines[14]);
                AssertLine(Box(" 1× (0.0/s)  S · E1" + Spaces(97)), 120, lines[15]);
                AssertLine(Box("12× (0.0/s)  S · E12" + Spaces(96)), 120, lines[26]);
                AssertLine(Box("+8 more" + Spaces(109)), 120, lines[27]);
                AssertLine(Bottom(120), 120, lines[28]);
                AssertLine(StepDetailFooter, 82, lines[29]);
            })
            .Step("At 23 rows the bodies are compact by height and the entries pay it all: six stay over '+14 more'; at 18 one entry and the more line are all the ticker keeps", context =>
            {
                // 7 + 6 + 22 = 35 against 22, thirteen over: E20 for the more line and E19 to
                // E7 a row each. Against 17 the ticker trims to E1 and "+19 more" — eighteen
                // rows, the overrun exactly — at rows 13-16.
                var six = LiveDashboardLayout.Render(new[] { ManyErrorsStepSnapshot(20) }, DetailView(0), 120, 23, ColorMode.None);
                Assert.HasCount(23, six);
                AssertLine(Row(Bottom(30), Bottom(29), Bottom(29), Bottom(29)), 120, six[5]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(StepLatencyChartTitle, 59), 120, six[7]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, six[12]);
                AssertLine(PanelTop("Errors", 120), 120, six[13]);
                AssertLine(Box(" 1× (0.0/s)  S · E1" + Spaces(97)), 120, six[14]);
                AssertLine(Box(" 6× (0.0/s)  S · E6" + Spaces(97)), 120, six[19]);
                AssertLine(Box("+14 more" + Spaces(108)), 120, six[20]);
                AssertLine(Bottom(120), 120, six[21]);
                AssertLine(StepDetailFooter, 82, six[22]);

                var one = LiveDashboardLayout.Render(new[] { ManyErrorsStepSnapshot(20) }, DetailView(0), 120, 18, ColorMode.None);
                Assert.HasCount(18, one);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, one[12]);
                AssertLine(PanelTop("Errors", 120), 120, one[13]);
                AssertLine(Box(" 1× (0.0/s)  S · E1" + Spaces(97)), 120, one[14]);
                AssertLine(Box("+19 more" + Spaces(108)), 120, one[15]);
                AssertLine(Bottom(120), 120, one[16]);
                AssertLine(StepDetailFooter, 82, one[17]);
            })
            .Step("At 17 rows the latency chart goes — side by side it frees nothing — and the requests chart after it, the trimmed ticker moving up; at 11 the Errors panel goes whole; at 7 the clipping cuts the blank line", context =>
            {
                var chartsGone = LiveDashboardLayout.Render(new[] { ManyErrorsStepSnapshot(20) }, DetailView(0), 120, 17, ColorMode.None);
                Assert.HasCount(17, chartsGone);
                AssertLine(string.Empty, 0, chartsGone[6]);
                AssertLine(PanelTop("Errors", 120), 120, chartsGone[7]);
                AssertLine(Box(" 1× (0.0/s)  S · E1" + Spaces(97)), 120, chartsGone[8]);
                AssertLine(Box("+19 more" + Spaces(108)), 120, chartsGone[9]);
                AssertLine(Bottom(120), 120, chartsGone[10]);
                AssertLine(string.Empty, 0, chartsGone[11]);
                AssertLine(string.Empty, 0, chartsGone[15]);
                AssertLine(StepDetailFooter, 82, chartsGone[16]);
                foreach (var line in chartsGone)
                {
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
                    Assert.DoesNotContain(StepLatencyChartTitle, line.Text);
                }

                var errorsGone = LiveDashboardLayout.Render(new[] { ManyErrorsStepSnapshot(20) }, DetailView(0), 120, 11, ColorMode.None);
                Assert.HasCount(11, errorsGone);
                AssertLine(Row(Bottom(30), Bottom(29), Bottom(29), Bottom(29)), 120, errorsGone[5]);
                AssertLine(string.Empty, 0, errorsGone[6]);
                AssertLine(string.Empty, 0, errorsGone[9]);
                AssertLine(StepDetailFooter, 82, errorsGone[10]);
                foreach (var line in errorsGone)
                    Assert.DoesNotContain("Errors", line.Text);

                var clipped = LiveDashboardLayout.Render(new[] { ManyErrorsStepSnapshot(20) }, DetailView(0), 120, 7, ColorMode.None);
                Assert.HasCount(7, clipped);
                AssertLine("step 1/1 · S · Esc back", 23, clipped[1]);
                AssertLine(Row(Bottom(30), Bottom(29), Bottom(29), Bottom(29)), 120, clipped[5]);
                AssertLine(StepDetailFooter, 82, clipped[6]);
            })
            .Step("Stacked, the latency chart goes alone: at 80 columns and 22 rows the requests chart stays over the trimmed ticker", context =>
            {
                // 7 fixed rows, two stacked six-row panels and the 22-row ticker are 41
                // against 21, twenty over: the entries pay eighteen down to E1 and the more
                // line, and the latency chart's six cover the two still owed — the requests
                // chart at rows 7-12, the ticker at 13-16 and rows 17-20 blank.
                var lines = LiveDashboardLayout.Render(new[] { ManyErrorsStepSnapshot(20) }, DetailView(0), 80, 22, ColorMode.None);

                Assert.HasCount(22, lines);
                AssertLine(PanelTop(RequestsChartTitle, 80), 80, lines[7]);
                AssertLine(Bottom(80), 80, lines[12]);
                AssertLine(PanelTop("Errors", 80), 80, lines[13]);
                AssertLine(Box(" 1× (0.0/s)  S · E1" + Spaces(57)), 80, lines[14]);
                AssertLine(Box("+19 more" + Spaces(68)), 80, lines[15]);
                AssertLine(Bottom(80), 80, lines[16]);
                AssertLine(string.Empty, 0, lines[17]);
                AssertLine(string.Empty, 0, lines[20]);
                AssertLine(StepDetailFooterAt73, 73, lines[21]);
                foreach (var line in lines)
                    Assert.DoesNotContain(StepLatencyChartTitle, line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_hostile_step_details_never_break_the_frame()
    {
        await Scenario()
            .Step("At every other width from 1 to 220 (and the odd boundaries: the quit hint's, the footer's hint edges, the chart, trend and tile-row edges) and every height from 1 to 60, for no selection, every step and a stale index, the frame is exactly the height, the footer owns the last row (the quit hint cut to the width below its six columns), no line exceeds the width and no surrogate pair is split", context =>
            {
                var snapshots = new[] { HostileDetailSnapshot() };
                var boundaries = new[] { 5, 7, 17, 27, 51, 59, 61, 73, 79, 99 };
                foreach (var selection in new int?[] { null, 0, 1, 2, 3, 99, -1 })
                {
                    var viewState = DetailView(selection);
                    for (var width = 1; width <= 220; width++)
                    {
                        if (width % 2 != 0 && Array.IndexOf(boundaries, width) < 0)
                            continue;

                        for (var height = 1; height <= 60; height++)
                        {
                            var lines = LiveDashboardLayout.Render(snapshots, viewState, width, height, ColorMode.TrueColor);

                            Assert.HasCount(height, lines, $"Row count at {width}×{height} for selection {selection}");
                            Assert.AreEqual(ExpectedStepDetailFooterWidth(width), lines[height - 1].Width, $"Footer at {width}×{height} for selection {selection}");
                            if (width >= 6)
                                Assert.EndsWith(Sgr(Bold, "q") + " quit", lines[height - 1].Text, $"Footer at {width}×{height} for selection {selection}");
                            AssertMaximumWidth(width, lines);
                            AssertNoLoneSurrogate(lines, $"{width}×{height} for selection {selection}");
                        }
                    }
                }
            })
            .Step("Color mode None renders every step's detail without an escape or a control character, every line's text at exactly its declared width", context =>
            {
                for (var selection = 0; selection < 4; selection++)
                {
                    var lines = LiveDashboardLayout.Render(new[] { HostileDetailSnapshot() }, DetailView(selection), 120, 40, ColorMode.None);

                    Assert.HasCount(40, lines);
                    Assert.StartsWith("step ", lines[1].Text);
                    Assert.Contains("/4 · ", lines[1].Text);
                    for (var row = 0; row < lines.Count; row++)
                    {
                        var text = lines[row].Text;
                        Assert.DoesNotContain("\u001b", text, $"Escape on row {row} for step {selection}");
                        Assert.DoesNotContain("\t", text, $"Tab on row {row} for step {selection}");
                        Assert.DoesNotContain("\0", text, $"NUL on row {row} for step {selection}");
                        Assert.DoesNotContain("\r", text, $"CR on row {row} for step {selection}");
                        Assert.DoesNotContain("\n", text, $"LF on row {row} for step {selection}");
                        Assert.AreEqual(lines[row].Width, text.Length + CountOccurrences(text, "⚡"), $"Declared width of row {row} for step {selection}");
                    }
                }
            })
            .Step("The Long step's entry — a count past the capacity, a rate of a billion and a 5000-character message — wraps to three lines under an ellipsis in its detail, and the count tile's ten digits stay whole", context =>
            {
                // Long is displayed last (no failure once its counts are clamped, no p95
                // reading). The detail's ticker is the step's one entry: the last space that
                // fits the 116-column panel is the one after the dot, so the first line is
                // "2000000000× (1000000000/s)  Long ·" alone, and the message continues under
                // the name — an indent of the 10-digit count, its ×, the 14-column rate and
                // the separators, 28 — 88 columns a line, the third cut to 87 and the
                // ellipsis; the ages never fit a line the message fills. The count tile:
                // 5 ok and −1 failed are 4.
                var lines = LiveDashboardLayout.Render(new[] { HostileDetailSnapshot() }, DetailView(0), 120, 40, ColorMode.None);
                var errors = IndexOfPanel(lines, "Errors");

                AssertLine(Box("2000000000× (1000000000/s)  Long ·" + Spaces(82)), 120, lines[errors + 1]);
                AssertLine(Box(Spaces(28) + new string('x', 88)), 120, lines[errors + 2]);
                AssertLine(Box(Spaces(28) + new string('x', 87) + "…"), 120, lines[errors + 3]);
                AssertLine(Bottom(120), 120, lines[errors + 4]);
                Assert.Contains("step 4/4 · Long · Esc back", lines[1].Text);
                Assert.Contains(Box("4" + Spaces(24)), lines[4].Text);

                var control = LiveDashboardLayout.Render(new[] { HostileDetailSnapshot() }, DetailView(1), 120, 40, ColorMode.None);
                Assert.Contains("step 1/4 · Control [step] · Esc back", control[1].Text);
                Assert.Contains(Box("4294967294" + Spaces(15)), control[4].Text);
                Assert.Contains("50 %", control[4].Text);
                Assert.Contains("3× (—/s)  Control [step] · [bold]Boom[/]  [31mred [0m  tail ", control[IndexOfPanel(control, "Errors") + 1].Text);
            })
            .Run();
    }
}
