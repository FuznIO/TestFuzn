using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins the plain stats lines the redirected-output live view writes, from synthetic snapshots:
/// the exact text of the stats, phase, failure and final lines in the dashboard's number
/// formats, the no-data and outcome vocabulary, that phase and failure lines are written on
/// transitions only and per scenario, and that every write is one sanitized line — no escape
/// byte, no embedded line break, exactly one line terminator.
/// </summary>
[TestClass]
public class LiveStatsWriterTests : Test
{
    private const string ScenarioName = "Checkout flow";

    /// <summary>
    /// A snapshot with the fields the lines read. A current rate becomes a two-entry series
    /// with the rate last, so a line reading the newest entry is told apart from one reading
    /// the first.
    /// </summary>
    private static LiveMetricsSnapshot Snapshot(
        string scenarioName = ScenarioName,
        string phaseLabel = "init",
        int elapsedSeconds = 0,
        int? plannedSeconds = null,
        int ok = 0,
        int failed = 0,
        double? currentRate = null,
        double percentile95Ms = 0,
        TestStatus status = TestStatus.Passed,
        string? statusDetail = null,
        bool isCompleted = false,
        IReadOnlyList<ThresholdResult>? thresholdResults = null,
        IReadOnlyList<LiveThreshold>? liveThresholds = null)
    {
        var requestsPerSecondSeries = Array.Empty<double>();
        if (currentRate != null)
            requestsPerSecondSeries = new[] { 3.0, currentRate.Value };

        TimeSpan? plannedDuration = null;
        if (plannedSeconds != null)
            plannedDuration = TimeSpan.FromSeconds(plannedSeconds.Value);

        var verdict = Array.Empty<ThresholdResult>() as IReadOnlyList<ThresholdResult>;
        if (thresholdResults != null)
            verdict = thresholdResults;

        var liveReadings = Array.Empty<LiveThreshold>() as IReadOnlyList<LiveThreshold>;
        if (liveThresholds != null)
            liveReadings = liveThresholds;

        return new LiveMetricsSnapshot
        {
            ScenarioName = scenarioName,
            Phase = LoadTestPhase.Measurement,
            PhaseLabel = phaseLabel,
            Duration = TimeSpan.FromSeconds(elapsedSeconds),
            PlannedDuration = plannedDuration,
            RequestCountOk = ok,
            RequestCountFailed = failed,
            Ok = new LiveStats { RequestCount = ok, ResponseTimePercentile95 = TimeSpan.FromMilliseconds(percentile95Ms) },
            Failed = new LiveStats { RequestCount = failed },
            RequestsPerSecondSeries = requestsPerSecondSeries,
            Status = status,
            StatusDetail = statusDetail,
            IsCompleted = isCompleted,
            Thresholds = liveReadings,
            ThresholdResults = verdict
        };
    }

    /// <summary>
    /// A verdict of <paramref name="passedCount"/> held thresholds followed by
    /// <paramref name="breachedCount"/> violated ones — the counts are all the final line reads.
    /// </summary>
    private static IReadOnlyList<ThresholdResult> Verdict(int passedCount, int breachedCount)
    {
        var threshold = new Threshold(ThresholdMetric.ErrorRate, 0.01, ThresholdComparison.LessThanOrEqualTo);
        var results = new List<ThresholdResult>();
        for (var index = 0; index < passedCount; index++)
            results.Add(new ThresholdResult(threshold, 0.0, true));
        for (var index = 0; index < breachedCount; index++)
            results.Add(new ThresholdResult(threshold, 0.5, false));

        return results;
    }

    /// <summary>
    /// The live per-interval reading of one declared threshold, breaching — what a stopped run's
    /// final snapshot still carries where its verdict, never taken, is empty.
    /// </summary>
    private static IReadOnlyList<LiveThreshold> BreachingLiveReading()
    {
        var threshold = new Threshold(ThresholdMetric.ErrorRate, 0.01, ThresholdComparison.LessThanOrEqualTo);
        return new[] { new LiveThreshold(threshold, 0.5, ThresholdState.Breached, TimeSpan.FromSeconds(3)) };
    }

    [Test]
    public async Task Verify_stats_line_carries_every_field_in_the_dashboard_formats()
    {
        await Scenario()
            .Step("Before any data: elapsed only, zero totals, and N/A for the rate and the p95 — never a fake zero", context =>
            {
                Assert.AreEqual("[Checkout flow] elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A", LiveStatsWriter.FormatStatsLine(Snapshot()));
            })
            .Step("With a determinate plan and a closed interval: planned after elapsed, totals from the measurement counts, the newest rate as a whole number from 10 up, the cumulative p95 in the summary's format", context =>
            {
                var snapshot = Snapshot(phaseLabel: "Fixed Load 100 rps", elapsedSeconds: 3909, plannedSeconds: 600, ok: 1230, failed: 4, currentRate: 12.4, percentile95Ms: 250);
                Assert.AreEqual("[Checkout flow] elapsed 01:05:09 / 00:10:00  total 1234  ok 1230  failed 4  rps 12  p95 250 ms", LiveStatsWriter.FormatStatsLine(snapshot));
            })
            .Step("A rate below 10 keeps one decimal, a sub-millisecond p95 reads as under a millisecond, and hours are not wrapped at a day", context =>
            {
                var snapshot = Snapshot(elapsedSeconds: 25 * 3600, ok: 2, currentRate: 0.04, percentile95Ms: 0.4);
                Assert.AreEqual("[Checkout flow] elapsed 25:00:00  total 2  ok 2  failed 0  rps 0.0  p95 <1 ms", LiveStatsWriter.FormatStatsLine(snapshot));
                Assert.AreEqual("[Checkout flow] elapsed 00:00:05  total 0  ok 0  failed 0  rps 5.0  p95 N/A", LiveStatsWriter.FormatStatsLine(Snapshot(elapsedSeconds: 5, currentRate: 5)));
            })
            .Step("A rate that is not finite is no data", context =>
            {
                Assert.EndsWith("rps N/A  p95 N/A", LiveStatsWriter.FormatStatsLine(Snapshot(currentRate: double.NaN)));
                Assert.EndsWith("rps N/A  p95 N/A", LiveStatsWriter.FormatStatsLine(Snapshot(currentRate: double.PositiveInfinity)));
            })
            .Run();
    }

    [Test]
    public async Task Verify_phase_and_failure_lines_are_written_on_transitions_only_and_per_scenario()
    {
        await Scenario()
            .Step("Every scenario's first sample writes its phase line; a sample with the same labels writes stats lines only; a label change writes that scenario's phase line only", context =>
            {
                var writer = new FakeTerminalWriter();
                var log = new LiveStatsWriter(writer, 2);

                log.WriteSample(new[] { Snapshot(), Snapshot(scenarioName: "Search flow") });
                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] phase: init",
                    "[Checkout flow] elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A",
                    "[Search flow] phase: init",
                    "[Search flow] elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A"
                }, Lines(writer));

                writer.ClearWrites();
                log.WriteSample(new[] { Snapshot(elapsedSeconds: 1), Snapshot(scenarioName: "Search flow", elapsedSeconds: 1) });
                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] elapsed 00:00:01  total 0  ok 0  failed 0  rps N/A  p95 N/A",
                    "[Search flow] elapsed 00:00:01  total 0  ok 0  failed 0  rps N/A  p95 N/A"
                }, Lines(writer));

                writer.ClearWrites();
                log.WriteSample(new[] { Snapshot(phaseLabel: "warmup: Fixed Load 5 rps", elapsedSeconds: 2, plannedSeconds: 12), Snapshot(scenarioName: "Search flow", elapsedSeconds: 2) });
                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] phase: warmup: Fixed Load 5 rps",
                    "[Checkout flow] elapsed 00:00:02 / 00:00:12  total 0  ok 0  failed 0  rps N/A  p95 N/A",
                    "[Search flow] elapsed 00:00:02  total 0  ok 0  failed 0  rps N/A  p95 N/A"
                }, Lines(writer));
            })
            .Step("An assert failure's reason is written once, when it appears, before that sample's stats line", context =>
            {
                var writer = new FakeTerminalWriter();
                var log = new LiveStatsWriter(writer, 1);
                log.WriteSample(new[] { Snapshot(phaseLabel: "Fixed Load 5 rps", ok: 2, percentile95Ms: 10) });

                writer.ClearWrites();
                log.WriteSample(new[] { Snapshot(phaseLabel: "Fixed Load 5 rps", elapsedSeconds: 1, ok: 2, percentile95Ms: 10, status: TestStatus.Failed, statusDetail: "Assert while running failed: p95 above 5 ms") });
                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] failed: Assert while running failed: p95 above 5 ms",
                    "[Checkout flow] elapsed 00:00:01  total 2  ok 2  failed 0  rps N/A  p95 10 ms"
                }, Lines(writer));

                writer.ClearWrites();
                log.WriteSample(new[] { Snapshot(phaseLabel: "Fixed Load 5 rps", elapsedSeconds: 2, ok: 2, percentile95Ms: 10, status: TestStatus.Failed, statusDetail: "Assert while running failed: p95 above 5 ms") });
                CollectionAssert.AreEqual(new[] { "[Checkout flow] elapsed 00:00:02  total 2  ok 2  failed 0  rps N/A  p95 10 ms" }, Lines(writer));
            })
            .Step("An empty phase label is never written as a phase line; the label that follows it is", context =>
            {
                var writer = new FakeTerminalWriter();
                var log = new LiveStatsWriter(writer, 1);

                log.WriteSample(new[] { Snapshot(phaseLabel: string.Empty) });
                CollectionAssert.AreEqual(new[] { "[Checkout flow] elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A" }, Lines(writer));

                writer.ClearWrites();
                log.WriteSample(new[] { Snapshot(phaseLabel: "init", elapsedSeconds: 1) });
                Assert.AreEqual("[Checkout flow] phase: init", Lines(writer)[0]);
            })
            .Step("More snapshots than the log was created for, or none at all, is a caller error", context =>
            {
                var log = new LiveStatsWriter(new FakeTerminalWriter(), 1);
                Assert.ThrowsExactly<ArgumentException>(() => log.WriteSample(new[] { Snapshot(), Snapshot(scenarioName: "Search flow") }));
                Assert.ThrowsExactly<ArgumentNullException>(() => log.WriteSample(null!));
                Assert.ThrowsExactly<ArgumentNullException>(() => log.WriteFinal(null!, isStopped: false));
                Assert.ThrowsExactly<ArgumentNullException>(() => new LiveStatsWriter(null!, 1));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LiveStatsWriter(new FakeTerminalWriter(), -1));
            })
            .Run();
    }

    [Test]
    public async Task Verify_final_line_reads_the_outcome_in_the_writers_own_vocabulary()
    {
        await Scenario()
            .Step("A completed run: completed, the frozen duration, the totals and the cumulative p95 — no rate", context =>
            {
                var snapshot = Snapshot(phaseLabel: "completed", elapsedSeconds: 12, plannedSeconds: 10, ok: 8, currentRate: 0, percentile95Ms: 10, isCompleted: true);
                Assert.AreEqual("[Checkout flow] completed  elapsed 00:00:12  total 8  ok 8  failed 0  p95 10 ms", LiveStatsWriter.FormatFinalLine(snapshot, isStopped: false));
            })
            .Step("A stopped run reads stopped, and so does a scenario whose measurement never completed — the run ended before it", context =>
            {
                var completed = Snapshot(phaseLabel: "completed", elapsedSeconds: 3, isCompleted: true);
                Assert.AreEqual("[Checkout flow] stopped  elapsed 00:00:03  total 0  ok 0  failed 0  p95 N/A", LiveStatsWriter.FormatFinalLine(completed, isStopped: true));

                var neverCompleted = Snapshot(phaseLabel: "init", elapsedSeconds: 2);
                Assert.AreEqual("[Checkout flow] stopped  elapsed 00:00:02  total 0  ok 0  failed 0  p95 N/A", LiveStatsWriter.FormatFinalLine(neverCompleted, isStopped: false));
            })
            .Step("A failed scenario reads failed with its reason, and failed wins over the stop the assert caused; a skipped scenario reads skipped", context =>
            {
                var failed = Snapshot(phaseLabel: "completed", elapsedSeconds: 6, ok: 2, percentile95Ms: 10, status: TestStatus.Failed, statusDetail: "Assert while running failed: p95 above 5 ms", isCompleted: true);
                Assert.AreEqual("[Checkout flow] failed  elapsed 00:00:06  total 2  ok 2  failed 0  p95 10 ms  reason: Assert while running failed: p95 above 5 ms", LiveStatsWriter.FormatFinalLine(failed, isStopped: true));

                var skipped = Snapshot(phaseLabel: "completed", elapsedSeconds: 1, status: TestStatus.Skipped, isCompleted: true);
                Assert.AreEqual("[Checkout flow] skipped  elapsed 00:00:01  total 0  ok 0  failed 0  p95 N/A", LiveStatsWriter.FormatFinalLine(skipped, isStopped: false));
            })
            .Step("WriteFinal writes one final line per scenario, in order", context =>
            {
                var writer = new FakeTerminalWriter();
                var log = new LiveStatsWriter(writer, 2);
                log.WriteFinal(new[] { Snapshot(elapsedSeconds: 12, ok: 3, percentile95Ms: 10, isCompleted: true), Snapshot(scenarioName: "Search flow", elapsedSeconds: 12, ok: 1, percentile95Ms: 10, isCompleted: true) }, isStopped: false);
                CollectionAssert.AreEqual(new[]
                {
                    "[Checkout flow] completed  elapsed 00:00:12  total 3  ok 3  failed 0  p95 10 ms",
                    "[Search flow] completed  elapsed 00:00:12  total 1  ok 1  failed 0  p95 10 ms"
                }, Lines(writer));
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_final_line_reports_the_threshold_verdict()
    {
        await Scenario()
            .Step("A verdict every threshold held reads ok, after the p95 and before any reason", context =>
            {
                var snapshot = Snapshot(phaseLabel: "completed", elapsedSeconds: 20, ok: 500, percentile95Ms: 320, isCompleted: true, thresholdResults: Verdict(passedCount: 2, breachedCount: 0));
                Assert.AreEqual("[Checkout flow] completed  elapsed 00:00:20  total 500  ok 500  failed 0  p95 320 ms  thresholds: ok", LiveStatsWriter.FormatFinalLine(snapshot, isStopped: false));
            })
            .Step("A violated verdict reads breached with the count of the violations, not of the thresholds", context =>
            {
                var snapshot = Snapshot(phaseLabel: "completed", elapsedSeconds: 20, ok: 490, failed: 10, percentile95Ms: 320, status: TestStatus.Failed, statusDetail: "Threshold violated: error rate 2.04 % > 2 %", isCompleted: true, thresholdResults: Verdict(passedCount: 2, breachedCount: 1));
                Assert.AreEqual("[Checkout flow] failed  elapsed 00:00:20  total 500  ok 490  failed 10  p95 320 ms  thresholds: breached (1)  reason: Threshold violated: error rate 2.04 % > 2 %", LiveStatsWriter.FormatFinalLine(snapshot, isStopped: false));

                var bothBreached = Snapshot(phaseLabel: "completed", elapsedSeconds: 20, ok: 490, failed: 10, percentile95Ms: 320, isCompleted: true, thresholdResults: Verdict(passedCount: 0, breachedCount: 2));
                Assert.EndsWith("thresholds: breached (2)", LiveStatsWriter.FormatFinalLine(bothBreached, isStopped: false));
            })
            .Step("A scenario that declared no threshold has no field at all", context =>
            {
                var noThresholds = Snapshot(phaseLabel: "completed", elapsedSeconds: 20, ok: 500, percentile95Ms: 320, isCompleted: true);
                Assert.AreEqual("[Checkout flow] completed  elapsed 00:00:20  total 500  ok 500  failed 0  p95 320 ms", LiveStatsWriter.FormatFinalLine(noThresholds, isStopped: false));
            })
            .Step("Nor has a stopped run that did declare thresholds: it carries their live readings but no verdict, and the field reports the verdict alone — a breaching reading on a run nobody judged must read as neither ok nor breached", context =>
            {
                // The state the collector leaves behind on a stop: the declared thresholds' last
                // per-interval readings still published, breaching one included, while the
                // cumulative verdict — taken where the AssertWhenDone callback runs, which a
                // stopped run never reaches — stays empty.
                var stopped = Snapshot(phaseLabel: "completed", elapsedSeconds: 20, ok: 500, percentile95Ms: 320, isCompleted: true, liveThresholds: BreachingLiveReading());
                Assert.IsNotEmpty(stopped.Thresholds);
                Assert.IsEmpty(stopped.ThresholdResults);

                Assert.AreEqual("[Checkout flow] stopped  elapsed 00:00:20  total 500  ok 500  failed 0  p95 320 ms", LiveStatsWriter.FormatFinalLine(stopped, isStopped: true));
            })
            .Step("The sample lines never carry the verdict: it exists only once the run has completed", context =>
            {
                var snapshot = Snapshot(phaseLabel: "completed", elapsedSeconds: 20, ok: 500, percentile95Ms: 320, thresholdResults: Verdict(passedCount: 1, breachedCount: 1));
                Assert.DoesNotContain("thresholds", LiveStatsWriter.FormatStatsLine(snapshot));
            })
            .Run();
    }

    [Test]
    public async Task Verify_every_write_is_one_sanitized_line()
    {
        await Scenario()
            .Step("Control characters in a scenario name or an assert message become spaces, so a line can neither break nor carry an escape sequence, and every write ends in exactly one line terminator", context =>
            {
                var writer = new FakeTerminalWriter();
                var log = new LiveStatsWriter(writer, 1);
                var snapshot = Snapshot(scenarioName: "Check\u001b[2Jout\r\nflow\tA", phaseLabel: "init", status: TestStatus.Failed, statusDetail: "line one\nline two");

                log.WriteSample(new[] { snapshot });
                log.WriteFinal(new[] { snapshot }, isStopped: false);

                CollectionAssert.AreEqual(new[]
                {
                    "[Check [2Jout flow A] phase: init",
                    "[Check [2Jout flow A] failed: line one line two",
                    "[Check [2Jout flow A] elapsed 00:00:00  total 0  ok 0  failed 0  rps N/A  p95 N/A",
                    "[Check [2Jout flow A] failed  elapsed 00:00:00  total 0  ok 0  failed 0  p95 N/A  reason: line one line two"
                }, Lines(writer));
                Assert.HasCount(4, writer.Writes);
                Assert.AreEqual(0, writer.WindowWidthReadCount);
                Assert.AreEqual(0, writer.WindowHeightReadCount);
            })
            .Run();
    }

    /// <summary>
    /// The writer's writes as lines: each one plain — no escape byte, ending in exactly one
    /// line terminator with no other line break — and returned without the terminator.
    /// </summary>
    private static List<string> Lines(FakeTerminalWriter writer)
    {
        var lines = new List<string>();
        foreach (var write in writer.Writes)
        {
            Assert.DoesNotContain("\u001b", write);
            Assert.EndsWith(Environment.NewLine, write);

            var line = write.Substring(0, write.Length - Environment.NewLine.Length);
            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
            lines.Add(line);
        }

        return lines;
    }
}
