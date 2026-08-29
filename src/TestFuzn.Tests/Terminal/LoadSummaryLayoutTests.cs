using System.Text.RegularExpressions;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins <see cref="LoadSummaryLayout"/> against a real in-memory <see cref="ScenarioLoadCollector"/>
/// driven through init, three measurement iterations — one of them failing its first step,
/// which skips the second — and cleanup, with every number in the goldens re-derived by hand
/// from what the collector records (HdrHistogram's equivalent-value rounding included). Pinned
/// at 120 columns: the summary panel, the simulations panel, the global metrics panel with the
/// scenario's requests and response-time tables side by side, one panel per step with the
/// Skipped row, and the errors panel; in color mode None as the exact plain lines with zero
/// escape bytes, and in TrueColor as the same lines once the styling is stripped, with the
/// palette spot-checked. Pinned at 60 columns: the tables stacked with the response-time
/// spread split into two tables of four and the simulation description word-wrapped. And
/// from 40 to 200 columns, for two-digit and five-digit response times alike: no cell is ever
/// truncated — the summary lists label/value lines and splits the spread into pairs before it
/// would cut a number, and below its minimum width it lays out at that minimum. Also a failed
/// status, a scenario that recorded nothing, and the word wrap the panels use.
/// </summary>
[TestClass]
public class LoadSummaryLayoutTests : Test
{
    private const int Width = 120;
    private const int NarrowWidth = 60;

    private const string ScenarioName = "Checkout flow";
    private const string BrowseStep = "Browse products";
    private const string OrderStep = "Place order";

    private static readonly Regex SgrSequence = new Regex("\u001b\\[[0-9;]*m");

    private static DateTime At(double seconds)
    {
        return SyntheticLoadSnapshots.At(seconds);
    }

    /// <summary>
    /// The goldens' scenario and its collected result: two steps, one fixed-load simulation,
    /// init 0–1 s, cleanup 11–12 s, and three measurement iterations — 10+30 ms passed,
    /// 20+30 ms passed, then the first step failed in 10 ms with "Connection refused" and the
    /// second skipped. The measurement start is placed in the future on purpose: the collector
    /// derives its rates from the wall clock, which a golden cannot depend on, and a start still
    /// ahead of every recording makes every rate read as its count.
    /// </summary>
    private static KeyValuePair<Scenario, ScenarioLoadResult> CollectCheckoutFlow(TestStatus status = TestStatus.Passed)
    {
        var collector = StartCollecting(out var scenario);

        Record(collector, TestStatus.Passed, StepResult(BrowseStep, StepStatus.Passed, 10), StepResult(OrderStep, StepStatus.Passed, 30));
        Record(collector, TestStatus.Passed, StepResult(BrowseStep, StepStatus.Passed, 20), StepResult(OrderStep, StepStatus.Passed, 30));
        Record(collector, TestStatus.Failed, StepResult(BrowseStep, StepStatus.Failed, 10, new InvalidOperationException("Connection refused")), StepResult(OrderStep, StepStatus.Skipped, 0));

        return FinishCollecting(scenario, collector, status);
    }

    /// <summary>
    /// The same scenario with slow steps — a 1234 ms and a 30 s step, then a 2116 ms failure —
    /// so the response times run to five digits and the spread's columns widen.
    /// </summary>
    private static KeyValuePair<Scenario, ScenarioLoadResult> CollectSlowCheckoutFlow()
    {
        var collector = StartCollecting(out var scenario);

        Record(collector, TestStatus.Passed, StepResult(BrowseStep, StepStatus.Passed, 1234), StepResult(OrderStep, StepStatus.Passed, 30000));
        Record(collector, TestStatus.Failed, StepResult(BrowseStep, StepStatus.Failed, 2116, new InvalidOperationException("HTTP 503 Service Unavailable")), StepResult(OrderStep, StepStatus.Skipped, 0));

        return FinishCollecting(scenario, collector, TestStatus.Passed);
    }

    private static ScenarioLoadCollector StartCollecting(out Scenario scenario)
    {
        scenario = new Scenario(ScenarioName);
        scenario.Id = "checkout-flow";
        scenario.Steps.Add(new Step { Name = BrowseStep, Id = "browse-products" });
        scenario.Steps.Add(new Step { Name = OrderStep, Id = "place-order" });
        scenario.SimulationsAction = (scenarioContext, simulations) => Task.CompletedTask;
        scenario.SimulationsInternal.Add(new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)));

        var collector = new ScenarioLoadCollector(scenario);
        collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
        collector.MarkPhaseAsCompleted(LoadTestPhase.Init, At(1));
        collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, DateTime.UtcNow.AddHours(1));
        return collector;
    }

    private static KeyValuePair<Scenario, ScenarioLoadResult> FinishCollecting(Scenario scenario, ScenarioLoadCollector collector, TestStatus status)
    {
        collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, At(11));
        collector.MarkPhaseAsStarted(LoadTestPhase.Cleanup, At(11));
        collector.MarkPhaseAsCompleted(LoadTestPhase.Cleanup, At(12));
        if (status != TestStatus.Passed)
            collector.SetStatus(status);

        return new KeyValuePair<Scenario, ScenarioLoadResult>(scenario, collector.GetCurrentResult(true));
    }

    private static void Record(ScenarioLoadCollector collector, TestStatus status, params StepStandardResult[] stepResults)
    {
        var iterationResult = new IterationResult();
        var duration = TimeSpan.Zero;
        foreach (var stepResult in stepResults)
        {
            iterationResult.StepResults.Add(stepResult.Name, stepResult);
            duration += stepResult.Duration;
        }

        iterationResult.ExecuteStartTime = SyntheticLoadSnapshots.BaseTime;
        iterationResult.ExecuteEndTime = SyntheticLoadSnapshots.BaseTime + duration;
        collector.RecordMeasurement(status, iterationResult);
    }

    private static StepStandardResult StepResult(string name, StepStatus status, int milliseconds, Exception? exception = null)
    {
        var stepResult = new StepStandardResult();
        stepResult.Name = name;
        stepResult.Id = name.ToLowerInvariant().Replace(' ', '-');
        stepResult.Status = status;
        stepResult.Duration = TimeSpan.FromMilliseconds(milliseconds);
        stepResult.Exception = exception!;
        return stepResult;
    }

    /// <summary>
    /// The plain golden of the collected scenario at 120 columns, derived by hand. Rates read as
    /// counts (see <see cref="CollectCheckoutFlow"/>); the response times follow from the
    /// recorded durations: the scenario's ok iterations took 40 and 50 ms (mean 45, standard
    /// deviation 5, median the first of two, p75 and up the second), its failed one 10 ms (a
    /// single value has no deviation: N/A); the first step's ok durations were 10 and 20 ms, its
    /// failed one 10 ms; the second step's two ok durations 30 ms each and it recorded no
    /// failure (N/A across) but one skip.
    /// </summary>
    private static string[] PlainGolden(string status)
    {
        var responseTimeHeader = "Metric    Min   Mean    Max  StdDev  Median    P75    P95    P99";
        return new[]
        {
            Top(LoadSummaryLayout.SummaryHeader),
            Row("Scenario       Execution Time  Test Run Time  Status"),
            Row("Checkout flow  00:00:00:10     00:00:12:00    " + status),
            Bottom(),
            Top(LoadSummaryLayout.SimulationsHeader),
            Row("Type"),
            Row("Fixed Load - Rate: 5, Interval: 0:00:01, Duration: 0:00:10"),
            Bottom(),
            Top(LoadSummaryLayout.GlobalMetricsHeader),
            Row("Scenario Requests     Response Times"),
            Row("Metric  Count  RPS    " + responseTimeHeader),
            Row("Total       3         Ok      40 ms  45 ms  50 ms    5 ms   40 ms  50 ms  50 ms  50 ms"),
            Row("OK          2    2    Failed  10 ms  10 ms  10 ms     N/A   10 ms  10 ms  10 ms  10 ms"),
            Row("Failed      1    1"),
            Bottom(),
            Top("Step " + BrowseStep + " Details"),
            Row("Step Requests          Response Times"),
            Row("Metric   Count  RPS    " + responseTimeHeader),
            Row("Total        3         Ok      10 ms  15 ms  20 ms    5 ms   10 ms  20 ms  20 ms  20 ms"),
            Row("OK           2    2    Failed  10 ms  10 ms  10 ms     N/A   10 ms  10 ms  10 ms  10 ms"),
            Row("Failed       1    1"),
            Row("Skipped      0"),
            Bottom(),
            Top("Step " + OrderStep + " Details"),
            Row("Step Requests          Response Times"),
            Row("Metric   Count  RPS    " + responseTimeHeader),
            Row("Total        2         Ok      30 ms  30 ms  30 ms     N/A   30 ms  30 ms  30 ms  30 ms"),
            Row("OK           2    2    Failed    N/A    N/A    N/A     N/A     N/A    N/A    N/A    N/A"),
            Row("Failed       0    0"),
            Row("Skipped      1"),
            Bottom(),
            Top(LoadSummaryLayout.ErrorsHeader),
            Row(BrowseStep + ":"),
            Row("  Connection refused (Count: 1)"),
            Bottom()
        };
    }

    /// <summary>
    /// The plain golden of the same scenario at 60 columns: the tables stack, the response-time
    /// spread splits into two tables of four columns under one title, and the 58-character
    /// simulation description wraps at the last space before 54 columns (the inner width less
    /// the continuation indent) with its continuation indented.
    /// </summary>
    private static string[] NarrowPlainGolden()
    {
        var firstHalfHeader = "Metric    Min   Mean    Max  StdDev";
        var secondHalfHeader = "Metric  Median    P75    P95    P99";
        return new[]
        {
            Top(LoadSummaryLayout.SummaryHeader, NarrowWidth),
            Row("Scenario       Execution Time  Test Run Time  Status", NarrowWidth),
            Row("Checkout flow  00:00:00:10     00:00:12:00    Passed", NarrowWidth),
            Bottom(NarrowWidth),
            Top(LoadSummaryLayout.SimulationsHeader, NarrowWidth),
            Row("Type", NarrowWidth),
            Row("Fixed Load - Rate: 5, Interval: 0:00:01, Duration:", NarrowWidth),
            Row("  0:00:10", NarrowWidth),
            Bottom(NarrowWidth),
            Top(LoadSummaryLayout.GlobalMetricsHeader, NarrowWidth),
            Row("Scenario Requests", NarrowWidth),
            Row("Metric  Count  RPS", NarrowWidth),
            Row("Total       3", NarrowWidth),
            Row("OK          2    2", NarrowWidth),
            Row("Failed      1    1", NarrowWidth),
            Row(string.Empty, NarrowWidth),
            Row("Response Times", NarrowWidth),
            Row(firstHalfHeader, NarrowWidth),
            Row("Ok      40 ms  45 ms  50 ms    5 ms", NarrowWidth),
            Row("Failed  10 ms  10 ms  10 ms     N/A", NarrowWidth),
            Row(secondHalfHeader, NarrowWidth),
            Row("Ok       40 ms  50 ms  50 ms  50 ms", NarrowWidth),
            Row("Failed   10 ms  10 ms  10 ms  10 ms", NarrowWidth),
            Bottom(NarrowWidth),
            Top("Step " + BrowseStep + " Details", NarrowWidth),
            Row("Step Requests", NarrowWidth),
            Row("Metric   Count  RPS", NarrowWidth),
            Row("Total        3", NarrowWidth),
            Row("OK           2    2", NarrowWidth),
            Row("Failed       1    1", NarrowWidth),
            Row("Skipped      0", NarrowWidth),
            Row(string.Empty, NarrowWidth),
            Row("Response Times", NarrowWidth),
            Row(firstHalfHeader, NarrowWidth),
            Row("Ok      10 ms  15 ms  20 ms    5 ms", NarrowWidth),
            Row("Failed  10 ms  10 ms  10 ms     N/A", NarrowWidth),
            Row(secondHalfHeader, NarrowWidth),
            Row("Ok       10 ms  20 ms  20 ms  20 ms", NarrowWidth),
            Row("Failed   10 ms  10 ms  10 ms  10 ms", NarrowWidth),
            Bottom(NarrowWidth),
            Top("Step " + OrderStep + " Details", NarrowWidth),
            Row("Step Requests", NarrowWidth),
            Row("Metric   Count  RPS", NarrowWidth),
            Row("Total        2", NarrowWidth),
            Row("OK           2    2", NarrowWidth),
            Row("Failed       0    0", NarrowWidth),
            Row("Skipped      1", NarrowWidth),
            Row(string.Empty, NarrowWidth),
            Row("Response Times", NarrowWidth),
            Row(firstHalfHeader, NarrowWidth),
            Row("Ok      30 ms  30 ms  30 ms     N/A", NarrowWidth),
            Row("Failed    N/A    N/A    N/A     N/A", NarrowWidth),
            Row(secondHalfHeader, NarrowWidth),
            Row("Ok       30 ms  30 ms  30 ms  30 ms", NarrowWidth),
            Row("Failed     N/A    N/A    N/A    N/A", NarrowWidth),
            Bottom(NarrowWidth),
            Top(LoadSummaryLayout.ErrorsHeader, NarrowWidth),
            Row(BrowseStep + ":", NarrowWidth),
            Row("  Connection refused (Count: 1)", NarrowWidth),
            Bottom(NarrowWidth)
        };
    }

    private static string Top(string header, int width = Width)
    {
        return "╭─ " + header + " " + new string('─', width - 5 - header.Length) + "╮";
    }

    private static string Row(string content, int width = Width)
    {
        return "│ " + content.PadRight(width - PanelWidget.ContentOverhead) + " │";
    }

    private static string Bottom(int width = Width)
    {
        return "╰" + new string('─', width - 2) + "╯";
    }

    private static void AssertGolden(string[] expected, IReadOnlyList<RenderedLine> lines, int width)
    {
        Assert.HasCount(expected.Length, lines);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index], lines[index].Text, $"Line mismatch at row {index}");
            Assert.AreEqual(width, lines[index].Width, $"Width mismatch at row {index}");
            Assert.DoesNotContain(AnsiCodes.Escape, lines[index].Text);
        }
    }

    [Test]
    public async Task Verify_load_summary_golden_at_120_columns_in_color_mode_None()
    {
        await Scenario()
            .Step("The five panels of a passed scenario, plain with zero escape bytes, every line exactly 120 columns", context =>
            {
                var lines = LoadSummaryLayout.Render(new[] { CollectCheckoutFlow() }, Width, ColorMode.None);

                AssertGolden(PlainGolden("Passed"), lines, Width);
            })
            .Step("A scenario failed by an assert reads Failed in the summary panel and is otherwise the same", context =>
            {
                var lines = LoadSummaryLayout.Render(new[] { CollectCheckoutFlow(TestStatus.Failed) }, Width, ColorMode.None);

                CollectionAssert.AreEqual(PlainGolden("Failed"), lines.Select(line => line.Text).ToList());
            })
            .Run();
    }

    [Test]
    public async Task Verify_load_summary_TrueColor_is_the_plain_golden_styled()
    {
        await Scenario()
            .Step("Stripping the SGR sequences yields the plain golden line for line, and the palette lands where it should", context =>
            {
                var lines = LoadSummaryLayout.Render(new[] { CollectCheckoutFlow() }, Width, ColorMode.TrueColor);

                var expected = PlainGolden("Passed");
                Assert.HasCount(expected.Length, lines);
                for (var index = 0; index < expected.Length; index++)
                {
                    Assert.AreEqual(expected[index], SgrSequence.Replace(lines[index].Text, string.Empty), $"Stripped line mismatch at row {index}");
                    Assert.AreEqual(Width, lines[index].Width, $"Width mismatch at row {index}");
                }

                // The panel headers in the dashboard's accent, the errors header in bold red.
                Assert.StartsWith("╭─ \u001b[1;38;2;255;157;61m" + LoadSummaryLayout.SummaryHeader + "\u001b[0m ", lines[0].Text);
                Assert.StartsWith("╭─ \u001b[1;38;5;9m" + LoadSummaryLayout.ErrorsHeader + "\u001b[0m ", lines[31].Text);
                // Dim column headers, the status in green, the ok row green and the failed row red.
                Assert.StartsWith("│ \u001b[2mScenario\u001b[0m", lines[1].Text);
                Assert.Contains("\u001b[38;5;2mPassed\u001b[0m", lines[2].Text);
                Assert.Contains("\u001b[38;5;2mOk\u001b[0m      \u001b[38;5;2m40 ms\u001b[0m", lines[11].Text);
                Assert.Contains("\u001b[38;5;9mFailed\u001b[0m  \u001b[38;5;9m10 ms\u001b[0m", lines[12].Text);
                // The skipped row's label is secondary, the bold table titles bold.
                Assert.Contains("\u001b[2mSkipped\u001b[0m", lines[21].Text);
                Assert.Contains("\u001b[1m" + LoadSummaryLayout.StepRequestsTitle + "\u001b[0m", lines[16].Text);
                Assert.Contains("\u001b[38;5;9m" + BrowseStep + ":\u001b[0m", lines[32].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_load_summary_golden_at_60_columns_stacks_the_tables_and_splits_the_spread()
    {
        await Scenario()
            .Step("At 60 columns the response-time spread is two tables of four under the requests table, the simulation description wraps, and nothing is cut", context =>
            {
                var lines = LoadSummaryLayout.Render(new[] { CollectCheckoutFlow() }, NarrowWidth, ColorMode.None);

                AssertGolden(NarrowPlainGolden(), lines, NarrowWidth);
            })
            .Run();
    }

    [Test]
    public async Task Verify_no_number_is_ever_cut_at_any_width()
    {
        await Scenario()
            .Step("From 40 to 200 columns, with two-digit and five-digit response times alike, every line is exactly the width and no cell is ever truncated", context =>
            {
                var resultSets = new[] { new[] { CollectCheckoutFlow() }, new[] { CollectSlowCheckoutFlow() } };
                foreach (var results in resultSets)
                {
                    Assert.IsLessThanOrEqualTo(40, LoadSummaryLayout.MeasureMinimumWidth(results));
                    for (var width = 40; width <= 200; width++)
                    {
                        var lines = LoadSummaryLayout.Render(results, width, ColorMode.None);

                        Assert.IsNotEmpty(lines);
                        foreach (var line in lines)
                        {
                            Assert.AreEqual(width, line.Width, $"Width mismatch at {width} columns");
                            Assert.DoesNotContain(MarkupText.Ellipsis.ToString(), line.Text, $"Truncated content at {width} columns: {line.Text}");
                        }
                    }
                }
            })
            .Step("At 40 columns the summary panel lists label/value lines and the spread splits into halves — into pairs once five-digit values widen the columns", context =>
            {
                var texts = LoadSummaryLayout.Render(new[] { CollectCheckoutFlow() }, 40, ColorMode.None).Select(line => line.Text).ToList();
                Assert.Contains(Row("Scenario        Checkout flow", 40), texts);
                Assert.Contains(Row("Execution Time  00:00:00:10", 40), texts);
                Assert.Contains(Row("Test Run Time   00:00:12:00", 40), texts);
                Assert.Contains(Row("Status          Passed", 40), texts);
                Assert.Contains(Row("Metric    Min   Mean    Max  StdDev", 40), texts);
                Assert.Contains(Row("Metric  Median    P75    P95    P99", 40), texts);

                var slowTexts = LoadSummaryLayout.Render(new[] { CollectSlowCheckoutFlow() }, 40, ColorMode.None).Select(line => line.Text).ToList();
                Assert.Contains(Row("Metric       Min      Mean", 40), slowTexts);
                Assert.Contains(Row("Metric       Max  StdDev", 40), slowTexts);
                Assert.Contains(Row("Metric    Median       P75", 40), slowTexts);
                Assert.Contains(Row("Metric       P95       P99", 40), slowTexts);
            })
            .Step("Below its minimum width the summary is laid out at the minimum: every line is that wide, and only the scenario name — text — is cut there", context =>
            {
                var results = new[] { CollectCheckoutFlow() };
                var minimumWidth = LoadSummaryLayout.MeasureMinimumWidth(results);
                Assert.IsGreaterThan(20, minimumWidth);

                var lines = LoadSummaryLayout.Render(results, 20, ColorMode.None);

                Assert.IsNotEmpty(lines);
                foreach (var line in lines)
                    Assert.AreEqual(minimumWidth, line.Width);

                // The label/value lines are 27 wide by their durations, which the minimum fits
                // exactly; only text is cut — the 29-wide scenario name line and the step
                // panels' headers — never a table cell.
                var truncated = lines.Where(line => line.Text.Contains(MarkupText.Ellipsis)).Select(line => line.Text).ToList();
                Assert.IsNotEmpty(truncated);
                foreach (var text in truncated)
                    Assert.IsTrue(text.StartsWith("╭─ Step ", StringComparison.Ordinal) || text.StartsWith("│ Scenario        Checkout f" + MarkupText.Ellipsis, StringComparison.Ordinal), "Unexpected truncation: " + text);

                Assert.Contains(Row("Execution Time  00:00:00:10", minimumWidth), lines.Select(line => line.Text).ToList());
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_scenario_that_recorded_nothing_renders_empty_metrics_and_no_errors_panel()
    {
        await Scenario()
            .Step("Zero counts, N/A response times, no simulation rows and no errors panel", context =>
            {
                var scenario = new Scenario("Empty flow");
                scenario.Steps.Add(new Step { Name = "Only step", Id = "only-step" });
                var collector = new ScenarioLoadCollector(scenario);

                var lines = LoadSummaryLayout.Render(new[] { new KeyValuePair<Scenario, ScenarioLoadResult>(scenario, collector.GetCurrentResult(true)) }, Width, ColorMode.None);

                var texts = lines.Select(line => line.Text).ToList();
                Assert.AreEqual(4, texts.Count(text => text.StartsWith("╰", StringComparison.Ordinal)));
                Assert.DoesNotContain(Top(LoadSummaryLayout.ErrorsHeader), texts);
                Assert.Contains(Row("Empty flow  00:00:00:00     00:00:00:00    Passed"), texts);
                Assert.Contains(Row("Type"), texts);
                // With every response time N/A the numeric columns are as narrow as their headers.
                Assert.Contains(Row("Total       0         Ok      N/A   N/A  N/A     N/A     N/A  N/A  N/A  N/A"), texts);
                Assert.Contains(Row("Skipped      0"), texts);
            })
            .Step("Null inputs are rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => LoadSummaryLayout.Render(null!, Width, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => LoadSummaryLayout.MeasureMinimumWidth(null!));
                Assert.ThrowsExactly<ArgumentException>(() => LoadSummaryLayout.Render(new[] { new KeyValuePair<Scenario, ScenarioLoadResult>(new Scenario("x"), null!) }, Width, ColorMode.None));
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_messages_wrap_at_word_boundaries_with_a_hanging_indent()
    {
        await Scenario()
            .Step("WrapText breaks at spaces, splits a word longer than the width, and leaves short or unwrappable text whole", context =>
            {
                CollectionAssert.AreEqual(new[] { "aaaa bbbb", "cccc" }, LoadSummaryLayout.WrapText("aaaa bbbb cccc", 9).ToList());
                CollectionAssert.AreEqual(new[] { "aaaa", "bbbb", "cccc" }, LoadSummaryLayout.WrapText("aaaa bbbb cccc", 6).ToList());
                CollectionAssert.AreEqual(new[] { "abcdefgh", "ij" }, LoadSummaryLayout.WrapText("abcdefghij", 8).ToList());
                CollectionAssert.AreEqual(new[] { "short" }, LoadSummaryLayout.WrapText("short", 8).ToList());
                CollectionAssert.AreEqual(new[] { "no width" }, LoadSummaryLayout.WrapText("no width", 0).ToList());
                Assert.ThrowsExactly<ArgumentNullException>(() => LoadSummaryLayout.WrapText(null!, 8));
            })
            .Step("A long error message in the panel continues on indented lines instead of being cut", context =>
            {
                var scenario = new Scenario("Wrap flow");
                scenario.Steps.Add(new Step { Name = "Only step", Id = "only-step" });
                var collector = new ScenarioLoadCollector(scenario);
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, DateTime.UtcNow.AddHours(1));
                var message = string.Join(" ", Enumerable.Repeat("word", 40));
                Record(collector, TestStatus.Failed, StepResult("Only step", StepStatus.Failed, 10, new InvalidOperationException(message)));

                var lines = LoadSummaryLayout.Render(new[] { new KeyValuePair<Scenario, ScenarioLoadResult>(scenario, collector.GetCurrentResult(true)) }, Width, ColorMode.None);

                // The 210-character message wraps at 112 columns (the inner width less the
                // continuation indent): 22 words on the first line, the remaining 18 and the
                // count — 100 characters — on the second, indented two columns deeper.
                var texts = lines.Select(line => line.Text).ToList();
                var errorsHeader = texts.IndexOf(Top(LoadSummaryLayout.ErrorsHeader));
                Assert.IsGreaterThan(0, errorsHeader);
                Assert.AreEqual(Row("Only step:"), texts[errorsHeader + 1]);
                Assert.AreEqual(Row("  " + string.Join(" ", Enumerable.Repeat("word", 22))), texts[errorsHeader + 2]);
                Assert.AreEqual(Row("    " + string.Join(" ", Enumerable.Repeat("word", 18)) + " (Count: 1)"), texts[errorsHeader + 3]);
                Assert.AreEqual(Bottom(), texts[errorsHeader + 4]);
                foreach (var line in lines)
                    Assert.AreEqual(Width, line.Width);
            })
            .Run();
    }
}
