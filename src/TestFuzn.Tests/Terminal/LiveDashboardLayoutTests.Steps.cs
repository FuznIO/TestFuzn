using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// The steps table and error ticker goldens of <see cref="LiveDashboardLayout"/>: the pain
/// sort, the selection highlight under a <see cref="LiveDashboardViewState"/>, the interval
/// p95 and its no-data em dash, the trend column and the tiers that drop it, the ticker's
/// line format and what the width strips from it, wrapping and its cap, hostile messages,
/// the "+N more" lines, the tables' minimum and the panels that go whole. Derived by hand as
/// the main file's summary describes: a table column is as wide as its widest cell or header,
/// columns are joined by two spaces, and a braille trend packs two samples per column.
/// </summary>
public partial class LiveDashboardLayoutTests
{
    /// <summary>The pain snapshot's rows in the widest tier, most painful first, without their pointer column: 72 columns each.</summary>
    private const string CheckoutPainRow = "Checkout      1000   10  10 ms   90 ms      20  █░░░░ 2.0%             ⣤";
    private const string AddToCartPainRow = "Add to cart   1000   10  10 ms   30 ms      20  █░░░░ 2.0%             ⣤";
    private const string LoginPainRow = "Login         1000   10  10 ms   50 ms      10  █░░░░ 1.0%             ⣤";
    private const string BrowsePainRow = "Browse        1000   10  10 ms  120 ms       0  ░░░░░ 0%               ⣤";
    private const string SearchPainRow = "Search        1000   10  10 ms  120 ms       0  ░░░░░ 0%               ⣤";
    private const string LogoutPainRow = "Logout        1000   10  10 ms       —       0  ░░░░░ 0%               ⣤";

    /// <summary>
    /// Six steps, declared Login, Browse, Add to cart, Checkout, Logout, Search, whose pain
    /// order differs from every other: Login fails 1.0 % (10 of 1000) with an interval p95 of
    /// 50 ms, Browse and Search never fail at 120 ms, Add to cart and Checkout both fail
    /// 2.0 % (20 of 1000) at 30 and 90 ms, and Logout never fails and has no interval p95 at
    /// all. Every step runs at 10 rps, a two-sample flat series (one middle-level trend
    /// column), with a 10 ms cumulative mean. No plan, no sample.
    /// </summary>
    private static LiveMetricsSnapshot PainSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Pain",
            Steps = new[]
            {
                PainStep("Login", 990, 10, 50),
                PainStep("Browse", 1000, 0, 120),
                PainStep("Add to cart", 980, 20, 30),
                PainStep("Checkout", 980, 20, 90),
                PainStep("Logout", 1000, 0, null),
                PainStep("Search", 1000, 0, 120)
            }
        };
    }

    private static LiveStepMetrics PainStep(string name, int okCount, int failedCount, double? intervalPercentile95)
    {
        var percentile95Series = Array.Empty<double>();
        if (intervalPercentile95 != null)
            percentile95Series = new[] { intervalPercentile95.Value };

        return new LiveStepMetrics
        {
            Name = name,
            RequestCountOk = okCount,
            RequestCountFailed = failedCount,
            RequestsPerSecond = 10,
            ResponseTimeMean = TimeSpan.FromMilliseconds(10),
            ResponseTimePercentile95 = TimeSpan.FromMilliseconds(20),
            RequestsPerSecondSeries = new double[] { 10, 10 },
            ResponseTimePercentile95Series = percentile95Series
        };
    }

    /// <summary>
    /// A run with the given number of errors E1 … En on step S, En seen last n seconds before
    /// the ring's origin, so E1 is the most recently active, each counted n times and no
    /// longer occurring; no sample, so the ticker shows no ages. The distinct count, when
    /// given, says the tracker holds more than the snapshot carries.
    /// </summary>
    private static LiveMetricsSnapshot ManyErrorsSnapshot(int errorCount, int distinctErrorCount = 0)
    {
        var errors = new LiveErrorEntry[errorCount];
        for (var index = 0; index < errorCount; index++)
            errors[index] = new LiveErrorEntry { StepName = "S", Message = "E" + (index + 1), Count = index + 1, FirstSeen = SampleTime(-60), LastSeen = SampleTime(-(index + 1)) };

        return new LiveMetricsSnapshot
        {
            ScenarioName = "Many",
            Errors = errors,
            DistinctErrorCount = distinctErrorCount
        };
    }

    /// <summary>
    /// One sample and one error on the Checkout step with the given message, seen first 30 s
    /// and last 3 s before the sample, seven times (or the given count), at 1.5/s.
    /// </summary>
    private static LiveMetricsSnapshot WrapSnapshot(string message, int count = 7)
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Wrap",
            Samples = new[] { Sample(1, 10, 0, 40) },
            RequestsPerSecondSeries = new double[] { 10 },
            OkDeltaSeries = new double[] { 10 },
            FailedDeltaSeries = new double[] { 0 },
            ResponseTimePercentile95Series = new double[] { 40 },
            Errors = new[]
            {
                new LiveErrorEntry { StepName = "Checkout", Message = message, Count = count, FirstSeen = SampleTime(-29), LastSeen = SampleTime(-2), RatePerSecond = 1.5 }
            }
        };
    }

    private const string LongMessage = "The request to the payment provider timed out after thirty seconds while the order was being finalized";

    /// <summary>A rocket, U+1F680: a surrogate pair, two UTF-16 units the engine counts as two columns.</summary>
    private const string Rocket = "🚀";

    private static string Rockets(int count)
    {
        return string.Concat(Enumerable.Repeat(Rocket, count));
    }

    /// <summary>
    /// Forty-two errors the ticker must survive — an empty message, a 5000-character one
    /// without a space, markup and every kind of control character, a 200-character step name,
    /// rates that are not finite, counts past the capacity, a name and a message of emoji
    /// (surrogate pairs, a 200-rocket run without a space among them) and a CJK name and
    /// message — over a distinct count past the list, and steps with the same hostility: a
    /// bracketed name with a line break, series of NaN and infinities, counts that do not add
    /// up.
    /// </summary>
    private static LiveMetricsSnapshot HostileSnapshot()
    {
        var errors = new LiveErrorEntry[42];
        errors[0] = new LiveErrorEntry { StepName = "Empty", Message = string.Empty, Count = 1, LastSeen = SampleTime(1) };
        errors[1] = new LiveErrorEntry { StepName = "Long", Message = new string('x', 5000), Count = 2000000000, RatePerSecond = 1e9 };
        errors[2] = new LiveErrorEntry { StepName = "Control [step]", Message = "[bold]Boom[/] \u001b[31mred\u001b[0m\r\n\ttail\0", Count = 3, RatePerSecond = double.NaN };
        errors[3] = new LiveErrorEntry { StepName = new string('n', 200), Message = "long name", Count = 4, RatePerSecond = double.PositiveInfinity, FirstSeen = SampleTime(500) };
        for (var index = 4; index < 40; index++)
            errors[index] = new LiveErrorEntry { StepName = "S" + index, Message = "error-" + index + " " + new string('y', index * 7), Count = index, LastSeen = SampleTime(-index), RatePerSecond = index / 3.0 };

        errors[40] = new LiveErrorEntry { StepName = Rocket + " Launch ☃", Message = Rocket + " timeout ☃ " + Rockets(200), Count = 40, LastSeen = SampleTime(-40), RatePerSecond = 0.5 };
        errors[41] = new LiveErrorEntry { StepName = "結帳", Message = "接続がタイムアウトしました 日本語のエラー メッセージ", Count = 41, LastSeen = SampleTime(-41), RatePerSecond = 0.25 };

        return new LiveMetricsSnapshot
        {
            ScenarioName = "Hostile",
            Samples = new[] { Sample(1, 1, 1, 1) },
            RequestsPerSecondSeries = new double[] { 2 },
            OkDeltaSeries = new double[] { 1 },
            FailedDeltaSeries = new double[] { 1 },
            ResponseTimePercentile95Series = new double[] { 1 },
            Errors = errors,
            DistinctErrorCount = 47,
            Steps = new[]
            {
                new LiveStepMetrics { Name = "[bold]Step\r\n1[/]", RequestCountOk = 5, RequestCountFailed = -1, RequestsPerSecond = double.NaN, RequestsPerSecondSeries = new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1 }, ResponseTimePercentile95Series = new[] { double.NaN } },
                new LiveStepMetrics { Name = new string('s', 300), RequestCountOk = int.MaxValue, RequestCountFailed = int.MaxValue, RequestsPerSecond = double.PositiveInfinity, ResponseTimePercentile95Series = new[] { double.PositiveInfinity } },
                new LiveStepMetrics { Name = string.Empty, ResponseTimePercentile95Series = new double[] { 1e300 } }
            }
        };
    }

    [Test]
    public async Task Verify_view_state_defaults_to_no_selection()
    {
        await Scenario()
            .Step("The default view state selects nothing, and a derived state carries its selection", context =>
            {
                Assert.IsNull(LiveDashboardViewState.Default.SelectedStepIndex);

                var selected = LiveDashboardViewState.Default with { SelectedStepIndex = 3 };
                Assert.AreEqual(3, selected.SelectedStepIndex);
                Assert.IsNull(LiveDashboardViewState.Default.SelectedStepIndex);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_rows_sort_by_pain()
    {
        // No plan, no sample: the title, five tile rows (a blank trend row, no gauge) and a
        // blank put the charts on rows 7-14, the heatmap on 15-24, the requests panel on
        // 25-29 and the step table on 30-38 under an unbounded height. The columns: the
        // pointer column and "Add to cart" (12), the counts (5), the rates (3), the means (5),
        // "120 ms" (6), "failed" (6), the 2.0 % bar (10) and the trend (12) — 73 columns.
        await Scenario()
            .Step("The rows come by failure share, then interval p95, then declaration order: the 2.0 % steps by p95, the 1.0 % step, the 0 % steps in declaration order, and the step without a p95 reading last", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { PainSnapshot() }, DefaultView, 120, 0, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine(PanelTop("Steps", 120), 120, lines[30]);
                AssertLine(Box(" step         count  rps   mean     p95  failed  fail%       trend       " + Spaces(43)), 120, lines[31]);
                AssertLine(Box(" " + CheckoutPainRow + Spaces(43)), 120, lines[32]);
                AssertLine(Box(" " + AddToCartPainRow + Spaces(43)), 120, lines[33]);
                AssertLine(Box(" " + LoginPainRow + Spaces(43)), 120, lines[34]);
                AssertLine(Box(" " + BrowsePainRow + Spaces(43)), 120, lines[35]);
                AssertLine(Box(" " + SearchPainRow + Spaces(43)), 120, lines[36]);
                AssertLine(Box(" " + LogoutPainRow + Spaces(43)), 120, lines[37]);
                AssertLine(Bottom(120), 120, lines[38]);
                AssertLine(OverviewFooter, 93, lines[39]);
            })
            .Step("TrueColor: the bar and its percentage are red above one percent, yellow at it, green at zero, the em dash and the trend dim, and no row is highlighted", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { PainSnapshot() }, DefaultView, 120, 0, ColorMode.TrueColor);

                Assert.Contains(Sgr(Red, "█") + Sgr(Dim, "░░░░") + " " + Sgr(Red, "2.0%") + "  " + Sgr(Dim, Spaces(11) + "⣤"), lines[32].Text);
                Assert.Contains(Sgr(Yellow, "█") + Sgr(Dim, "░░░░") + " " + Sgr(Yellow, "1.0%") + "  " + Sgr(Dim, Spaces(11) + "⣤"), lines[34].Text);
                Assert.Contains(Sgr(Dim, "░░░░░") + " " + Sgr(Green, "0%") + Spaces(4) + Sgr(Dim, Spaces(11) + "⣤"), lines[35].Text);
                Assert.Contains("10 ms" + Spaces(7) + Sgr(Dim, "—") + Spaces(7) + "0", lines[37].Text);
                foreach (var line in lines)
                {
                    Assert.DoesNotContain(LiveDashboardLayout.Pointer, line.Text);
                    Assert.DoesNotContain(AnsiCodes.Reverse, line.Text);
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_selected_step_row_is_pointed_at_and_reversed()
    {
        await Scenario()
            .Step("Index 0 marks the most painful row with the pointer and paints the whole row, padding included, as one reverse-video span", context =>
            {
                var viewState = new LiveDashboardViewState { SelectedStepIndex = 0 };

                var plain = LiveDashboardLayout.Render(new[] { PainSnapshot() }, viewState, 120, 0, ColorMode.None);
                AssertLine(Box(LiveDashboardLayout.Pointer + CheckoutPainRow + Spaces(43)), 120, plain[32]);
                AssertLine(Box(" " + AddToCartPainRow + Spaces(43)), 120, plain[33]);
                Assert.DoesNotContain("\u001b", plain[32].Text);

                var styled = LiveDashboardLayout.Render(new[] { PainSnapshot() }, viewState, 120, 0, ColorMode.TrueColor);
                AssertLine("│ " + Sgr(Reverse, LiveDashboardLayout.Pointer + CheckoutPainRow + Spaces(43)) + " │", 120, styled[32]);
                Assert.Contains(Sgr(Red, "█") + Sgr(Dim, "░░░░") + " " + Sgr(Red, "2.0%"), styled[33].Text);
                Assert.DoesNotContain(AnsiCodes.Reverse, styled[33].Text);

                var monochrome = LiveDashboardLayout.Render(new[] { PainSnapshot() }, viewState, 120, 0, ColorMode.Monochrome);
                AssertLine("│ " + Sgr(Reverse, LiveDashboardLayout.Pointer + CheckoutPainRow + Spaces(43)) + " │", 120, monochrome[32]);
            })
            .Step("The last index highlights the last row, the one without a p95 reading", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { PainSnapshot() }, new LiveDashboardViewState { SelectedStepIndex = 5 }, 120, 0, ColorMode.TrueColor);

                AssertLine("│ " + Sgr(Reverse, LiveDashboardLayout.Pointer + LogoutPainRow + Spaces(43)) + " │", 120, lines[37]);
                Assert.DoesNotContain(AnsiCodes.Reverse, lines[32].Text);
            })
            .Step("An index past the last row is clamped to the last row, and a negative one to the first", context =>
            {
                var past = LiveDashboardLayout.Render(new[] { PainSnapshot() }, new LiveDashboardViewState { SelectedStepIndex = 99 }, 120, 0, ColorMode.None);
                AssertLine(Box(LiveDashboardLayout.Pointer + LogoutPainRow + Spaces(43)), 120, past[37]);

                var negative = LiveDashboardLayout.Render(new[] { PainSnapshot() }, new LiveDashboardViewState { SelectedStepIndex = -3 }, 120, 0, ColorMode.None);
                AssertLine(Box(LiveDashboardLayout.Pointer + CheckoutPainRow + Spaces(43)), 120, negative[32]);

                foreach (var lines in new[] { past, negative })
                {
                    var pointerCount = 0;
                    foreach (var line in lines)
                    {
                        if (line.Text.Contains(LiveDashboardLayout.Pointer))
                            pointerCount++;
                    }

                    Assert.AreEqual(1, pointerCount);
                }
            })
            .Step("No selection highlights nothing, and a selection on a snapshot without steps is nothing to highlight", context =>
            {
                var none = LiveDashboardLayout.Render(new[] { PainSnapshot() }, new LiveDashboardViewState { SelectedStepIndex = null }, 120, 0, ColorMode.TrueColor);
                var noSteps = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, new LiveDashboardViewState { SelectedStepIndex = 0 }, 120, 0, ColorMode.TrueColor);

                foreach (var line in none.Concat(noSteps))
                {
                    Assert.DoesNotContain(LiveDashboardLayout.Pointer, line.Text);
                    Assert.DoesNotContain(AnsiCodes.Reverse, line.Text);
                }
            })
            .Step("The selection indexes the rows displayed: once the budget hides the least painful rows, an index past them lands on the last row kept", context =>
            {
                // Below 30 rows the tiles carry no trend (four rows, no gauge without a plan)
                // and the chart bodies are four rows: the title, the tiles, a blank, the
                // six-row chart panels and the five-row requests panel are 17 rows before the
                // table, and its 9 are 26 against the 24 above the footer. No heatmap and the
                // charts compact by height already, so the step rows pay: the first row given
                // back costs the more line, so three rows go for two — Checkout, Add to cart
                // and Login stay over "+3 more" — and index 5 lands on Login. The columns
                // follow the rows shown: without "120 ms" the p95 column is five wide, so
                // the rows are 72 columns.
                var lines = LiveDashboardLayout.Render(new[] { PainSnapshot() }, new LiveDashboardViewState { SelectedStepIndex = 5 }, 120, 25, ColorMode.None);

                Assert.HasCount(25, lines);
                AssertLine(PanelTop("Steps", 120), 120, lines[17]);
                AssertLine(Box(" Checkout      1000   10  10 ms  90 ms      20  █░░░░ 2.0%" + Spaces(13) + "⣤" + Spaces(44)), 120, lines[19]);
                AssertLine(Box(" Add to cart   1000   10  10 ms  30 ms      20  █░░░░ 2.0%" + Spaces(13) + "⣤" + Spaces(44)), 120, lines[20]);
                AssertLine(Box(LiveDashboardLayout.Pointer + "Login         1000   10  10 ms  50 ms      10  █░░░░ 1.0%" + Spaces(13) + "⣤" + Spaces(44)), 120, lines[21]);
                AssertLine(Box(" +3 more" + Spaces(108)), 120, lines[22]);
                AssertLine(Bottom(120), 120, lines[23]);
                AssertLine(OverviewFooter, 93, lines[24]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_table_trims_to_one_row_and_then_goes_whole()
    {
        // Below 30 rows the tiles carry no trend and the chart bodies are four rows: the
        // title, four tile rows, a blank, the six-row chart panels and the five-row requests
        // panel put the step table's top border on row 17, and the table's nine rows are 26
        // against the rows above the footer. No heatmap, and the charts are compact by
        // height, so the step rows are the first budget step with anything to give.
        await Scenario()
            .Step("Four rows over, the table keeps one row — its columns as wide as that row — and announces the five it hides; five over, one row and the more line are still one too many, and the charts go for it — the trimmed table stays as it is over the rows they freed", context =>
            {
                // 26 against 22: the rows pay four, the first one the more line and the next
                // four a row each. Against 21 the table is trimmed to its one row and the
                // more line for the same four, and the row still owed takes the latency
                // chart — side by side that frees nothing — and the requests chart after it:
                // the requests panel moves up to 6-10 and the table to 11-15, still one row
                // and "+5 more", since the order never gives rows back, over five blank rows.
                var minimum = LiveDashboardLayout.Render(new[] { PainSnapshot() }, DefaultView, 120, 23, ColorMode.None);
                AssertLine(PanelTop("Steps", 120), 120, minimum[17]);
                AssertLine(Box(" Checkout   1000   10  10 ms  90 ms      20  █░░░░ 2.0%" + Spaces(13) + "⣤" + Spaces(47)), 120, minimum[19]);
                AssertLine(Box(" +5 more" + Spaces(108)), 120, minimum[20]);
                AssertLine(Bottom(120), 120, minimum[21]);
                AssertLine(OverviewFooter, 93, minimum[22]);

                var chartsGone = LiveDashboardLayout.Render(new[] { PainSnapshot() }, DefaultView, 120, 22, ColorMode.None);
                Assert.HasCount(22, chartsGone);
                AssertLine(PanelTop("Requests", 120), 120, chartsGone[6]);
                AssertLine(Bottom(120), 120, chartsGone[10]);
                AssertLine(PanelTop("Steps", 120), 120, chartsGone[11]);
                AssertLine(Box(" Checkout   1000   10  10 ms  90 ms      20  █░░░░ 2.0%" + Spaces(13) + "⣤" + Spaces(47)), 120, chartsGone[13]);
                AssertLine(Box(" +5 more" + Spaces(108)), 120, chartsGone[14]);
                AssertLine(Bottom(120), 120, chartsGone[15]);
                AssertLine(string.Empty, 0, chartsGone[16]);
                AssertLine(string.Empty, 0, chartsGone[20]);
                AssertLine(OverviewFooter, 93, chartsGone[21]);
                foreach (var line in chartsGone)
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
            })
            .Step("With the charts gone the one-row table and its more line fit 17 rows exactly, and at 16 the panel goes whole rather than showing a header", context =>
            {
                // The title, four tile rows, a blank, the five-row requests panel and the
                // five-row trimmed table are 16: against 16 rows above the footer the table
                // sits at 11-15; against 15 the row owed takes the panel whole, and rows
                // 11-14 stay blank.
                var fitting = LiveDashboardLayout.Render(new[] { PainSnapshot() }, DefaultView, 120, 17, ColorMode.None);
                Assert.HasCount(17, fitting);
                AssertLine(PanelTop("Steps", 120), 120, fitting[11]);
                AssertLine(Box(" +5 more" + Spaces(108)), 120, fitting[14]);
                AssertLine(Bottom(120), 120, fitting[15]);
                AssertLine(OverviewFooter, 93, fitting[16]);

                var dropped = LiveDashboardLayout.Render(new[] { PainSnapshot() }, DefaultView, 120, 16, ColorMode.None);
                Assert.HasCount(16, dropped);
                AssertLine(Bottom(120), 120, dropped[10]);
                AssertLine(string.Empty, 0, dropped[11]);
                AssertLine(string.Empty, 0, dropped[14]);
                AssertLine(OverviewFooter, 93, dropped[15]);
                foreach (var line in dropped)
                    Assert.DoesNotContain("Steps", line.Text);
            })
            .Step("The more line is dim", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { PainSnapshot() }, DefaultView, 120, 23, ColorMode.TrueColor);

                AssertLine("│  " + Sgr(Dim, "+5 more") + Spaces(108) + " │", 120, lines[20]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_table_tiers_drop_the_trend_first_and_then_mean_and_failed()
    {
        await Scenario()
            .Step("The trend column shows while the widest tier's 73 columns fit the panel — at 77 columns exactly — and goes at 76", context =>
            {
                var fitting = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 77, 0, ColorMode.None);
                var fittingSteps = IndexOfPanel(fitting, "Steps");
                AssertLine(Box(StepsHeaderRow), 77, fitting[fittingSteps + 1]);
                AssertLine(Box(CheckoutStepRow), 77, fitting[fittingSteps + 2]);
                AssertLine(Box(AddToCartStepRow), 77, fitting[fittingSteps + 3]);

                var narrower = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 76, 0, ColorMode.None);
                var narrowerSteps = IndexOfPanel(narrower, "Steps");
                AssertLine(Box(" step         count  rps   mean    p95  failed  fail%      " + Spaces(13)), 76, narrower[narrowerSteps + 1]);
                AssertLine(Box(" Checkout      6260  9.6  58 ms  96 ms      30  █░░░░ 0.5% " + Spaces(13)), 76, narrower[narrowerSteps + 2]);
            })
            .Step("Mean and failed go next: at 63 columns the seven-column tier's 59 fit exactly, at 62 the name, count, rps, p95 and fail% remain", context =>
            {
                var fitting = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 63, 0, ColorMode.None);
                var fittingSteps = IndexOfPanel(fitting, "Steps");
                AssertLine(Box(" Checkout      6260  9.6  58 ms  96 ms      30  █░░░░ 0.5% "), 63, fitting[fittingSteps + 2]);

                var narrower = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 62, 0, ColorMode.None);
                var narrowerSteps = IndexOfPanel(narrower, "Steps");
                AssertLine(Box(" step         count  rps    p95  fail%      " + Spaces(14)), 62, narrower[narrowerSteps + 1]);
                AssertLine(Box(" Checkout      6260  9.6  96 ms  █░░░░ 0.5% " + Spaces(14)), 62, narrower[narrowerSteps + 2]);
                AssertLine(Box(" Add to cart   6252   71  38 ms  █░░░░ <0.1%" + Spaces(14)), 62, narrower[narrowerSteps + 3]);
            })
            .Step("The block glyph set draws the trend one sample per column", context =>
            {
                // Ten samples in the right ten of twelve columns at eight levels: Add to cart's
                // 44 → 71.4 reads 1 2 3 4 5 6 7 7 8 8 (a level is 1 + round(position × 7)).
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 42, ColorMode.None, SparklineGlyphSet.Blocks);

                Assert.EndsWith("█░░░░ <0.1%" + Spaces(4) + "▁▂▃▄▅▆▇▇██" + Spaces(43) + " │", lines[35].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_ticker_format_degrades_by_width()
    {
        await Scenario()
            .Step("At 79 columns the ages go and the rates stay; at 59 the rates go too and the count is followed by one space", context =>
            {
                var withRates = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 79, 0, ColorMode.None);
                var withRatesErrors = IndexOfPanel(withRates, "Errors");
                AssertLine(Box("30× (2.0/s)  Checkout · Connection refused (localhost:7058)" + Spaces(16)), 79, withRates[withRatesErrors + 1]);
                AssertLine(Box(" 2× (0.0/s)  Add to cart · Timeout after 30s" + Spaces(31)), 79, withRates[withRatesErrors + 2]);

                var bare = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 59, 0, ColorMode.None);
                var bareErrors = IndexOfPanel(bare, "Errors");
                AssertLine(Box("30× Checkout · Connection refused (localhost:7058)" + Spaces(5)), 59, bare[bareErrors + 1]);
                AssertLine(Box(" 2× Add to cart · Timeout after 30s" + Spaces(20)), 59, bare[bareErrors + 2]);
            })
            .Step("At 80 columns the ages are back and the 92-column entry wraps at the last space that fits 76: the ages split over the break, the rest indented under the step name", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 80, 0, ColorMode.None);
                var errors = IndexOfPanel(lines, "Errors");

                AssertLine(Box("30× (2.0/s)  Checkout · Connection refused (localhost:7058)   first 1m 12s" + Spaces(2)), 80, lines[errors + 1]);
                AssertLine(Box(Spaces(13) + "ago · last 2s ago" + Spaces(46)), 80, lines[errors + 2]);
                AssertLine(Box(" 2× (0.0/s)  Add to cart · Timeout after 30s   first 45s ago · last 20s ago" + Spaces(1)), 80, lines[errors + 3]);
                AssertLine(Bottom(80), 80, lines[errors + 4]);
            })
            .Step("Without a sample there is no instant to measure the ages from, so they are left out at any width", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(2) }, DefaultView, 120, 0, ColorMode.None);
                var errors = IndexOfPanel(lines, "Errors");

                AssertLine(Box("1× (0.0/s)  S · E1" + Spaces(98)), 120, lines[errors + 1]);
                AssertLine(Box("2× (0.0/s)  S · E2" + Spaces(98)), 120, lines[errors + 2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_messages_wrap_to_the_panel()
    {
        await Scenario()
            .Step("A 102-character message breaks at the last space that fits the 116-column panel, and the last word carries the ages on a line indented under the step name", context =>
            {
                // 23 columns of prefix and step, the message and 30 of ages are 155: the space
                // before "finalized" is column 115, so the first line is 115 columns and the
                // second the last word and the ages, 39 columns, after a 12-column indent.
                var lines = LiveDashboardLayout.Render(new[] { WrapSnapshot(LongMessage) }, DefaultView, 120, 0, ColorMode.None);
                var errors = IndexOfPanel(lines, "Errors");

                AssertLine(Box("7× (1.5/s)  Checkout · The request to the payment provider timed out after thirty seconds while the order was being" + Spaces(1)), 120, lines[errors + 1]);
                AssertLine(Box(Spaces(12) + "finalized   first 30s ago · last 3s ago" + Spaces(65)), 120, lines[errors + 2]);
                AssertLine(Bottom(120), 120, lines[errors + 3]);
            })
            .Step("TrueColor: every line is styled on its own — the count red, the rate, the dot and the ages dim, the name bold — with no style open across the break", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { WrapSnapshot(LongMessage) }, DefaultView, 120, 0, ColorMode.TrueColor);
                var errors = IndexOfPanel(lines, "Errors");

                AssertLine("│ " + Sgr(Red, "7×") + " " + Sgr(Dim, "(1.5/s)") + "  " + Sgr(Bold, "Checkout") + " " + Sgr(Dim, "·") + " The request to the payment provider timed out after thirty seconds while the order was being" + Spaces(1) + " │", 120, lines[errors + 1]);
                AssertLine("│ " + Spaces(12) + "finalized   " + Sgr(Dim, "first 30s ago · last 3s ago") + Spaces(65) + " │", 120, lines[errors + 2]);
            })
            .Step("An entry keeps at most three lines: a 300-character word breaks at the width and the third line ends in an ellipsis", context =>
            {
                // At 60 columns the ticker is 56 wide with rates and without ages. The last
                // space that fits is the one after the dot, so the first line is the prefix
                // alone; the continuation lines are 44 columns after the 12-column indent, the
                // second a full 44 of the word and the third the rest cut to 43 and the
                // ellipsis.
                var lines = LiveDashboardLayout.Render(new[] { WrapSnapshot(new string('x', 300)) }, DefaultView, 60, 0, ColorMode.None);
                var errors = IndexOfPanel(lines, "Errors");

                AssertLine(Box("7× (1.5/s)  Checkout ·" + Spaces(34)), 60, lines[errors + 1]);
                AssertLine(Box(Spaces(12) + new string('x', 44)), 60, lines[errors + 2]);
                AssertLine(Box(Spaces(12) + new string('x', 43) + "…"), 60, lines[errors + 3]);
                AssertLine(Bottom(60), 60, lines[errors + 4]);
            })
            .Step("Markup and control characters in a step name and a message render literally as spaces and brackets, never as styling", context =>
            {
                var snapshot = new LiveMetricsSnapshot
                {
                    ScenarioName = "Escaped",
                    Errors = new[] { new LiveErrorEntry { StepName = "Step [1]", Message = "[bold]Boom[/] \u001b[31mred\u001b[0m\r\ntail", Count = 1 } }
                };

                var plain = LiveDashboardLayout.Render(new[] { snapshot }, DefaultView, 120, 0, ColorMode.None);
                var errors = IndexOfPanel(plain, "Errors");
                AssertLine(Box("1× (0.0/s)  Step [1] · [bold]Boom[/]  [31mred [0m tail" + Spaces(62)), 120, plain[errors + 1]);
                Assert.DoesNotContain("\u001b", plain[errors + 1].Text);

                var styled = LiveDashboardLayout.Render(new[] { snapshot }, DefaultView, 120, 0, ColorMode.TrueColor);
                Assert.Contains(Sgr(Bold, "Step [1]") + " " + Sgr(Dim, "·") + " [bold]Boom[/]  [31mred [0m tail", styled[errors + 1].Text);
                Assert.DoesNotContain("\u001b[31m", styled[errors + 1].Text);
                Assert.AreEqual(120, styled[errors + 1].Width);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_ticker_announces_hidden_errors()
    {
        // The many-errors run at 120 columns: the title, five tile rows and a blank, the
        // charts, the heatmap and the requests panel put the ticker's top border on row 30.
        await Scenario()
            .Step("Every error fits: five entries, most recently active first, and no more line", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(5) }, DefaultView, 120, 38, ColorMode.None);

                AssertLine(PanelTop("Errors", 120), 120, lines[30]);
                AssertLine(Box("1× (0.0/s)  S · E1" + Spaces(98)), 120, lines[31]);
                AssertLine(Box("5× (0.0/s)  S · E5" + Spaces(98)), 120, lines[35]);
                AssertLine(Bottom(120), 120, lines[36]);
                AssertLine(OverviewFooter, 93, lines[37]);
                foreach (var line in lines)
                    Assert.DoesNotContain("more", line.Text);
            })
            .Step("Two rows short, the entries go least recently active first — three of them, since the first costs the more line — and the line counts the hidden ones", context =>
            {
                // Below 30 rows: the title, four tile rows, a blank, the six-row chart panels
                // and the five-row requests panel are 17 rows, the seven-row ticker 24
                // against the 22 above the footer at 23 rows — no heatmap and the charts
                // compact by height, so the entries pay: E5 for the more line, E4 and E3 for
                // the two rows, the ticker at 17-21.
                var lines = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(5) }, DefaultView, 120, 23, ColorMode.None);

                Assert.HasCount(23, lines);
                AssertLine(PanelTop("Errors", 120), 120, lines[17]);
                AssertLine(Box("1× (0.0/s)  S · E1" + Spaces(98)), 120, lines[18]);
                AssertLine(Box("2× (0.0/s)  S · E2" + Spaces(98)), 120, lines[19]);
                AssertLine(Box("+3 more" + Spaces(109)), 120, lines[20]);
                AssertLine(Bottom(120), 120, lines[21]);
                AssertLine(OverviewFooter, 93, lines[22]);
            })
            .Step("Distinct errors beyond what the snapshot carries count too, with every entry shown and once the budget hides some", context =>
            {
                // With four distinct errors beyond the five, the more line is there from the
                // start: the eight-row ticker is three over the 22 at 23 rows, and every entry
                // given back saves a row — E5, E4 and E3 — for "+7 more" under E1 and E2.
                var beyond = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(3, distinctErrorCount: 7) }, DefaultView, 120, 0, ColorMode.None);
                AssertLine(Box("3× (0.0/s)  S · E3" + Spaces(98)), 120, beyond[33]);
                AssertLine(Box("+4 more" + Spaces(109)), 120, beyond[34]);
                AssertLine(Bottom(120), 120, beyond[35]);

                var both = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(5, distinctErrorCount: 9) }, DefaultView, 120, 23, ColorMode.None);
                AssertLine(Box("2× (0.0/s)  S · E2" + Spaces(98)), 120, both[19]);
                AssertLine(Box("+7 more" + Spaces(109)), 120, both[20]);
                AssertLine(Bottom(120), 120, both[21]);

                var styled = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(3, distinctErrorCount: 7) }, DefaultView, 120, 0, ColorMode.TrueColor);
                AssertLine("│ " + Sgr(Dim, "+4 more") + Spaces(109) + " │", 120, styled[34]);
            })
            .Step("The ticker keeps one entry and the more line at the least, and goes whole one row short of that once the charts are gone", context =>
            {
                // Twenty errors below 30 rows: the title, four tile rows, a blank, the
                // six-row chart panels and the requests panel are 17 rows, the ticker's 22
                // are 39; 22 rows of window leave 21, so the ticker trims to E1 and "+19 more"
                // on rows 17-20. The charts go before the ticker does: 16 rows leave 15, the
                // trimmed ticker's 18 and the charts' 6 pay the 24, and it sits at 11-14 under
                // the requests panel; 15 rows leave it one short of even that.
                var minimum = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(20) }, DefaultView, 120, 22, ColorMode.None);
                AssertLine(PanelTop("Errors", 120), 120, minimum[17]);
                AssertLine(Box(" 1× (0.0/s)  S · E1" + Spaces(97)), 120, minimum[18]);
                AssertLine(Box("+19 more" + Spaces(108)), 120, minimum[19]);
                AssertLine(Bottom(120), 120, minimum[20]);
                AssertLine(OverviewFooter, 93, minimum[21]);

                var chartsGone = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(20) }, DefaultView, 120, 16, ColorMode.None);
                Assert.HasCount(16, chartsGone);
                AssertLine(PanelTop("Requests", 120), 120, chartsGone[6]);
                AssertLine(PanelTop("Errors", 120), 120, chartsGone[11]);
                AssertLine(Box(" 1× (0.0/s)  S · E1" + Spaces(97)), 120, chartsGone[12]);
                AssertLine(Box("+19 more" + Spaces(108)), 120, chartsGone[13]);
                AssertLine(Bottom(120), 120, chartsGone[14]);
                AssertLine(OverviewFooter, 93, chartsGone[15]);
                foreach (var line in chartsGone)
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);

                var dropped = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(20) }, DefaultView, 120, 15, ColorMode.None);
                Assert.HasCount(15, dropped);
                AssertLine(Bottom(120), 120, dropped[10]);
                AssertLine(string.Empty, 0, dropped[11]);
                AssertLine(string.Empty, 0, dropped[13]);
                AssertLine(OverviewFooter, 93, dropped[14]);
                foreach (var line in dropped)
                    Assert.DoesNotContain("Errors", line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_ticker_shows_no_data_for_a_rate_that_is_not_finite()
    {
        await Scenario()
            .Step("A rate that is not finite renders as the em dash in the rate's parentheses, right-aligned with the finite rates", context =>
            {
                // No sample, so no ages. Alone, "(—/s)" is five columns: "1× (—/s)  S · E"
                // is 15 and the 116-column panel pads 101. Beside a finite rate the rate
                // column is as wide as "(2.0/s)", seven, so the dash's is padded left by two;
                // the entries sort most recently active first, E1 before E2.
                var alone = new LiveMetricsSnapshot { ScenarioName = "Rates", Errors = new[] { new LiveErrorEntry { StepName = "S", Message = "E", Count = 1, RatePerSecond = double.NaN } } };
                var aloneLines = LiveDashboardLayout.Render(new[] { alone }, DefaultView, 120, 0, ColorMode.None);
                AssertLine(Box("1× (—/s)  S · E" + Spaces(101)), 120, aloneLines[IndexOfPanel(aloneLines, "Errors") + 1]);

                var mixed = new LiveMetricsSnapshot
                {
                    ScenarioName = "Rates",
                    Errors = new[]
                    {
                        new LiveErrorEntry { StepName = "S", Message = "E1", Count = 1, RatePerSecond = double.PositiveInfinity, LastSeen = SampleTime(2) },
                        new LiveErrorEntry { StepName = "S", Message = "E2", Count = 2, RatePerSecond = 2.0, LastSeen = SampleTime(1) }
                    }
                };
                var mixedLines = LiveDashboardLayout.Render(new[] { mixed }, DefaultView, 120, 0, ColorMode.None);
                var errors = IndexOfPanel(mixedLines, "Errors");
                AssertLine(Box("1×   (—/s)  S · E1" + Spaces(98)), 120, mixedLines[errors + 1]);
                AssertLine(Box("2× (2.0/s)  S · E2" + Spaces(98)), 120, mixedLines[errors + 2]);

                var styled = LiveDashboardLayout.Render(new[] { alone }, DefaultView, 120, 0, ColorMode.TrueColor);
                Assert.Contains(Sgr(Red, "1×") + " " + Sgr(Dim, "(—/s)") + "  " + Sgr(Bold, "S"), styled[IndexOfPanel(styled, "Errors") + 1].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_hostile_steps_and_errors_never_break_the_frame()
    {
        await Scenario()
            .Step("At every width from 1 to 220 and height from 1 to 60, with a row selected, the frame is exactly the height, the footer owns the last row (as many of the overview's hints as fit before the quit hint, which is cut to the width below its six columns), no line exceeds the width and no surrogate pair is split", context =>
            {
                var snapshots = new[] { HostileSnapshot() };
                var viewState = new LiveDashboardViewState { SelectedStepIndex = 1 };
                for (var width = 1; width <= 220; width++)
                {
                    for (var height = 1; height <= 60; height++)
                    {
                        var lines = LiveDashboardLayout.Render(snapshots, viewState, width, height, ColorMode.TrueColor);

                        Assert.HasCount(height, lines, $"Row count at {width}×{height}");
                        Assert.AreEqual(ExpectedOverviewFooterWidth(width), lines[height - 1].Width, $"Footer at {width}×{height}");
                        if (width >= 6)
                            Assert.EndsWith(Sgr(Bold, "q") + " quit", lines[height - 1].Text, $"Footer at {width}×{height}");
                        AssertMaximumWidth(width, lines);
                        AssertNoLoneSurrogate(lines, $"{width}×{height}");
                    }
                }
            })
            .Step("Under an unbounded height every one of the forty-two entries is on screen, capped at three lines, over a more line for the five beyond the list, and no control character or foreign escape leaks", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { HostileSnapshot() }, DefaultView, 120, 0, ColorMode.None);
                var errors = IndexOfPanel(lines, "Errors");

                var entryLines = 0;
                for (var index = errors + 1; index < lines.Count; index++)
                {
                    if (lines[index].Text.StartsWith("╰", StringComparison.Ordinal))
                        break;

                    entryLines++;
                }

                Assert.IsGreaterThanOrEqualTo(43, entryLines);
                Assert.IsLessThanOrEqualTo(42 * LiveDashboardLayout.MaximumErrorEntryLines + 1, entryLines);
                Assert.Contains("+5 more", lines[errors + entryLines].Text);
                for (var index = errors + 1; index <= errors + entryLines; index++)
                {
                    Assert.DoesNotContain("\u001b", lines[index].Text);
                    Assert.DoesNotContain("\t", lines[index].Text);
                    Assert.DoesNotContain("\0", lines[index].Text);
                    Assert.DoesNotContain("\r", lines[index].Text);
                    Assert.DoesNotContain("\n", lines[index].Text);
                    Assert.AreEqual(120, lines[index].Text.Length, "A plain ticker line is exactly the width: " + lines[index].Text);
                }

                AssertNoLoneSurrogate(lines, "120×0");
            })
            .Step("A run of surrogate pairs without a space breaks at the width, never between a pair's halves: the continuation backs off one unit and the last line takes the rest under an ellipsis", context =>
            {
                // At 60 columns the ticker is 56 wide with rates and without ages; a count of
                // 10 makes the prefix "10× (1.5/s)  Checkout · " 24 units and the indent 13,
                // so the continuation width is 43 — odd. The message is 100 rockets, 200
                // units: the first line is the prefix alone (the last space that fits 56 is
                // the one after the dot), the second would end between the 22nd rocket's
                // halves at unit 24 + 43, so it backs off to 21 whole rockets — 42 units, one
                // short of the width, padded by the panel — and the third takes the rest cut
                // to 42 units and the ellipsis.
                var lines = LiveDashboardLayout.Render(new[] { WrapSnapshot(Rockets(100), count: 10) }, DefaultView, 60, 0, ColorMode.None);
                var errors = IndexOfPanel(lines, "Errors");

                AssertLine(Box("10× (1.5/s)  Checkout ·" + Spaces(33)), 60, lines[errors + 1]);
                AssertLine(Box(Spaces(13) + Rockets(21) + " "), 60, lines[errors + 2]);
                AssertLine(Box(Spaces(13) + Rockets(21) + "…"), 60, lines[errors + 3]);
                AssertLine(Bottom(60), 60, lines[errors + 4]);
                AssertNoLoneSurrogate(lines, "60×0");
            })
            .Run();
    }

    /// <summary>Fails when any line holds a high surrogate without its low half after it, or a low one without its high half before it.</summary>
    private static void AssertNoLoneSurrogate(IReadOnlyList<RenderedLine> lines, string size)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var text = lines[index].Text;
            for (var position = 0; position < text.Length; position++)
            {
                if (char.IsHighSurrogate(text[position]))
                {
                    Assert.IsTrue(position + 1 < text.Length && char.IsLowSurrogate(text[position + 1]), $"Lone high surrogate on row {index} at {size}");
                    position++;
                }
                else
                {
                    Assert.IsFalse(char.IsLowSurrogate(text[position]), $"Lone low surrogate on row {index} at {size}");
                }
            }
        }
    }

    /// <summary>The row of the top border of the panel with the given header, in any colour mode.</summary>
    private static int IndexOfPanel(IReadOnlyList<RenderedLine> lines, string header)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Text.StartsWith("╭─ ", StringComparison.Ordinal) && lines[index].Text.Contains(header))
                return index;
        }

        Assert.Fail($"No {header} panel in the frame.");
        return -1;
    }
}
