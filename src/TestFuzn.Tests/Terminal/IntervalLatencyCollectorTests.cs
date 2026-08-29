using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins the per-interval latency distribution the live dashboard reads:
/// <see cref="StatsCollector.GetIntervalLatency"/> over known recordings (hand-derived bucket
/// counts and percentiles), the force-refresh interval cadence through a REAL in-memory
/// <see cref="ScenarioLoadCollector"/>, per-step intervals independent of the scenario's, and
/// the <see cref="IntervalLatency.Empty"/> and <see cref="LatencyBuckets"/> contracts.
/// Hermetic — no TestWebApp, sink or terminal.
/// </summary>
[TestClass]
public class IntervalLatencyCollectorTests : Test
{
    private const string BrowseStep = "Browse products";
    private const string OrderStep = "Place order";
    private const string ChargeSubStep = "Charge card";

    /// <summary>A collector with interval tracking on, with every (duration, count) pair recorded count times.</summary>
    private static StatsCollector RecordDurations(params (TimeSpan Duration, int Count)[] recordings)
    {
        var collector = new StatsCollector(trackIntervalLatency: true);
        foreach (var recording in recordings)
        {
            for (var index = 0; index < recording.Count; index++)
                collector.Record(recording.Duration, SyntheticLoadSnapshots.BaseTime, SyntheticLoadSnapshots.At(1));
        }

        return collector;
    }

    private static (TimeSpan Duration, int Count) Milliseconds(double milliseconds, int count)
    {
        return (TimeSpan.FromMilliseconds(milliseconds), count);
    }

    /// <summary>A two-step checkout scenario collector in its measurement phase.</summary>
    private static ScenarioLoadCollector CreateMeasuringCollector()
    {
        var scenario = new Scenario("Checkout flow");
        scenario.Steps.Add(new Step { Name = BrowseStep, Id = "browse-products" });
        scenario.Steps.Add(new Step { Name = OrderStep, Id = "place-order" });

        var collector = new ScenarioLoadCollector(scenario);
        collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
        collector.MarkPhaseAsCompleted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
        collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(0));
        return collector;
    }

    /// <summary>Records one measurement iteration whose execute duration is the sum of its step durations.</summary>
    private static void RecordIteration(ScenarioLoadCollector collector, TestStatus status, StepStandardResult browse, StepStandardResult order)
    {
        var iterationResult = new IterationResult();
        iterationResult.ExecuteStartTime = SyntheticLoadSnapshots.BaseTime;
        iterationResult.ExecuteEndTime = SyntheticLoadSnapshots.BaseTime + browse.Duration + order.Duration;
        iterationResult.StepResults.Add(browse.Name, browse);
        iterationResult.StepResults.Add(order.Name, order);
        collector.RecordMeasurement(status, iterationResult);
    }

    /// <summary>Records passing iterations: browse 10 ms, order 100 ms with a 40 ms charge sub-step.</summary>
    private static void RecordPassingIterations(ScenarioLoadCollector collector, int count)
    {
        for (var index = 0; index < count; index++)
            RecordIteration(collector, TestStatus.Passed, StepResult(BrowseStep, StepStatus.Passed, 10), StepResult(OrderStep, StepStatus.Passed, 100, StepResult(ChargeSubStep, StepStatus.Passed, 40)));
    }

    private static StepStandardResult StepResult(string name, StepStatus status, double milliseconds, StepStandardResult? subStep = null)
    {
        var stepResult = new StepStandardResult();
        stepResult.Name = name;
        stepResult.Id = name.ToLowerInvariant().Replace(' ', '-');
        stepResult.Status = status;
        stepResult.Duration = TimeSpan.FromMilliseconds(milliseconds);
        if (status == StepStatus.Failed)
            stepResult.Exception = new Exception("HTTP 500");
        if (subStep != null)
            stepResult.StepResults = new List<StepStandardResult> { subStep };
        return stepResult;
    }

    /// <summary>
    /// Asserts a percentile to within HdrHistogram's 3-significant-digit resolution: the
    /// histogram reports the highest value equivalent to the recorded one, at most 0.1 % above it.
    /// </summary>
    private static void AssertResolution(double expectedMilliseconds, TimeSpan actual)
    {
        Assert.IsInRange(TimeSpan.FromMilliseconds(expectedMilliseconds), TimeSpan.FromMilliseconds(expectedMilliseconds * 1.001), actual);
    }

    [Test]
    public async Task Verify_bucket_counts_from_known_recordings()
    {
        await Scenario()
            .Step("Every bucket receives exactly the recordings inside its bounds", context =>
            {
                // One representative duration well inside each of the fifteen buckets, with a
                // distinct count per bucket so a misplaced recording changes two entries.
                var collector = RecordDurations(
                    Milliseconds(0.5, 2), Milliseconds(1.5, 2), Milliseconds(3, 3), Milliseconds(7, 4), Milliseconds(15, 5),
                    Milliseconds(30, 6), Milliseconds(75, 7), Milliseconds(150, 8), Milliseconds(300, 9), Milliseconds(750, 10),
                    Milliseconds(1500, 11), Milliseconds(3000, 12), Milliseconds(7000, 13), Milliseconds(20000, 14), Milliseconds(45000, 15));

                var latency = collector.GetIntervalLatency();
                Assert.AreEqual(121, latency.RequestCount);
                CollectionAssert.AreEqual(new[] { 2, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, latency.BucketCounts.ToList());
            })
            .Step("A response time on a bound counts toward the bucket it bounds; one just above it toward the next", context =>
            {
                var collector = RecordDurations(
                    Milliseconds(1, 1), Milliseconds(1.1, 1),
                    Milliseconds(2, 1), Milliseconds(2.1, 1),
                    Milliseconds(10, 1), Milliseconds(10.1, 1),
                    Milliseconds(30000, 1), Milliseconds(30100, 1));

                var latency = collector.GetIntervalLatency();
                Assert.AreEqual(8, latency.RequestCount);
                CollectionAssert.AreEqual(new[] { 1, 2, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 }, latency.BucketCounts.ToList());
            })
            .Run();
    }

    [Test]
    public async Task Verify_percentiles_from_known_recordings()
    {
        await Scenario()
            .Step("Median, p95 and p99 are the order statistics of the interval's recordings", context =>
            {
                // 10, 20, ..., 200 ms once each: of 20 values the median is the 10th (100 ms),
                // p95 the 19th (190 ms) and p99 the 20th (200 ms).
                var recordings = new List<(TimeSpan Duration, int Count)>();
                for (var milliseconds = 10; milliseconds <= 200; milliseconds += 10)
                    recordings.Add(Milliseconds(milliseconds, 1));

                var latency = RecordDurations(recordings.ToArray()).GetIntervalLatency();
                Assert.AreEqual(20, latency.RequestCount);
                AssertResolution(100, latency.ResponseTimeMedian);
                AssertResolution(190, latency.ResponseTimePercentile95);
                AssertResolution(200, latency.ResponseTimePercentile99);
                // 10 ms → ≤10 ms, 20 ms → ≤20 ms, 30..50 ms → ≤50 ms, 60..100 ms → ≤100 ms, 110..200 ms → ≤200 ms.
                CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 1, 3, 5, 10, 0, 0, 0, 0, 0, 0, 0 }, latency.BucketCounts.ToList());
            })
            .Step("A slow tail shows in p99 before p95", context =>
            {
                // 95 requests at 10 ms and 5 at 500 ms: the 95th value is still 10 ms, the 99th is 500 ms.
                var latency = RecordDurations(Milliseconds(10, 95), Milliseconds(500, 5)).GetIntervalLatency();
                Assert.AreEqual(100, latency.RequestCount);
                AssertResolution(10, latency.ResponseTimeMedian);
                AssertResolution(10, latency.ResponseTimePercentile95);
                AssertResolution(500, latency.ResponseTimePercentile99);
                CollectionAssert.AreEqual(new[] { 0, 0, 0, 95, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0 }, latency.BucketCounts.ToList());
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_force_refresh_closes_the_interval_and_a_plain_result_reuses_it()
    {
        await Scenario()
            .Step("A force refresh closes the interval; a plain result between refreshes carries the last closed one", context =>
            {
                var collector = CreateMeasuringCollector();
                RecordPassingIterations(collector, 3);

                var closed = collector.GetCurrentResult(true).IntervalLatency;
                Assert.AreEqual(3, closed.RequestCount);
                AssertResolution(110, closed.ResponseTimePercentile95);
                Assert.AreEqual(3, closed.BucketCounts[LatencyBuckets.IndexOf(TimeSpan.FromMilliseconds(110))]);

                RecordPassingIterations(collector, 2);
                var between = collector.GetCurrentResult();
                // The cumulative stats moved on, but the two new iterations stay in the open
                // interval: a plain result must not drain it between dashboard ticks.
                Assert.AreEqual(5, between.Ok.RequestCount);
                Assert.AreSame(closed, between.IntervalLatency);

                var next = collector.GetCurrentResult(true).IntervalLatency;
                Assert.AreEqual(2, next.RequestCount);
                Assert.AreEqual(2, next.BucketCounts[LatencyBuckets.IndexOf(TimeSpan.FromMilliseconds(110))]);
            })
            .Step("An idle interval closes as Empty and stays Empty on the plain results that follow", context =>
            {
                var collector = CreateMeasuringCollector();
                RecordPassingIterations(collector, 3);
                _ = collector.GetCurrentResult(true);

                var idle = collector.GetCurrentResult(true);
                Assert.AreSame(IntervalLatency.Empty, idle.IntervalLatency);
                Assert.AreSame(IntervalLatency.Empty, collector.GetCurrentResult().IntervalLatency);
                // The cumulative view is unaffected by the interval cadence.
                Assert.AreEqual(3, idle.Ok.RequestCount);
            })
            .Step("Completing the measurement phase closes the interval like a force refresh does", context =>
            {
                var collector = CreateMeasuringCollector();
                RecordPassingIterations(collector, 4);
                collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(10));

                var completed = collector.GetCurrentResult();
                Assert.IsTrue(completed.IsCompleted);
                Assert.AreEqual(4, completed.IntervalLatency.RequestCount);
                Assert.AreEqual(4, completed.Steps[BrowseStep].IntervalLatency.RequestCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_intervals_are_independent_of_the_scenario_interval()
    {
        await Scenario()
            .Step("Each step's interval holds its own durations while the scenario's holds the iteration durations", context =>
            {
                var collector = CreateMeasuringCollector();
                RecordPassingIterations(collector, 4);

                var result = collector.GetCurrentResult(true);
                Assert.AreEqual(4, result.IntervalLatency.RequestCount);
                AssertResolution(110, result.IntervalLatency.ResponseTimePercentile95);
                Assert.AreEqual(4, result.IntervalLatency.BucketCounts[7]);

                var browse = result.Steps[BrowseStep].IntervalLatency;
                Assert.AreEqual(4, browse.RequestCount);
                AssertResolution(10, browse.ResponseTimeMedian);
                AssertResolution(10, browse.ResponseTimePercentile95);
                AssertResolution(10, browse.ResponseTimePercentile99);
                Assert.AreEqual(4, browse.BucketCounts[3]);

                var order = result.Steps[OrderStep].IntervalLatency;
                Assert.AreEqual(4, order.RequestCount);
                AssertResolution(100, order.ResponseTimePercentile95);
                Assert.AreEqual(4, order.BucketCounts[6]);

                var charge = Assert.ContainsSingle(result.Steps[OrderStep].Steps).IntervalLatency;
                Assert.AreEqual(4, charge.RequestCount);
                AssertResolution(40, charge.ResponseTimePercentile95);
                Assert.AreEqual(4, charge.BucketCounts[5]);
            })
            .Step("A failed iteration's passing step lands in that step's interval but not in the scenario's", context =>
            {
                var collector = CreateMeasuringCollector();
                RecordPassingIterations(collector, 2);
                RecordIteration(collector, TestStatus.Failed, StepResult(BrowseStep, StepStatus.Passed, 10), StepResult(OrderStep, StepStatus.Failed, 100, StepResult(ChargeSubStep, StepStatus.Failed, 40)));

                var result = collector.GetCurrentResult(true);
                Assert.AreEqual(2, result.IntervalLatency.RequestCount);
                Assert.AreEqual(3, result.Steps[BrowseStep].IntervalLatency.RequestCount);
                Assert.AreEqual(2, result.Steps[OrderStep].IntervalLatency.RequestCount);
                Assert.AreEqual(2, Assert.ContainsSingle(result.Steps[OrderStep].Steps).IntervalLatency.RequestCount);
            })
            .Step("A step's plain result reuses its last closed interval while the scenario's does too", context =>
            {
                var collector = CreateMeasuringCollector();
                RecordPassingIterations(collector, 1);
                var closed = collector.GetCurrentResult(true);

                RecordPassingIterations(collector, 2);
                var between = collector.GetCurrentResult();
                Assert.AreEqual(3, between.Steps[BrowseStep].Ok.RequestCount);
                Assert.AreSame(closed.Steps[BrowseStep].IntervalLatency, between.Steps[BrowseStep].IntervalLatency);
                Assert.AreSame(closed.IntervalLatency, between.IntervalLatency);

                var next = collector.GetCurrentResult(true);
                Assert.AreEqual(2, next.Steps[BrowseStep].IntervalLatency.RequestCount);
                Assert.AreEqual(2, next.IntervalLatency.RequestCount);
            })
            .Step("An interval with only a failed iteration leaves the scenario and every step Empty", context =>
            {
                var collector = CreateMeasuringCollector();
                RecordPassingIterations(collector, 1);
                _ = collector.GetCurrentResult(true);
                RecordIteration(collector, TestStatus.Failed, StepResult(BrowseStep, StepStatus.Failed, 10), StepResult(OrderStep, StepStatus.Skipped, 0));

                var result = collector.GetCurrentResult(true);
                Assert.AreSame(IntervalLatency.Empty, result.IntervalLatency);
                Assert.AreSame(IntervalLatency.Empty, result.Steps[BrowseStep].IntervalLatency);
                Assert.AreSame(IntervalLatency.Empty, result.Steps[OrderStep].IntervalLatency);
                Assert.AreEqual(1, result.Steps[BrowseStep].Failed.RequestCount);
                Assert.AreEqual(1, result.Steps[OrderStep].SkippedCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_empty_semantics()
    {
        await Scenario()
            .Step("Empty has no requests, zero percentiles and a zero count in every bucket", context =>
            {
                var empty = IntervalLatency.Empty;
                Assert.AreEqual(0, empty.RequestCount);
                Assert.AreEqual(TimeSpan.Zero, empty.ResponseTimeMedian);
                Assert.AreEqual(TimeSpan.Zero, empty.ResponseTimePercentile95);
                Assert.AreEqual(TimeSpan.Zero, empty.ResponseTimePercentile99);
                CollectionAssert.AreEqual(new int[15], empty.BucketCounts.ToList());
            })
            .Step("A collector without interval tracking always reports Empty, even after recording", context =>
            {
                var collector = new StatsCollector();
                collector.Record(TimeSpan.FromMilliseconds(10), SyntheticLoadSnapshots.BaseTime, SyntheticLoadSnapshots.At(1));

                Assert.AreSame(IntervalLatency.Empty, collector.GetIntervalLatency());
                Assert.AreEqual(1, collector.GetCurrentResult().RequestCount);
            })
            .Step("A tracking collector reports Empty before its first recording and for an interval with none", context =>
            {
                var collector = new StatsCollector(trackIntervalLatency: true);
                Assert.AreSame(IntervalLatency.Empty, collector.GetIntervalLatency());

                collector.Record(TimeSpan.FromMilliseconds(10), SyntheticLoadSnapshots.BaseTime, SyntheticLoadSnapshots.At(1));
                Assert.AreEqual(1, collector.GetIntervalLatency().RequestCount);
                Assert.AreSame(IntervalLatency.Empty, collector.GetIntervalLatency());
            })
            .Step("A fresh scenario collector reports Empty for the scenario and every step", context =>
            {
                var result = CreateMeasuringCollector().GetCurrentResult(true);
                Assert.AreSame(IntervalLatency.Empty, result.IntervalLatency);
                Assert.AreSame(IntervalLatency.Empty, result.Steps[BrowseStep].IntervalLatency);
                Assert.AreSame(IntervalLatency.Empty, result.Steps[OrderStep].IntervalLatency);
            })
            .Run();
    }

    [Test]
    public async Task Verify_latency_bucket_and_interval_latency_contracts()
    {
        await Scenario()
            .Step("The bounds are the fixed log-spaced series from 1 ms to 30 s, plus the open-ended last bucket", context =>
            {
                CollectionAssert.AreEqual(new[]
                {
                    TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20),
                    TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)
                }, LatencyBuckets.UpperBounds.ToList());
                // One bucket per bound plus the open-ended last one.
                Assert.AreEqual(LatencyBuckets.Count, LatencyBuckets.UpperBounds.Count + 1);
            })
            .Step("IndexOf maps a value to the first bucket whose bound is at or above it", context =>
            {
                Assert.AreEqual(0, LatencyBuckets.IndexOf(TimeSpan.Zero));
                Assert.AreEqual(0, LatencyBuckets.IndexOf(TimeSpan.FromMilliseconds(1)));
                Assert.AreEqual(1, LatencyBuckets.IndexOf(TimeSpan.FromMilliseconds(1) + TimeSpan.FromTicks(1)));
                Assert.AreEqual(8, LatencyBuckets.IndexOf(TimeSpan.FromMilliseconds(500)));
                Assert.AreEqual(9, LatencyBuckets.IndexOf(TimeSpan.FromMilliseconds(501)));
                Assert.AreEqual(13, LatencyBuckets.IndexOf(TimeSpan.FromSeconds(30)));
                Assert.AreEqual(14, LatencyBuckets.IndexOf(TimeSpan.FromSeconds(30) + TimeSpan.FromTicks(1)));
                Assert.AreEqual(14, LatencyBuckets.IndexOf(TimeSpan.MaxValue));
            })
            .Step("An interval latency copies its bucket counts and rejects malformed input", context =>
            {
                var counts = new int[LatencyBuckets.Count];
                counts[3] = 7;
                var latency = new IntervalLatency(7, TimeSpan.FromMilliseconds(8), TimeSpan.FromMilliseconds(9), TimeSpan.FromMilliseconds(10), counts);
                counts[3] = 99;
                Assert.AreEqual(7, latency.BucketCounts[3]);
                Assert.HasCount(LatencyBuckets.Count, latency.BucketCounts);
                // Genuinely read-only: neither list is a bare array a cast could mutate.
                Assert.IsNotInstanceOfType<int[]>(IntervalLatency.Empty.BucketCounts);
                Assert.IsNotInstanceOfType<TimeSpan[]>(LatencyBuckets.UpperBounds);
                Assert.AreEqual(TimeSpan.FromMilliseconds(8), latency.ResponseTimeMedian);
                Assert.AreEqual(TimeSpan.FromMilliseconds(9), latency.ResponseTimePercentile95);
                Assert.AreEqual(TimeSpan.FromMilliseconds(10), latency.ResponseTimePercentile99);

                Assert.ThrowsExactly<ArgumentException>(() => new IntervalLatency(1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, new int[3]));
                Assert.ThrowsExactly<ArgumentNullException>(() => new IntervalLatency(1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, null!));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new IntervalLatency(-1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, new int[LatencyBuckets.Count]));
                var negative = new int[LatencyBuckets.Count];
                negative[0] = -1;
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new IntervalLatency(1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, negative));
            })
            .Run();
    }
}
