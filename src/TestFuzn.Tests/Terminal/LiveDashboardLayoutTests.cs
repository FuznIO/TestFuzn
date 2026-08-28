using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class LiveDashboardLayoutTests : Test
{
    /// <summary>
    /// A mid-run snapshot with every dashboard element populated: determinate plan, warmup
    /// counts, full percentile spreads, ring samples with their sparkline series, two steps and
    /// two errors. The golden frames below are derived from these values by hand.
    /// </summary>
    private static LiveMetricsSnapshot RichSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Checkout flow",
            Phase = LoadTestPhase.Measurement,
            PhaseLabel = "sim 1/2: Fixed 50 rps",
            Duration = TimeSpan.FromSeconds(135),
            PlannedDuration = TimeSpan.FromSeconds(300),
            PlannedMeasurementDuration = TimeSpan.FromSeconds(240),
            ProgressFraction = 0.45,
            EstimatedTimeRemaining = TimeSpan.FromSeconds(165),
            IsCompleted = false,
            Status = TestStatus.Passed,
            RequestCountOk = 12480,
            RequestCountFailed = 32,
            WarmupRequestCountOk = 1200,
            WarmupRequestCountFailed = 3,
            RequestsPerSecond = 142,
            Ok = new LiveStats
            {
                RequestCount = 12480,
                RequestsPerSecond = 142,
                ResponseTimeMin = TimeSpan.FromMilliseconds(12),
                ResponseTimeMean = TimeSpan.FromMilliseconds(38),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(35),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(48),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(72),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(94),
                ResponseTimeMax = TimeSpan.FromMilliseconds(312)
            },
            Failed = new LiveStats
            {
                RequestCount = 32,
                RequestsPerSecond = 1,
                ResponseTimeMin = TimeSpan.FromMilliseconds(88),
                ResponseTimeMean = TimeSpan.FromMilliseconds(102),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(99),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(110),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(140),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(160),
                ResponseTimeMax = TimeSpan.FromMilliseconds(201)
            },
            Samples = new[]
            {
                Sample(1, 88, 0, 45), Sample(2, 96, 0, 44), Sample(3, 104, 0, 42), Sample(4, 111, 1, 41), Sample(5, 120, 0, 40),
                Sample(6, 128, 0, 40), Sample(7, 133, 1, 39), Sample(8, 138, 0, 39), Sample(9, 140, 0, 38), Sample(10, 141, 1, 38)
            },
            RequestsPerSecondSeries = new double[] { 88, 96, 104, 112, 120, 128, 134, 138, 140, 142 },
            ResponseTimePercentile95Series = new double[] { 45, 44, 42, 41, 40, 40, 39, 39, 38, 38 },
            Errors = new[]
            {
                new LiveErrorEntry { StepName = "Checkout", Message = "Connection refused (localhost:7058)", Count = 30 },
                new LiveErrorEntry { StepName = "Add to cart", Message = "Timeout after 30s", Count = 2 }
            },
            Steps = new[]
            {
                new LiveStepMetrics
                {
                    Name = "Add to cart",
                    RequestCountOk = 6250,
                    RequestCountFailed = 2,
                    RequestsPerSecond = 71.4,
                    AverageRequestsPerSecond = 69.5,
                    ResponseTimeMean = TimeSpan.FromMilliseconds(18),
                    ResponseTimePercentile95 = TimeSpan.FromMilliseconds(40)
                },
                new LiveStepMetrics
                {
                    Name = "Checkout",
                    RequestCountOk = 6230,
                    RequestCountFailed = 30,
                    RequestsPerSecond = 9.6,
                    AverageRequestsPerSecond = 69.2,
                    ResponseTimeMean = TimeSpan.FromMilliseconds(58),
                    ResponseTimePercentile95 = TimeSpan.FromMilliseconds(90)
                }
            }
        };
    }

    /// <summary>A warming-up snapshot with a count-based (indeterminate) plan and no data yet.</summary>
    private static LiveMetricsSnapshot IndeterminateSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Browse catalog",
            Phase = LoadTestPhase.Warmup,
            PhaseLabel = "warmup: Fixed 50 rps",
            Duration = TimeSpan.FromSeconds(42)
        };
    }

    /// <summary>A completed snapshot failed by an assert, carrying the failure reason.</summary>
    private static LiveMetricsSnapshot FailedSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Checkout flow",
            PhaseLabel = "completed",
            Duration = TimeSpan.FromSeconds(300),
            IsCompleted = true,
            Status = TestStatus.Failed,
            StatusDetail = "Assert.IsLessThan failed. p95 too high: 240 ms"
        };
    }

    /// <summary>
    /// A long run with 9-digit counts, a 7-digit current rate and hour-class response times —
    /// the widest cells the requests table has to fit without truncating a number.
    /// </summary>
    private static LiveMetricsSnapshot HugeSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Huge numbers",
            PhaseLabel = "Fixed Load 10000000 rps",
            Duration = TimeSpan.FromHours(30),
            Ok = new LiveStats
            {
                RequestCount = 987654321,
                ResponseTimeMin = TimeSpan.FromMilliseconds(3600000),
                ResponseTimeMean = TimeSpan.FromMilliseconds(7200000),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(5400000),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(6000000),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(7200000),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(8000000),
                ResponseTimeMax = TimeSpan.FromMilliseconds(9000000)
            },
            Failed = new LiveStats
            {
                RequestCount = 123456789,
                ResponseTimeMin = TimeSpan.FromMilliseconds(100000),
                ResponseTimeMean = TimeSpan.FromMilliseconds(500000),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(400000),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(600000),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(800000),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(900000),
                ResponseTimeMax = TimeSpan.FromMilliseconds(950000)
            },
            Samples = new[] { Sample(1, 9999999, 1, 7200000) },
            RequestsPerSecondSeries = new double[] { 10000000 },
            ResponseTimePercentile95Series = new double[] { 7200000 }
        };
    }

    /// <summary>
    /// A ramp whose newest interval runs at 50 rps (45 ok + 5 failed) while the lifetime
    /// averages sit at 30/3 — the two measures disagree, so a frame shows which one it uses.
    /// </summary>
    private static LiveMetricsSnapshot RampSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Ramp",
            PhaseLabel = "Gradual Load 10→50 rps",
            Duration = TimeSpan.FromSeconds(60),
            RequestsPerSecond = 33,
            Ok = new LiveStats
            {
                RequestCount = 1800,
                RequestsPerSecond = 30,
                ResponseTimeMin = TimeSpan.FromMilliseconds(10),
                ResponseTimeMean = TimeSpan.FromMilliseconds(20),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(20),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(25),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(40),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(60),
                ResponseTimeMax = TimeSpan.FromMilliseconds(90)
            },
            Failed = new LiveStats
            {
                RequestCount = 180,
                RequestsPerSecond = 3,
                ResponseTimeMin = TimeSpan.FromMilliseconds(15),
                ResponseTimeMean = TimeSpan.FromMilliseconds(45),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(45),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(55),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(70),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(80),
                ResponseTimeMax = TimeSpan.FromMilliseconds(95)
            },
            Samples = new[] { Sample(1, 10, 0, 40), Sample(2, 20, 0, 40), Sample(3, 28, 2, 40), Sample(4, 38, 2, 40), Sample(5, 45, 5, 40) },
            RequestsPerSecondSeries = new double[] { 10, 20, 30, 40, 50 },
            ResponseTimePercentile95Series = new double[] { 40, 40, 40, 40, 40 },
            Steps = new[]
            {
                new LiveStepMetrics
                {
                    Name = "Add to cart",
                    RequestCountOk = 1800,
                    RequestCountFailed = 180,
                    RequestsPerSecond = 50,
                    AverageRequestsPerSecond = 30,
                    ResponseTimeMean = TimeSpan.FromMilliseconds(20),
                    ResponseTimePercentile95 = TimeSpan.FromMilliseconds(40)
                }
            }
        };
    }

    /// <summary>A snapshot whose only content is one step, for pinning single cells of the step table.</summary>
    private static LiveMetricsSnapshot StepSnapshot(int okCount, int failedCount, double requestsPerSecond = 0)
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "S",
            Steps = new[]
            {
                new LiveStepMetrics
                {
                    Name = "S",
                    RequestCountOk = okCount,
                    RequestCountFailed = failedCount,
                    RequestsPerSecond = requestsPerSecond,
                    ResponseTimeMean = TimeSpan.FromMilliseconds(1),
                    ResponseTimePercentile95 = TimeSpan.FromMilliseconds(2)
                }
            }
        };
    }

    /// <summary>A one-second ring sample closed at the given second; its rate is the combined delta.</summary>
    private static LiveMetricsSample Sample(int second, int okDelta, int failedDelta, double percentile95Milliseconds)
    {
        var timestamp = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc).AddSeconds(second);
        return new LiveMetricsSample(timestamp, okDelta, failedDelta, okDelta + failedDelta, TimeSpan.FromMilliseconds(percentile95Milliseconds));
    }

    [Test]
    public async Task Verify_dashboard_golden_frame_at_120x40()
    {
        await Scenario()
            .Step("The frame pads to the window height with the footer on the last row", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine("q quit", 6, lines[39]);
                AssertMaximumWidth(120, lines);
            })
            .Step("The title line carries name, status and the compact logo top-right", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine("Checkout flow  ● Running" + new string(' ', 83) + "  ⚡ TestFuzn", 120, lines[0]);
            })
            .Step("The timing line shows elapsed/planned, progress bar, ETA and sim phase", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine("elapsed 00:02:15 / 00:05:00  █████████░░░░░░░░░░  45%  eta 00:02:45  · sim 1/2: Fixed 50 rps", 92, lines[1]);
                AssertLine(string.Empty, 0, lines[2]);
            })
            .Step("The sparkline panels sit side by side, headed by the current values", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine("╭─ rps 142 " + new string('─', 47) + "╮  ╭─ p95 38 ms " + new string('─', 45) + "╮", 120, lines[3]);
                AssertLine("│ " + new string(' ', 50) + "⣀⣤⣶⣿⣿ │  │ " + new string(' ', 50) + "⣿⣦⣤⣀⣀ │", 120, lines[4]);
            })
            .Step("The requests panel shows the warmup headline, the current rates and the full spread", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine("╭─ Requests " + new string('─', 107) + "╮", 120, lines[6]);
                AssertLine("│ warmup 1200 ok · 3 failed" + new string(' ', 91) + " │", 120, lines[7]);
                AssertLine("│         count  rps    min    mean    p50     p75     p95     p99     max" + new string(' ', 44) + " │", 120, lines[8]);
                AssertLine("│ ok      12480  141  12 ms   38 ms  35 ms   48 ms   72 ms   94 ms  312 ms" + new string(' ', 44) + " │", 120, lines[9]);
                AssertLine("│ failed     32  1.0  88 ms  102 ms  99 ms  110 ms  140 ms  160 ms  201 ms" + new string(' ', 44) + " │", 120, lines[10]);
            })
            .Step("The step table shows counts, current rate, latencies and the fail% mini-bar", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine("│ Add to cart   6252   71  18 ms  40 ms       2  █░░░░ <0.1%" + new string(' ', 58) + " │", 120, lines[14]);
                AssertLine("│ Checkout      6260  9.6  58 ms  90 ms      30  █░░░░ 0.5%" + new string(' ', 59) + " │", 120, lines[15]);
            })
            .Step("The error ticker lists distinct errors with right-aligned counts, newest first", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine("│ 30× Checkout · Connection refused (localhost:7058)" + new string(' ', 66) + " │", 120, lines[18]);
                AssertLine("│  2× Add to cart · Timeout after 30s" + new string(' ', 81) + " │", 120, lines[19]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_dashboard_golden_frame_at_100x30()
    {
        await Scenario()
            .Step("The layout keeps its structure at 100 columns with the logo still shown", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 100, 30, ColorMode.None);

                Assert.HasCount(30, lines);
                AssertLine("Checkout flow  ● Running" + new string(' ', 63) + "  ⚡ TestFuzn", 100, lines[0]);
                AssertLine("╭─ rps 142 " + new string('─', 37) + "╮  ╭─ p95 38 ms " + new string('─', 35) + "╮", 100, lines[3]);
                AssertLine("│         count  rps    min    mean    p50     p75     p95     p99     max" + new string(' ', 24) + " │", 100, lines[8]);
                AssertLine("│ step         count  rps   mean    p95  failed  fail%" + new string(' ', 44) + " │", 100, lines[13]);
                AssertLine("│ Checkout      6260  9.6  58 ms  90 ms      30  █░░░░ 0.5%" + new string(' ', 39) + " │", 100, lines[15]);
                AssertLine("│  2× Add to cart · Timeout after 30s" + new string(' ', 61) + " │", 100, lines[19]);
                AssertLine("q quit", 6, lines[29]);
                AssertMaximumWidth(100, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_dashboard_degrades_at_narrow_widths()
    {
        await Scenario()
            .Step("Below 80 columns the logo is dropped while the sparklines and the full spread remain", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 78, 24, ColorMode.None);

                Assert.HasCount(24, lines);
                AssertLine("Checkout flow  ● Running", 24, lines[0]);
                AssertLine("elapsed 00:02:15 / 00:05:00  █████████░░░░░░░░░░  45%  eta 00:02:45", 67, lines[1]);
                AssertLine("╭─ rps 142 " + new string('─', 26) + "╮  ╭─ p95 38 ms " + new string('─', 24) + "╮", 78, lines[3]);
                AssertLine("│         count  rps    min    mean    p50     p75     p95     p99     max" + new string(' ', 2) + " │", 78, lines[8]);
                AssertLine("q quit", 6, lines[23]);
                AssertMaximumWidth(78, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("⚡", line.Text);
            })
            .Step("Below 60 columns the sparkline panels are dropped and the requests table narrows", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 56, 24, ColorMode.None);

                Assert.HasCount(24, lines);
                AssertLine("│         count  rps    mean    p50     p95     max    │", 56, lines[5]);
                AssertLine("│ ok      12480  141   38 ms  35 ms   72 ms  312 ms    │", 56, lines[6]);
                AssertLine("q quit", 6, lines[23]);
                AssertMaximumWidth(56, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain("╭─ rps", line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_requests_table_narrows_columns_by_content_width()
    {
        await Scenario()
            .Step("Wide enough for the full spread, every hour-class value renders whole", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, 120, 0, ColorMode.None);

                AssertLine("│             count      rps         min        mean         p50         p75         p95         p99         max       │", 120, lines[7]);
                AssertLine("│ ok      987654321  9999999  3600000 ms  7200000 ms  5400000 ms  6000000 ms  7200000 ms  8000000 ms  9000000 ms       │", 120, lines[8]);
            })
            .Step("At 78 columns min, p75 and p99 are dropped so the remaining numbers fit exactly", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, 78, 0, ColorMode.None);

                AssertLine("│             count      rps        mean         p50         p95         max │", 78, lines[7]);
                AssertLine("│ ok      987654321  9999999  7200000 ms  5400000 ms  7200000 ms  9000000 ms │", 78, lines[8]);
                AssertLine("│ failed  123456789      1.0   500000 ms   400000 ms   800000 ms   950000 ms │", 78, lines[9]);
            })
            .Step("One column narrower, p50 and max go too rather than any number truncating", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, 77, 0, ColorMode.None);

                Assert.Contains("p95", lines[7].Text);
                Assert.DoesNotContain("p50", lines[7].Text);
                Assert.DoesNotContain("max", lines[7].Text);
                Assert.Contains("987654321  9999999  7200000 ms  7200000 ms", lines[8].Text);
            })
            .Step("At 56 columns count, rps, mean and p95 remain, all whole", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, 56, 0, ColorMode.None);

                AssertLine("│             count      rps        mean         p95   │", 56, lines[4]);
                AssertLine("│ ok      987654321  9999999  7200000 ms  7200000 ms   │", 56, lines[5]);
                AssertLine("│ failed  123456789      1.0   500000 ms   800000 ms   │", 56, lines[6]);
            })
            .Step("From 56 columns up no numeric cell of the requests rows is ever truncated", context =>
            {
                for (var width = 56; width <= 130; width++)
                {
                    var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, width, 0, ColorMode.None);
                    var requestsRowCount = 0;
                    foreach (var line in lines)
                    {
                        if (!line.Text.StartsWith("│ ok ", StringComparison.Ordinal) && !line.Text.StartsWith("│ failed ", StringComparison.Ordinal))
                            continue;

                        requestsRowCount++;
                        Assert.DoesNotContain("…", line.Text, $"Truncated requests row at width {width}: {line.Text}");
                    }

                    Assert.AreEqual(2, requestsRowCount, $"Expected the ok and failed rows at width {width}");
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_live_rates_share_one_measure()
    {
        await Scenario()
            .Step("The sparkline header, the requests rows and the step row all show the current interval", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RampSnapshot() }, 100, 0, ColorMode.None);

                AssertLine("╭─ rps 50 " + new string('─', 38) + "╮  ╭─ p95 40 ms " + new string('─', 35) + "╮", 100, lines[3]);
                AssertLine("│ ok       1800   45  10 ms  20 ms  20 ms  25 ms  40 ms  60 ms  90 ms" + new string(' ', 29) + " │", 100, lines[8]);
                AssertLine("│ failed    180  5.0  15 ms  45 ms  45 ms  55 ms  70 ms  80 ms  95 ms" + new string(' ', 29) + " │", 100, lines[9]);
                AssertLine("│ Add to cart   1980   50  20 ms  40 ms     180  █░░░░ 9.1%" + new string(' ', 39) + " │", 100, lines[13]);
            })
            .Step("An interval without requests shows a zero rate, and no sample yet shows no data", context =>
            {
                var idle = new LiveMetricsSnapshot { ScenarioName = "Idle", Samples = new[] { Sample(1, 0, 0, 0) }, RequestsPerSecondSeries = new double[] { 0 } };
                var fresh = new LiveMetricsSnapshot { ScenarioName = "Fresh" };

                var idleLines = LiveDashboardLayout.Render(new[] { idle }, 100, 0, ColorMode.None);
                var freshLines = LiveDashboardLayout.Render(new[] { fresh }, 100, 0, ColorMode.None);

                Assert.StartsWith("╭─ rps 0.0 ", idleLines[3].Text);
                AssertLine("│ ok          0  0.0  N/A   N/A  N/A  N/A  N/A  N/A  N/A" + new string(' ', 42) + " │", 100, idleLines[8]);
                Assert.StartsWith("╭─ rps — ", freshLines[3].Text);
                AssertLine("│ ok          0    —  N/A   N/A  N/A  N/A  N/A  N/A  N/A" + new string(' ', 42) + " │", 100, freshLines[8]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_multi_scenario_sections_stack_in_order()
    {
        await Scenario()
            .Step("Sections stack in snapshot order with a blank separator and one logo", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, 100, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine(string.Empty, 0, lines[21]);
                AssertLine("Browse catalog  ● Running", 25, lines[22]);
                AssertLine("elapsed 00:00:42  · warmup: Fixed 50 rps", 40, lines[23]);
                AssertLine("╭─ rps — " + new string('─', 39) + "╮  ╭─ p95 — " + new string('─', 39) + "╮", 100, lines[25]);
                AssertLine("│ ok          0    —  N/A   N/A  N/A  N/A  N/A  N/A  N/A" + new string(' ', 42) + " │", 100, lines[30]);
                AssertLine("q quit", 6, lines[39]);
                AssertMaximumWidth(100, lines);

                var logoLineCount = 0;
                foreach (var line in lines)
                {
                    if (line.Text.Contains("⚡"))
                        logoLineCount++;
                }

                Assert.AreEqual(1, logoLineCount);
            })
            .Step("A frame taller than the window keeps the footer on the last row and cuts the content", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, 120, 24, ColorMode.None);

                Assert.HasCount(24, lines);
                AssertLine("Browse catalog  ● Running", 25, lines[22]);
                AssertLine("q quit", 6, lines[23]);

                foreach (var line in lines)
                    Assert.DoesNotContain("warmup: Fixed 50 rps", line.Text);
            })
            .Step("A one-row window is just the footer", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 1, ColorMode.None);

                Assert.HasCount(1, lines);
                AssertLine("q quit", 6, lines[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_indeterminate_plan_and_status_detail()
    {
        await Scenario()
            .Step("An indeterminate plan renders an elapsed-only header with no progress bar", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, 100, 0, ColorMode.None);

                AssertLine("elapsed 00:00:42  · warmup: Fixed 50 rps", 40, lines[1]);
                Assert.DoesNotContain("█", lines[1].Text);
                Assert.DoesNotContain("░", lines[1].Text);
                Assert.DoesNotContain("eta", lines[1].Text);
            })
            .Step("Measurement-segment progress shows the bar and ETA without a planned total", context =>
            {
                var snapshot = new LiveMetricsSnapshot
                {
                    ScenarioName = "Browse catalog",
                    PhaseLabel = "sim 1/1: Fixed 50 rps",
                    Duration = TimeSpan.FromSeconds(42),
                    ProgressFraction = 0.3,
                    EstimatedTimeRemaining = TimeSpan.FromSeconds(84)
                };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                AssertLine("elapsed 00:00:42  ██████░░░░░░░░░░░░░  30%  eta 00:01:24  · sim 1/1: Fixed 50 rps", 81, lines[1]);
            })
            .Step("A failed scenario shows its assert reason as a styled header line", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { FailedSnapshot() }, 100, 0, ColorMode.None);

                Assert.HasCount(13, lines);
                Assert.Contains("● Failed", lines[0].Text);
                AssertLine("elapsed 00:05:00  · completed", 29, lines[1]);
                AssertLine("✗ Assert.IsLessThan failed. p95 too high: 240 ms", 48, lines[2]);
                AssertLine("q quit", 6, lines[12]);
            })
            .Step("The assert reason line renders red in TrueColor", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { FailedSnapshot() }, 100, 0, ColorMode.TrueColor);

                AssertLine("[38;5;9m✗ Assert.IsLessThan failed. p95 too high: 240 ms[0m", 48, lines[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_timing_line_drops_tail_pieces_whole()
    {
        await Scenario()
            .Step("At 40 columns the ETA does not fit whole, so it is dropped rather than cut", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 40, 0, ColorMode.None);

                AssertLine("elapsed 00:02:15 / 00:05:00", 27, lines[1]);
            })
            .Step("One column more and the ETA appears whole", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 41, 0, ColorMode.None);

                AssertLine("elapsed 00:02:15 / 00:05:00  eta 00:02:45", 41, lines[1]);
            })
            .Step("The phase label is the first piece to go, one column short of fitting", context =>
            {
                var shortLines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 91, 0, ColorMode.None);
                var fullLines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 92, 0, ColorMode.None);

                AssertLine("elapsed 00:02:15 / 00:05:00  █████████░░░░░░░░░░  45%  eta 00:02:45", 67, shortLines[1]);
                AssertLine("elapsed 00:02:15 / 00:05:00  █████████░░░░░░░░░░  45%  eta 00:02:45  · sim 1/2: Fixed 50 rps", 92, fullLines[1]);
            })
            .Step("The planned total is dropped whole too, leaving the bare elapsed time", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 26, 0, ColorMode.None);

                AssertLine("elapsed 00:02:15", 16, lines[1]);
            })
            .Step("A phase label that does not fit whole is dropped, never truncated", context =>
            {
                var shortLines = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, 39, 0, ColorMode.None);
                var fullLines = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, 40, 0, ColorMode.None);

                AssertLine("elapsed 00:00:42", 16, shortLines[1]);
                AssertLine("elapsed 00:00:42  · warmup: Fixed 50 rps", 40, fullLines[1]);
            })
            .Step("Negative durations display as zero instead of negative clock fields", context =>
            {
                var snapshot = new LiveMetricsSnapshot
                {
                    ScenarioName = "Skewed",
                    PhaseLabel = "p",
                    Duration = TimeSpan.FromSeconds(-95),
                    PlannedDuration = TimeSpan.MinValue,
                    EstimatedTimeRemaining = TimeSpan.FromHours(-3)
                };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                AssertLine("elapsed 00:00:00 / 00:00:00  eta 00:00:00  · p", 46, lines[1]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_no_data_and_out_of_range_values_render_safely()
    {
        await Scenario()
            .Step("A p95 series value no TimeSpan can hold shows no data instead of throwing", context =>
            {
                var justOutOfRange = Math.BitIncrement(TimeSpan.MaxValue.TotalMilliseconds);
                foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, justOutOfRange, -justOutOfRange, 1e15 })
                {
                    var snapshot = new LiveMetricsSnapshot { ScenarioName = "p95", ResponseTimePercentile95Series = new[] { value } };

                    var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                    Assert.Contains("╭─ p95 — ", lines[3].Text, $"Unexpected p95 header for {value}");
                }
            })
            .Step("A representable p95 series value, up to the largest TimeSpan, renders through the shared formatter", context =>
            {
                var hours = new LiveMetricsSnapshot { ScenarioName = "p95", ResponseTimePercentile95Series = new double[] { 7200000 } };
                var largest = new LiveMetricsSnapshot { ScenarioName = "p95", ResponseTimePercentile95Series = new[] { TimeSpan.MaxValue.TotalMilliseconds } };
                var negative = new LiveMetricsSnapshot { ScenarioName = "p95", ResponseTimePercentile95Series = new double[] { -5 } };

                Assert.Contains("╭─ p95 7200000 ms ", LiveDashboardLayout.Render(new[] { hours }, 100, 0, ColorMode.None)[3].Text);
                Assert.Contains("╭─ p95 922337203685477 ms ", LiveDashboardLayout.Render(new[] { largest }, 100, 0, ColorMode.None)[3].Text);
                Assert.Contains("╭─ p95 N/A ", LiveDashboardLayout.Render(new[] { negative }, 100, 0, ColorMode.None)[3].Text);
            })
            .Step("A rate that is not finite shows no data in the header and the step table alike", context =>
            {
                var snapshot = new LiveMetricsSnapshot
                {
                    ScenarioName = "S",
                    RequestsPerSecondSeries = new[] { double.NaN },
                    Steps = new[]
                    {
                        new LiveStepMetrics { Name = "S", RequestCountOk = 5, RequestsPerSecond = double.PositiveInfinity, ResponseTimeMean = TimeSpan.FromMilliseconds(1), ResponseTimePercentile95 = TimeSpan.FromMilliseconds(2) }
                    }
                };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                Assert.StartsWith("╭─ rps — ", lines[3].Text);
                AssertLine("│ S         5    —  1 ms  2 ms       0  ░░░░░ 0%" + new string(' ', 50) + " │", 100, lines[13]);
            })
            .Step("Step counts that do not add up render a clamped bar at every width instead of throwing", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { StepSnapshot(-1, 5) }, 100, 0, ColorMode.None);
                Assert.Contains("█████ 100%", lines[13].Text);

                foreach (var snapshot in new[] { StepSnapshot(-1, 5), StepSnapshot(5, -1), StepSnapshot(-5, 5), StepSnapshot(int.MaxValue, int.MaxValue) })
                {
                    for (var width = 1; width <= 130; width++)
                        AssertMaximumWidth(width, LiveDashboardLayout.Render(new[] { snapshot }, width, 0, ColorMode.None));
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_counts_and_percentages_stay_honest()
    {
        await Scenario()
            .Step("Two counts of two billion sum without wrapping in the step table and the warmup line", context =>
            {
                var snapshot = new LiveMetricsSnapshot
                {
                    ScenarioName = "S",
                    WarmupRequestCountOk = 2000000000,
                    WarmupRequestCountFailed = 2000000000,
                    Steps = new[] { new LiveStepMetrics { Name = "S", RequestCountOk = 2000000000, RequestCountFailed = 2000000000 } }
                };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                AssertLine("│ warmup 2000000000 ok · 2000000000 failed" + new string(' ', 56) + " │", 100, lines[7]);
                AssertLine("│ S     4000000000  0.0   N/A  N/A  2000000000  ███░░ 50%" + new string(' ', 41) + " │", 100, lines[14]);
            })
            .Step("A failure share below 100% never reads as 100%, mirroring the <0.1% floor", context =>
            {
                Assert.Contains("█████ >99.9%", LiveDashboardLayout.Render(new[] { StepSnapshot(3, 9997) }, 100, 0, ColorMode.None)[13].Text);
                Assert.Contains("█████ 99.9%", LiveDashboardLayout.Render(new[] { StepSnapshot(1, 999) }, 100, 0, ColorMode.None)[13].Text);
                Assert.Contains("█████ 99.6%", LiveDashboardLayout.Render(new[] { StepSnapshot(4, 996) }, 100, 0, ColorMode.None)[13].Text);
                Assert.Contains("█████ 99.5%", LiveDashboardLayout.Render(new[] { StepSnapshot(5, 995) }, 100, 0, ColorMode.None)[13].Text);
                Assert.Contains("█████ 99%", LiveDashboardLayout.Render(new[] { StepSnapshot(6, 994) }, 100, 0, ColorMode.None)[13].Text);
                Assert.Contains("█████ 100%", LiveDashboardLayout.Render(new[] { StepSnapshot(0, 5) }, 100, 0, ColorMode.None)[13].Text);
                Assert.Contains("█░░░░ <0.1%", LiveDashboardLayout.Render(new[] { StepSnapshot(9999, 1) }, 100, 0, ColorMode.None)[13].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_status_badges_and_color_modes()
    {
        await Scenario()
            .Step("A completed passed scenario shows the Passed badge", context =>
            {
                var snapshot = new LiveMetricsSnapshot { ScenarioName = "S", PhaseLabel = "completed", IsCompleted = true, Status = TestStatus.Passed };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 40, 0, ColorMode.None);

                AssertLine("S  ● Passed", 11, lines[0]);
            })
            .Step("A skipped scenario shows the Skipped badge", context =>
            {
                var snapshot = new LiveMetricsSnapshot { ScenarioName = "S", PhaseLabel = "completed", IsCompleted = true, Status = TestStatus.Skipped };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 40, 0, ColorMode.None);

                AssertLine("S  ● Skipped", 12, lines[0]);
            })
            .Step("Color mode None emits zero escape bytes across the whole frame", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), FailedSnapshot() }, 120, 40, ColorMode.None);

                foreach (var line in lines)
                    Assert.DoesNotContain("", line.Text);
            })
            .Step("TrueColor styles the frame with the warm accent on the panel headers", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.TrueColor);

                Assert.Contains("[1;38;2;255;157;61mRequests[0m", lines[6].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_dashboard_width_invariant_and_determinism()
    {
        await Scenario()
            .Step("No line exceeds the width at any width from 1 to 130 with either glyph set", context =>
            {
                var snapshots = new[] { RichSnapshot(), FailedSnapshot(), IndeterminateSnapshot(), HugeSnapshot(), RampSnapshot() };
                foreach (var glyphSet in new[] { SparklineGlyphSet.Braille, SparklineGlyphSet.Blocks })
                {
                    for (var width = 1; width <= 130; width++)
                    {
                        var lines = LiveDashboardLayout.Render(snapshots, width, 0, ColorMode.TrueColor, glyphSet);
                        AssertMaximumWidth(width, lines);
                    }
                }
            })
            .Step("The block glyph set passes through to the sparkline panels", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None, SparklineGlyphSet.Blocks);

                AssertLine("│ " + new string(' ', 45) + "▁▂▃▄▅▆▇▇██ │  │ " + new string(' ', 45) + "█▇▅▄▃▃▂▂▁▁ │", 120, lines[4]);
            })
            .Step("Identical inputs render an identical frame", context =>
            {
                var first = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.TrueColor);
                var second = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.TrueColor);

                Assert.HasCount(first.Count, second);
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.AreEqual(first[index].Text, second[index].Text, $"Text mismatch at row {index}");
                    Assert.AreEqual(first[index].Width, second[index].Width, $"Width mismatch at row {index}");
                }
            })
            .Step("A width below 1 renders nothing and null snapshots are rejected", context =>
            {
                Assert.IsEmpty(LiveDashboardLayout.Render(new[] { RichSnapshot() }, 0, 40, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => LiveDashboardLayout.Render(null!, 120, 40, ColorMode.None));
            })
            .Step("No snapshots renders just the footer padded to the window height", context =>
            {
                var lines = LiveDashboardLayout.Render(Array.Empty<LiveMetricsSnapshot>(), 40, 5, ColorMode.None);

                Assert.HasCount(5, lines);
                AssertLine("q quit", 6, lines[4]);
            })
            .Run();
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
