using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class LoadSummaryLayoutTests : Test
{
    private const string ReportPath = "C:/results/TestReport.html";

    private static LoadViewHeader Header()
    {
        return new LoadViewHeader { ExecutionEnvironment = "local", TargetEnvironment = "test" };
    }

    [Test]
    public async Task Verify_summary_golden_at_120_columns()
    {
        await Scenario()
            .Step("The summary renders the header block, the scenario info, the phases, the totals, the steps, the failure and the step errors", context =>
            {
                var lines = LoadSummaryLayout.Render(LoadViewSamples.SummaryResults(), 120, ColorMode.None, ReportPath, Header());

                AssertFrame(new[]
                {
                    "",
                    "════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════",
                    "  ⚡ TestFuzn",
                    "  Execution Environment: local    Target Environment: test",
                    "",
                    "  Scenario - Checkout flow                                                                               Failed   5m 12s",
                    "",
                    "  Description   Buys a product and checks out",
                    "",
                    "  Test Phases ──────────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  Phase           Duration  Started   Ended   ",
                    "  Init            2s        14:04:21  14:04:23",
                    "  Warmup          30s       14:04:23  14:04:53",
                    "    Fixed Load - Rate: 10, Interval: 0:00:01, Duration: 0:00:30 (Warmup)",
                    "  Execution       4m 33s    14:04:53  14:09:26",
                    "    Fixed Load - Rate: 400, Interval: 0:00:01, Duration: 0:05:00",
                    "  Cleanup         7s        14:09:26  14:09:33",
                    "  Total Test Run  5m 12s    14:04:21  14:09:33",
                    "",
                    "  Requests ─────────────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  Total Requests 24000    Successful 16001 (66.7%)    Failed 7999 (33.3%)    Requests/sec 265    Warmup 1203",
                    "",
                    "  Step Performance ─────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  Step                 Type    Requests  RPS    Mean  Median     P75     P95     P99    Min     Max  StdDev",
                    "  All Steps (Summary)  Ok         16001  265   38 ms   35 ms   48 ms   72 ms   94 ms  12 ms  312 ms   15 ms",
                    "                       Failed      7999  132  102 ms   99 ms  110 ms  140 ms  160 ms  88 ms  201 ms   20 ms",
                    "  Add to cart          Ok         24000  397   18 ms   16 ms   22 ms   40 ms   55 ms   8 ms  120 ms    6 ms",
                    "  Checkout             Ok         16001  265   58 ms   52 ms   70 ms   90 ms  130 ms  20 ms  410 ms   19 ms",
                    "                       Failed      7999  132  102 ms   99 ms  110 ms  140 ms  160 ms  95 ms  980 ms   21 ms",
                    "",
                    "  Failure Details ──────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  AssertWhileRunning Failed",
                    "    System.InvalidOperationException: Assert.IsLessThan failed. p95 too high: 240 ms",
                    "",
                    "  Step Errors ──────────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  Checkout:",
                    "    Assertion failed. (Count: 7999)",
                    "",
                    "  Report ───────────────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  C:/results/TestReport.html",
                    "════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════",
                    ""
                }, 120, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_summary_narrows_without_cutting_numbers()
    {
        await Scenario()
            .Step("At 60 columns, without a header, the tables drop columns and the text wraps", context =>
            {
                var lines = LoadSummaryLayout.Render(LoadViewSamples.SummaryResults(), 60, ColorMode.None, ReportPath);

                AssertFrame(new[]
                {
                    "",
                    "════════════════════════════════════════════════════════════",
                    "  Scenario - Checkout flow                   Failed   5m 12s",
                    "",
                    "  Description   Buys a product and checks out",
                    "",
                    "  Test Phases ──────────────────────────────────────────────",
                    "  Phase           Duration  Started   Ended   ",
                    "  Init            2s        14:04:21  14:04:23",
                    "  Warmup          30s       14:04:23  14:04:53",
                    "    Fixed Load - Rate: 10, Interval: 0:00:01, Duration:",
                    "    0:00:30 (Warmup)",
                    "  Execution       4m 33s    14:04:53  14:09:26",
                    "    Fixed Load - Rate: 400, Interval: 0:00:01, Duration:",
                    "    0:05:00",
                    "  Cleanup         7s        14:09:26  14:09:33",
                    "  Total Test Run  5m 12s    14:04:21  14:09:33",
                    "",
                    "  Requests ─────────────────────────────────────────────────",
                    "  Total Requests 24000    Successful 16001 (66.7%)",
                    "  Failed 7999 (33.3%)    Requests/sec 265    Warmup 1203",
                    "",
                    "  Step Performance ─────────────────────────────────────────",
                    "  Step                 Type    Requests  RPS    Mean     P95",
                    "  All Steps (Summary)  Ok         16001  265   38 ms   72 ms",
                    "                       Failed      7999  132  102 ms  140 ms",
                    "  Add to cart          Ok         24000  397   18 ms   40 ms",
                    "  Checkout             Ok         16001  265   58 ms   90 ms",
                    "                       Failed      7999  132  102 ms  140 ms",
                    "",
                    "  Failure Details ──────────────────────────────────────────",
                    "  AssertWhileRunning Failed",
                    "    System.InvalidOperationException: Assert.IsLessThan",
                    "    failed. p95 too high: 240 ms",
                    "",
                    "  Step Errors ──────────────────────────────────────────────",
                    "  Checkout:",
                    "    Assertion failed. (Count: 7999)",
                    "",
                    "  Report ───────────────────────────────────────────────────",
                    "  C:/results/TestReport.html",
                    "════════════════════════════════════════════════════════════",
                    ""
                }, 60, lines);
            })
            .Step("A width below the minimum lays out at the minimum instead of cutting", context =>
            {
                var results = LoadViewSamples.SummaryResults();
                var minimumWidth = LoadSummaryLayout.MeasureMinimumWidth(results);
                var lines = LoadSummaryLayout.Render(results, 10, ColorMode.None);

                Assert.IsGreaterThan(10, minimumWidth);
                AssertMaximumWidth(minimumWidth + LiveDashboardLayout.Gutter.Length, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_several_scenarios_are_numbered_as_in_the_html_report()
    {
        await Scenario()
            .Step("Two scenarios are titled Scenario 1 and Scenario 2, separated by a blank line", context =>
            {
                var results = LoadViewSamples.SummaryResults();
                foreach (var pair in LoadViewSamples.SummaryResults())
                    results.Add(new Scenario("Browse catalog"), pair.Value);

                var lines = LoadSummaryLayout.Render(results, 120, ColorMode.None);

                Assert.Contains("Scenario 1 - Checkout flow", LineContaining(lines, "Scenario 1 - "));
                Assert.Contains("Scenario 2 - Browse catalog", LineContaining(lines, "Scenario 2 - "));
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_passed_scenario_leaves_out_the_failure_and_error_sections()
    {
        await Scenario()
            .Step("A scenario without failures shows Passed, a single zero Failed summary row, and no failure or error section", context =>
            {
                var lines = LoadSummaryLayout.Render(PassedResults(), 120, ColorMode.None);

                Assert.Contains(LiveDashboardLayout.PassedText, LineContaining(lines, LiveDashboardLayout.ScenarioTitlePrefix));
                foreach (var line in lines)
                {
                    Assert.DoesNotContain(FailureDetailsLayout.Header, line.Text);
                    Assert.DoesNotContain(LiveDashboardLayout.StepErrorsHeader, line.Text);
                }

                var stepLines = SectionLines(lines, StepPerformanceLayout.Header);
                var failedRows = stepLines.Where(line => line.Contains(StepPerformanceLayout.FailedType)).ToList();
                Assert.HasCount(1, failedRows);
                foreach (var failedRow in failedRows)
                    Assert.Contains(" 0 ", failedRow);
            })
            .Step("Null results are rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => LoadSummaryLayout.Render(null!, 120, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => LoadSummaryLayout.MeasureMinimumWidth(null!));
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_failed_scenario_without_an_assertion_still_gets_a_failure_section()
    {
        await Scenario()
            .Step("A scenario failed by its requests shows Failure Details with what is known", context =>
            {
                var results = LoadViewSamples.SummaryResults();
                foreach (var pair in results)
                    pair.Value.AssertWhileRunningException = null;

                var lines = LoadSummaryLayout.Render(results, 120, ColorMode.None).Select(line => line.Text).ToList();

                var headerIndex = lines.FindIndex(text => text.TrimStart().StartsWith(FailureDetailsLayout.Header));

                Assert.IsGreaterThanOrEqualTo(0, headerIndex);
                Assert.Contains(LiveDashboardLayout.ScenarioFailedText, lines[headerIndex + 1]);
                Assert.Contains("No assertion failed.", string.Join(" ", lines));
            })
            .Run();
    }

    private static string LineContaining(IReadOnlyList<RenderedLine> lines, string text)
    {
        return lines.Select(line => line.Text).First(candidate => candidate.Contains(text));
    }

    private static List<string> SectionLines(IReadOnlyList<RenderedLine> lines, string header)
    {
        var sectionLines = new List<string>();
        var inSection = false;
        foreach (var line in lines)
        {
            if (line.Text.TrimStart().StartsWith(header))
            {
                inSection = true;
                continue;
            }

            if (!inSection)
                continue;

            if (line.Text.Length == 0)
                break;

            sectionLines.Add(line.Text);
        }

        return sectionLines;
    }

    private static Dictionary<Scenario, ScenarioLoadResult> PassedResults()
    {
        var results = LoadViewSamples.SummaryResults();
        foreach (var pair in results)
        {
            pair.Value.Status = TestStatus.Passed;
            pair.Value.AssertWhileRunningException = null;
            pair.Value.Failed = LoadViewSamples.CoreStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            foreach (var step in pair.Value.Steps.Values)
            {
                step.Failed = LoadViewSamples.CoreStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                step.Errors = new Dictionary<string, ErrorEntry>();
            }
        }

        return results;
    }

    private static void AssertFrame(IReadOnlyList<string> expectedLines, int expectedWidth, IReadOnlyList<RenderedLine> actualLines)
    {
        Assert.HasCount(expectedLines.Count, actualLines);
        for (var index = 0; index < expectedLines.Count; index++)
        {
            Assert.AreEqual(expectedLines[index], actualLines[index].Text, $"Text mismatch at row {index}");
            Assert.IsLessThanOrEqualTo(expectedWidth, actualLines[index].Width, $"Line {index} exceeds width {expectedWidth}");
        }
    }

    private static void AssertMaximumWidth(int width, IReadOnlyList<RenderedLine> lines)
    {
        for (var index = 0; index < lines.Count; index++)
            Assert.IsLessThanOrEqualTo(width, lines[index].Width, $"Line {index} exceeds width {width}");
    }
}
