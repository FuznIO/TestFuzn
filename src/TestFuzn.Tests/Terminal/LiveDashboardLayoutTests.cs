using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class LiveDashboardLayoutTests : Test
{
    private const string SpinnerGlyph = "⠙";

    private const string FooterLine = LiveDashboardLayout.Gutter + "q quit";

    private static LoadViewHeader Header()
    {
        return new LoadViewHeader { ExecutionEnvironment = "local", TargetEnvironment = "test" };
    }

    [Test]
    public async Task Verify_live_view_golden_frame_at_120x40()
    {
        await Scenario()
            .Step("One scenario with a header renders the wordmark, the environments, the phases, the totals and the step performance table with every percentile", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 120, 40, ColorMode.None, SpinnerGlyph, Header());

                AssertFrame(new[]
                {
                    "════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════",
                    "  ⚡ TestFuzn",
                    "  Execution Environment: local    Target Environment: test",
                    "",
                    "  Scenario - Checkout flow                                                                            ⠙ Running   2m 15s",
                    "",
                    "  Test Phases ──────────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  Phase           Duration  Started   Ended   ",
                    "  Init            2s        14:04:21  14:04:23",
                    "  Warmup          30s       14:04:23  14:04:53",
                    "    Fixed Load - Rate: 10, Interval: 0:00:01, Duration: 0:00:30 (Warmup)",
                    "  Execution       1m 43s    14:04:53  —       ",
                    "    Fixed Load - Rate: 400, Interval: 0:00:01, Duration: 0:05:00",
                    "  Cleanup         —         —         —       ",
                    "  Total Test Run  2m 15s    14:04:21  —       ",
                    "",
                    "  Requests ─────────────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  Total Requests 12512    Successful 12480 (99.7%)    Failed 32 (0.3%)    Requests/sec 142    Warmup 1203",
                    "",
                    "  Step Performance ─────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  Step                 Type    Requests  RPS    Mean  Median     P75     P95     P99    Min     Max  StdDev",
                    "  All Steps (Summary)  Ok         12480  142   38 ms   35 ms   48 ms   72 ms   94 ms  12 ms  312 ms   15 ms",
                    "                       Failed        32    1  102 ms   99 ms  110 ms  140 ms  160 ms  88 ms  201 ms   20 ms",
                    "  Add to cart          Ok          6250   71   18 ms   16 ms   22 ms   40 ms   55 ms   8 ms  120 ms    6 ms",
                    "                       Failed         2    1  102 ms   99 ms  110 ms  140 ms  160 ms  95 ms  980 ms   21 ms",
                    "  Checkout             Ok          6230   69   58 ms   52 ms   70 ms   90 ms  130 ms  20 ms  410 ms   19 ms",
                    "                       Failed        30    1  102 ms   99 ms  110 ms  140 ms  160 ms  95 ms  980 ms   21 ms",
                    "    → Pay              Ok          6230   69   22 ms   20 ms   26 ms   44 ms   61 ms  10 ms  150 ms    7 ms",
                    "",
                    "  Step Errors ──────────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  Checkout:",
                    "    Connection refused (localhost:7058) (Count: 30)",
                    "  Add to cart:",
                    "    Timeout after 30s (Count: 2)"
                }, 120, lines);
            })
            .Step("The frame pads to the window height with the footer on the last row", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 120, 40, ColorMode.None, SpinnerGlyph, Header());

                Assert.HasCount(40, lines);
                AssertLine(string.Empty, 0, lines[38]);
                AssertLine(FooterLine, FooterLine.Length, lines[39]);
                AssertMaximumWidth(120, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_live_view_narrows_by_dropping_columns_and_wrapping_totals()
    {
        await Scenario()
            .Step("At 80 columns, without a header, the step table drops columns and the totals wrap to a second line", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 80, 40, ColorMode.None, SpinnerGlyph);

                AssertFrame(new[]
                {
                    "════════════════════════════════════════════════════════════════════════════════",
                    "  Scenario - Checkout flow                                    ⠙ Running   2m 15s",
                    "",
                    "  Test Phases ──────────────────────────────────────────────────────────────────",
                    "  Phase           Duration  Started   Ended   ",
                    "  Init            2s        14:04:21  14:04:23",
                    "  Warmup          30s       14:04:23  14:04:53",
                    "    Fixed Load - Rate: 10, Interval: 0:00:01, Duration: 0:00:30 (Warmup)",
                    "  Execution       1m 43s    14:04:53  —       ",
                    "    Fixed Load - Rate: 400, Interval: 0:00:01, Duration: 0:05:00",
                    "  Cleanup         —         —         —       ",
                    "  Total Test Run  2m 15s    14:04:21  —       ",
                    "",
                    "  Requests ─────────────────────────────────────────────────────────────────────",
                    "  Total Requests 12512    Successful 12480 (99.7%)    Failed 32 (0.3%)",
                    "  Requests/sec 142    Warmup 1203",
                    "",
                    "  Step Performance ─────────────────────────────────────────────────────────────",
                    "  Step                 Type    Requests  RPS    Mean  Median     P95     P99",
                    "  All Steps (Summary)  Ok         12480  142   38 ms   35 ms   72 ms   94 ms",
                    "                       Failed        32    1  102 ms   99 ms  140 ms  160 ms",
                    "  Add to cart          Ok          6250   71   18 ms   16 ms   40 ms   55 ms",
                    "                       Failed         2    1  102 ms   99 ms  140 ms  160 ms",
                    "  Checkout             Ok          6230   69   58 ms   52 ms   90 ms  130 ms",
                    "                       Failed        30    1  102 ms   99 ms  140 ms  160 ms",
                    "    → Pay              Ok          6230   69   22 ms   20 ms   44 ms   61 ms",
                    "",
                    "  Step Errors ──────────────────────────────────────────────────────────────────",
                    "  Checkout:",
                    "    Connection refused (localhost:7058) (Count: 30)",
                    "  Add to cart:",
                    "    Timeout after 30s (Count: 2)"
                }, 80, lines);
            })
            .Step("Every width from 1 to 130 keeps every line inside the frame, with and without the header", context =>
            {
                var snapshots = new[] { LoadViewSamples.RunningSnapshot(), LoadViewSamples.SecondSnapshot() };
                var header = new LoadViewHeader { ExecutionEnvironment = "local", TargetEnvironment = "test" };
                for (var width = 1; width <= 130; width++)
                {
                    AssertMaximumWidth(width, LiveDashboardLayout.Render(snapshots, width, 0, ColorMode.TrueColor, SpinnerGlyph));
                    AssertMaximumWidth(width, LiveDashboardLayout.Render(new[] { snapshots[0] }, width, 0, ColorMode.TrueColor, SpinnerGlyph));
                    AssertMaximumWidth(width, LiveDashboardLayout.Render(snapshots, width, 0, ColorMode.TrueColor, SpinnerGlyph, header));
                    AssertMaximumWidth(width, LiveDashboardLayout.Render(new[] { snapshots[0] }, width, 0, ColorMode.TrueColor, SpinnerGlyph, header));
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_several_scenarios_render_one_overview_row_each()
    {
        await Scenario()
            .Step("Two scenarios render the scenario performance table and the errors of both", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot(), LoadViewSamples.SecondSnapshot() }, 120, 30, ColorMode.None, SpinnerGlyph);

                AssertFrame(new[]
                {
                    "════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════",
                    "  Scenarios 2                                                                                         ⠙ Running   2m 15s",
                    "",
                    "  Scenario Performance ─────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  #  Scenario        Phase      Status     Requests  RPS   Mean  Median    P95  Failed",
                    "  1  Checkout flow   Execution  ⠙ Running     12512  142  38 ms   35 ms  72 ms      32",
                    "  2  Browse catalog  Warmup     ⠙ Running      2000   48  12 ms   11 ms  22 ms       0",
                    "",
                    "  Step Errors ──────────────────────────────────────────────────────────────────────────────────────────────────────────",
                    "  Checkout flow · Checkout:",
                    "    Connection refused (localhost:7058) (Count: 30)",
                    "  Checkout flow · Add to cart:",
                    "    Timeout after 30s (Count: 2)"
                }, 120, lines);
            })
            .Step("A frame taller than the window keeps the footer on the last row and cuts the content", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 120, 6, ColorMode.None);

                Assert.HasCount(6, lines);
                AssertLine(FooterLine, FooterLine.Length, lines[5]);
            })
            .Step("A one-row window is just the footer", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 120, 1, ColorMode.None);

                Assert.HasCount(1, lines);
                AssertLine(FooterLine, FooterLine.Length, lines[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_status_badges_and_color_modes()
    {
        await Scenario()
            .Step("A completed scenario shows Passed, a skipped one shows Skipped", context =>
            {
                var passed = LiveDashboardLayout.Render(new[] { CompletedSnapshot(TestStatus.Passed) }, 120, 20, ColorMode.None);
                var skipped = LiveDashboardLayout.Render(new[] { CompletedSnapshot(TestStatus.Skipped) }, 120, 20, ColorMode.None);

                Assert.Contains(LiveDashboardLayout.PassedText + "   5m 12s", TitleLine(passed));
                Assert.Contains(LiveDashboardLayout.SkippedText + "   5m 12s", TitleLine(skipped));
            })
            .Step("The spinner glyph replaces the dot on a running badge only", context =>
            {
                var running = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 120, 20, ColorMode.None, SpinnerGlyph);
                var passed = LiveDashboardLayout.Render(new[] { CompletedSnapshot(TestStatus.Passed) }, 120, 20, ColorMode.None, SpinnerGlyph);

                Assert.Contains(SpinnerGlyph + " " + LiveDashboardLayout.RunningText, TitleLine(running));
                Assert.DoesNotContain(SpinnerGlyph, TitleLine(passed));
            })
            .Step("Color mode None emits zero escape bytes across the whole frame", context =>
            {
                foreach (var line in LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 120, 40, ColorMode.None, SpinnerGlyph))
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("TrueColor styles the section headers with the warm accent", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 120, 40, ColorMode.TrueColor, SpinnerGlyph);

                var header = lines.Select(line => line.Text).First(text => text.Contains(TestPhasesLayout.Header));
                Assert.Contains("\u001b[1;38;2;255;157;61m" + TestPhasesLayout.Header + "\u001b[0m", header);
            })
            .Run();
    }

    [Test]
    public async Task Verify_argument_handling_and_determinism()
    {
        await Scenario()
            .Step("Identical inputs render an identical frame", context =>
            {
                var first = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 120, 40, ColorMode.TrueColor, SpinnerGlyph);
                var second = LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 120, 40, ColorMode.TrueColor, SpinnerGlyph);

                Assert.HasCount(first.Count, second);
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.AreEqual(first[index].Text, second[index].Text, $"Text mismatch at row {index}");
                    Assert.AreEqual(first[index].Width, second[index].Width, $"Width mismatch at row {index}");
                }
            })
            .Step("A width below 1 renders nothing and null snapshots are rejected", context =>
            {
                Assert.IsEmpty(LiveDashboardLayout.Render(new[] { LoadViewSamples.RunningSnapshot() }, 0, 40, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => LiveDashboardLayout.Render(null!, 120, 40, ColorMode.None));
            })
            .Step("No snapshots renders just the footer padded to the window height", context =>
            {
                var lines = LiveDashboardLayout.Render(Array.Empty<LiveMetricsSnapshot>(), 40, 5, ColorMode.None);

                Assert.HasCount(5, lines);
                AssertLine(FooterLine, FooterLine.Length, lines[4]);
            })
            .Run();
    }

    private static LiveMetricsSnapshot CompletedSnapshot(TestStatus status)
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Checkout flow",
            Duration = TimeSpan.FromSeconds(312),
            Timestamp = new DateTime(2026, 9, 20, 12, 9, 33, DateTimeKind.Utc),
            IsCompleted = true,
            Status = status,
            Ok = LoadViewSamples.Stats(16001, 265, 12, 38, 15, 35, 48, 72, 94, 312),
            Failed = LoadViewSamples.Stats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        };
    }

    private static string TitleLine(IReadOnlyList<RenderedLine> lines)
    {
        return lines.Select(line => line.Text).First(text => text.Contains(LiveDashboardLayout.ScenarioTitlePrefix));
    }

    private static void AssertFrame(IReadOnlyList<string> expectedLines, int expectedWidth, IReadOnlyList<RenderedLine> actualLines)
    {
        Assert.IsGreaterThanOrEqualTo(expectedLines.Count, actualLines.Count);
        for (var index = 0; index < expectedLines.Count; index++)
        {
            Assert.AreEqual(expectedLines[index], actualLines[index].Text, $"Text mismatch at row {index}");
            Assert.IsLessThanOrEqualTo(expectedWidth, actualLines[index].Width, $"Line {index} exceeds width {expectedWidth}");
        }
    }

    private static void AssertLine(string expectedText, int expectedWidth, RenderedLine actualLine)
    {
        Assert.AreEqual(expectedText, actualLine.Text);
        Assert.AreEqual(expectedWidth, actualLine.Width);
    }

    private static void AssertMaximumWidth(int width, IReadOnlyList<RenderedLine> lines)
    {
        for (var index = 0; index < lines.Count; index++)
            Assert.IsLessThanOrEqualTo(width, lines[index].Width, $"Line {index} exceeds width {width}");
    }
}
