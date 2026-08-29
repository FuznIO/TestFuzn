using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Drives the live model's per-step series, per-interval latency series, error timestamps and
/// rates, and the snapshot's newest-interval fields through a REAL in-memory
/// <see cref="ScenarioLoadCollector"/> with known recordings, the way the dashboard wiring
/// feeds it: force-refreshed snapshots at advancing 1 Hz-style ticks. Every expectation is
/// hand-derived from the recordings. Hermetic — no TestWebApp, sink or terminal.
/// </summary>
[TestClass]
public class ScenarioLiveMetricsSeriesCollectorTests : Test
{
    private const string BrowseStep = "Browse products";
    private const string OrderStep = "Place order";
    private const string ChargeSubStep = "Charge card";

    /// <summary>A two-step checkout scenario collector with no phase started yet.</summary>
    private static ScenarioLoadCollector CreateCollector()
    {
        var scenario = new Scenario("Checkout flow");
        scenario.Steps.Add(new Step { Name = BrowseStep, Id = "browse-products" });
        scenario.Steps.Add(new Step { Name = OrderStep, Id = "place-order" });
        return new ScenarioLoadCollector(scenario);
    }

    /// <summary>A two-step checkout scenario collector in its measurement phase since tick 0.</summary>
    private static ScenarioLoadCollector CreateMeasuringCollector()
    {
        var collector = CreateCollector();
        collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
        collector.MarkPhaseAsCompleted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
        collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(0));
        return collector;
    }

    private static ScenarioLiveMetrics CreateMetrics()
    {
        return new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[]
        {
            new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)) { IsWarmup = true },
            new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
        });
    }

    /// <summary>A measuring collector and its model with the baseline established at tick 0, ready for the first interval.</summary>
    private static (ScenarioLoadCollector Collector, ScenarioLiveMetrics Metrics) CreateBaselined()
    {
        var collector = CreateMeasuringCollector();
        var metrics = CreateMetrics();
        Tick(collector, metrics, 0);
        return (collector, metrics);
    }

    /// <summary>Force-refreshes the collector and records the result at the given tick, as the 1 Hz wiring does; returns the result that was fed in.</summary>
    private static ScenarioLoadResult Tick(ScenarioLoadCollector collector, ScenarioLiveMetrics metrics, double seconds)
    {
        var result = collector.GetCurrentResult(true);
        metrics.Record(result, SyntheticLoadSnapshots.At(seconds));
        return result;
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

    /// <summary>Passing iterations: browse 10 ms, order 100 ms with a 40 ms charge sub-step — 110 ms per iteration.</summary>
    private static void RecordPassingIterations(ScenarioLoadCollector collector, int count)
    {
        for (var index = 0; index < count; index++)
            RecordIteration(collector, TestStatus.Passed, StepResult(BrowseStep, StepStatus.Passed, 10), StepResult(OrderStep, StepStatus.Passed, 100, StepResult(ChargeSubStep, StepStatus.Passed, 40)));
    }

    /// <summary>Iterations failing in browse (10 ms, "HTTP 500") with order skipped, as the step handler leaves a step after an earlier failure.</summary>
    private static void RecordBrowseFailures(ScenarioLoadCollector collector, int count)
    {
        for (var index = 0; index < count; index++)
            RecordIteration(collector, TestStatus.Failed, StepResult(BrowseStep, StepStatus.Failed, 10), StepResult(OrderStep, StepStatus.Skipped, 0));
    }

    /// <summary>Iterations passing browse (10 ms) and failing order (100 ms, "Card declined").</summary>
    private static void RecordOrderFailures(ScenarioLoadCollector collector, int count)
    {
        for (var index = 0; index < count; index++)
            RecordIteration(collector, TestStatus.Failed, StepResult(BrowseStep, StepStatus.Passed, 10), StepResult(OrderStep, StepStatus.Failed, 100, message: "Card declined"));
    }

    private static StepStandardResult StepResult(string name, StepStatus status, double milliseconds, StepStandardResult? subStep = null, string message = "HTTP 500")
    {
        var stepResult = new StepStandardResult();
        stepResult.Name = name;
        stepResult.Id = name.ToLowerInvariant().Replace(' ', '-');
        stepResult.Status = status;
        stepResult.Duration = TimeSpan.FromMilliseconds(milliseconds);
        if (status == StepStatus.Failed)
            stepResult.Exception = new Exception(message);
        if (subStep != null)
            stepResult.StepResults = new List<StepStandardResult> { subStep };
        return stepResult;
    }

    /// <summary>
    /// Asserts a millisecond series entry to within HdrHistogram's 3-significant-digit
    /// resolution: the histogram reports the highest value equivalent to the recorded one, at
    /// most 0.1 % above it.
    /// </summary>
    private static void AssertResolution(double expectedMilliseconds, double actualMilliseconds)
    {
        Assert.IsInRange(expectedMilliseconds, expectedMilliseconds * 1.001, actualMilliseconds);
    }

    private static LiveStepMetrics StepRow(LiveMetricsSnapshot view, string name)
    {
        return view.Steps.Single(step => step.Name == name);
    }

    [Test]
    public async Task Verify_per_step_series_match_the_recordings()
    {
        await Scenario()
            .Step("Each top-level step's deltas, rate and interval p95 follow its own recordings tick by tick", context =>
            {
                var (collector, metrics) = CreateBaselined();

                RecordPassingIterations(collector, 3);
                Tick(collector, metrics, 1);

                RecordPassingIterations(collector, 2);
                RecordBrowseFailures(collector, 1);
                Tick(collector, metrics, 2);

                RecordOrderFailures(collector, 1);
                Tick(collector, metrics, 3);

                Tick(collector, metrics, 4);

                var view = metrics.Current;
                Assert.HasCount(4, view.Samples);
                CollectionAssert.AreEqual(new double[] { 3, 2, 0, 0 }, view.OkDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 0, 1, 1, 0 }, view.FailedDeltaSeries.ToList());

                var browse = StepRow(view, BrowseStep);
                CollectionAssert.AreEqual(new double[] { 3, 2, 1, 0 }, browse.OkDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 0, 1, 0, 0 }, browse.FailedDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 3, 3, 1, 0 }, browse.RequestsPerSecondSeries.ToList());
                Assert.HasCount(4, browse.ResponseTimePercentile95Series);
                AssertResolution(10, browse.ResponseTimePercentile95Series[0]);
                AssertResolution(10, browse.ResponseTimePercentile95Series[1]);
                AssertResolution(10, browse.ResponseTimePercentile95Series[2]);
                Assert.AreEqual(0.0, browse.ResponseTimePercentile95Series[3]);

                var order = StepRow(view, OrderStep);
                // The browse failure at tick 2 left order skipped: it recorded nothing in that
                // iteration, so only the two passing iterations count.
                CollectionAssert.AreEqual(new double[] { 3, 2, 0, 0 }, order.OkDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 0, 0, 1, 0 }, order.FailedDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 3, 2, 1, 0 }, order.RequestsPerSecondSeries.ToList());
                Assert.HasCount(4, order.ResponseTimePercentile95Series);
                AssertResolution(100, order.ResponseTimePercentile95Series[0]);
                AssertResolution(100, order.ResponseTimePercentile95Series[1]);
                // Tick 3 had only a failed order execution: no Ok record, so its interval p95 is zero.
                Assert.AreEqual(0.0, order.ResponseTimePercentile95Series[2]);
                Assert.AreEqual(0.0, order.ResponseTimePercentile95Series[3]);
                Assert.AreEqual(1, order.SkippedCount);
                Assert.AreEqual(5, order.RequestCountOk);
                Assert.AreEqual(1, order.RequestCountFailed);
            })
            .Step("The row's current rate is the newest series entry, and rows stay top-level only (a sub-step is no row)", context =>
            {
                var (collector, metrics) = CreateBaselined();
                RecordPassingIterations(collector, 4);
                // A two-second interval: four requests give 2 rps.
                var result = Tick(collector, metrics, 2);

                var view = metrics.Current;
                Assert.HasCount(2, view.Steps);
                Assert.AreEqual(BrowseStep, view.Steps[0].Name);
                Assert.AreEqual(OrderStep, view.Steps[1].Name);
                foreach (var row in view.Steps)
                {
                    Assert.AreEqual(2.0, row.RequestsPerSecond, 1e-9);
                    Assert.AreEqual(row.RequestsPerSecond, Assert.ContainsSingle(row.RequestsPerSecondSeries));
                    CollectionAssert.AreEqual(new double[] { 4 }, row.OkDeltaSeries.ToList());
                }
                // The charge sub-step's executions are in the collector's tree, not in a row.
                Assert.AreEqual(4, Assert.ContainsSingle(result.Steps[OrderStep].Steps).Ok.RequestCount);
            })
            .Step("Before the first interval closes every step series is empty, and its requests land in the first interval", context =>
            {
                var (collector, metrics) = CreateBaselined();
                RecordPassingIterations(collector, 2);
                // A Record whose timestamp does not advance closes no interval.
                Tick(collector, metrics, 0);

                var view = metrics.Current;
                Assert.IsEmpty(view.Samples);
                var browse = StepRow(view, BrowseStep);
                Assert.AreEqual(2, browse.RequestCountOk);
                Assert.AreEqual(0.0, browse.RequestsPerSecond);
                Assert.IsEmpty(browse.OkDeltaSeries);
                Assert.IsEmpty(browse.FailedDeltaSeries);
                Assert.IsEmpty(browse.RequestsPerSecondSeries);
                Assert.IsEmpty(browse.ResponseTimePercentile95Series);

                Tick(collector, metrics, 1);
                CollectionAssert.AreEqual(new double[] { 2 }, StepRow(metrics.Current, BrowseStep).OkDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 2 }, StepRow(metrics.Current, OrderStep).OkDeltaSeries.ToList());
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_series_are_zero_across_warmup_while_the_scenario_series_shows_it()
    {
        await Scenario()
            .Step("Warmup iterations count for the scenario only: every step series stays zero until the first measurement interval", context =>
            {
                var collector = CreateCollector();
                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Warmup, SyntheticLoadSnapshots.At(0));
                var metrics = CreateMetrics();
                Tick(collector, metrics, 0);

                for (var index = 0; index < 5; index++)
                    collector.RecordWarmup(TestStatus.Passed);
                Tick(collector, metrics, 1);

                for (var index = 0; index < 3; index++)
                    collector.RecordWarmup(TestStatus.Passed);
                collector.RecordWarmup(TestStatus.Failed);
                Tick(collector, metrics, 2);

                collector.MarkPhaseAsCompleted(LoadTestPhase.Warmup, SyntheticLoadSnapshots.At(2));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(2));
                RecordPassingIterations(collector, 4);
                Tick(collector, metrics, 3);

                var view = metrics.Current;
                Assert.AreEqual(8, view.WarmupRequestCountOk);
                Assert.AreEqual(1, view.WarmupRequestCountFailed);
                Assert.AreEqual(4, view.RequestCountOk);
                CollectionAssert.AreEqual(new double[] { 5, 3, 4 }, view.OkDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 0, 1, 0 }, view.FailedDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 5, 4, 4 }, view.RequestsPerSecondSeries.ToList());

                Assert.HasCount(2, view.Steps);
                foreach (var row in view.Steps)
                {
                    CollectionAssert.AreEqual(new double[] { 0, 0, 4 }, row.OkDeltaSeries.ToList());
                    CollectionAssert.AreEqual(new double[] { 0, 0, 0 }, row.FailedDeltaSeries.ToList());
                    CollectionAssert.AreEqual(new double[] { 0, 0, 4 }, row.RequestsPerSecondSeries.ToList());
                    Assert.HasCount(3, row.ResponseTimePercentile95Series);
                    Assert.AreEqual(0.0, row.ResponseTimePercentile95Series[0]);
                    Assert.AreEqual(0.0, row.ResponseTimePercentile95Series[1]);
                    Assert.AreEqual(4, row.RequestCountOk);
                }
                AssertResolution(10, StepRow(view, BrowseStep).ResponseTimePercentile95Series[2]);
                AssertResolution(100, StepRow(view, OrderStep).ResponseTimePercentile95Series[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_latency_series_and_newest_interval_fields_through_a_real_collector()
    {
        await Scenario()
            .Step("The median, p99 and bucket series carry each interval's distribution, and the newest-interval fields agree with its sample", context =>
            {
                var (collector, metrics) = CreateBaselined();

                // 95 passing iterations at 110 ms and 5 slow ones at 500 ms (order 490 ms).
                RecordPassingIterations(collector, 95);
                for (var index = 0; index < 5; index++)
                    RecordIteration(collector, TestStatus.Passed, StepResult(BrowseStep, StepStatus.Passed, 10), StepResult(OrderStep, StepStatus.Passed, 490));
                var first = Tick(collector, metrics, 1);

                var view = metrics.Current;
                Assert.HasCount(1, view.ResponseTimeMedianSeries);
                Assert.HasCount(1, view.ResponseTimePercentile99Series);
                AssertResolution(110, view.ResponseTimeMedianSeries[0]);
                AssertResolution(110, view.ResponseTimePercentile95Series[0]);
                AssertResolution(500, view.ResponseTimePercentile99Series[0]);
                var buckets = Assert.ContainsSingle(view.LatencyBucketSeries);
                Assert.HasCount(LatencyBuckets.Count, buckets);
                // 110 ms → the ≤200 ms bucket; 500 ms sits on the ≤500 ms bound and is placed
                // by its lowest equivalent value, so it counts toward the bucket it bounds.
                Assert.AreEqual(95, buckets[7]);
                Assert.AreEqual(5, buckets[8]);
                Assert.AreEqual(100, buckets.Sum());
                // Shared, not copied: the vector is the interval's own immutable list.
                Assert.AreSame(first.IntervalLatency.BucketCounts, buckets);

                Assert.AreEqual(100, view.IntervalRequestCount);
                Assert.AreEqual(0.0, view.ErrorRate);
                Assert.AreEqual(100.0, view.IntervalRequestsPerSecond);
                Assert.AreSame(first.IntervalLatency, view.IntervalLatency);
                Assert.AreEqual(100, view.IntervalLatency.RequestCount);
                Assert.AreEqual(view.ResponseTimePercentile99Series[0], view.IntervalLatency.ResponseTimePercentile99.TotalMilliseconds);
            })
            .Step("A mixed interval reports the failed share over all its requests, while the latency side counts only the successful ones", context =>
            {
                var (collector, metrics) = CreateBaselined();
                RecordPassingIterations(collector, 3);
                RecordBrowseFailures(collector, 1);
                // A two-second interval: four requests give 2 rps.
                Tick(collector, metrics, 2);

                var view = metrics.Current;
                Assert.AreEqual(4, view.IntervalRequestCount);
                Assert.AreEqual(0.25, view.ErrorRate);
                Assert.AreEqual(2.0, view.IntervalRequestsPerSecond);
                Assert.AreEqual(3, view.IntervalLatency.RequestCount);
                AssertResolution(110, view.ResponseTimeMedianSeries[0]);
                Assert.AreEqual(3, view.LatencyBucketSeries[0][7]);
            })
            .Step("An idle interval reports zeros, an all-zero bucket vector and an Empty interval latency", context =>
            {
                var (collector, metrics) = CreateBaselined();
                RecordPassingIterations(collector, 2);
                Tick(collector, metrics, 1);
                Tick(collector, metrics, 2);

                var view = metrics.Current;
                Assert.HasCount(2, view.LatencyBucketSeries);
                Assert.AreEqual(2, view.LatencyBucketSeries[0][7]);
                Assert.AreEqual(0.0, view.ResponseTimeMedianSeries[1]);
                Assert.AreEqual(0.0, view.ResponseTimePercentile99Series[1]);
                CollectionAssert.AreEqual(new int[LatencyBuckets.Count], view.LatencyBucketSeries[1].ToList());
                Assert.AreEqual(0, view.IntervalRequestCount);
                Assert.AreEqual(0.0, view.ErrorRate);
                Assert.AreEqual(0.0, view.IntervalRequestsPerSecond);
                Assert.AreSame(IntervalLatency.Empty, view.IntervalLatency);
            })
            .Step("An interval of failures only has an error rate of one and no latency", context =>
            {
                var (collector, metrics) = CreateBaselined();
                RecordBrowseFailures(collector, 3);
                Tick(collector, metrics, 1);

                var view = metrics.Current;
                Assert.AreEqual(3, view.IntervalRequestCount);
                Assert.AreEqual(1.0, view.ErrorRate);
                Assert.AreEqual(3.0, view.IntervalRequestsPerSecond);
                Assert.AreSame(IntervalLatency.Empty, view.IntervalLatency);
                CollectionAssert.AreEqual(new int[LatencyBuckets.Count], Assert.ContainsSingle(view.LatencyBucketSeries).ToList());
            })
            .Step("Before the first sample the newest-interval fields are zero and the latency series empty", context =>
            {
                var (collector, metrics) = CreateBaselined();

                var view = metrics.Current;
                Assert.AreEqual(0, view.IntervalRequestCount);
                Assert.AreEqual(0.0, view.ErrorRate);
                Assert.AreEqual(0.0, view.IntervalRequestsPerSecond);
                Assert.AreSame(IntervalLatency.Empty, view.IntervalLatency);
                Assert.IsEmpty(view.ResponseTimeMedianSeries);
                Assert.IsEmpty(view.ResponseTimePercentile99Series);
                Assert.IsEmpty(view.LatencyBucketSeries);
            })
            .Step("Warmup requests count toward the interval request count and error rate", context =>
            {
                var collector = CreateCollector();
                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Warmup, SyntheticLoadSnapshots.At(0));
                var metrics = CreateMetrics();
                Tick(collector, metrics, 0);

                for (var index = 0; index < 2; index++)
                    collector.RecordWarmup(TestStatus.Passed);
                for (var index = 0; index < 2; index++)
                    collector.RecordWarmup(TestStatus.Failed);
                Tick(collector, metrics, 1);

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Warmup, view.Phase);
                Assert.AreEqual(4, view.IntervalRequestCount);
                Assert.AreEqual(0.5, view.ErrorRate);
                Assert.AreEqual(4.0, view.IntervalRequestsPerSecond);
                // Warmup iterations are counted, not timed: no latency interval.
                Assert.AreSame(IntervalLatency.Empty, view.IntervalLatency);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_first_seen_last_seen_and_rate_across_samples()
    {
        await Scenario()
            .Step("An error is first seen with a zero rate, then rates its count delta over the actual gap between samples", context =>
            {
                var (collector, metrics) = CreateBaselined();

                RecordBrowseFailures(collector, 3);
                Tick(collector, metrics, 1);
                var entry = Assert.ContainsSingle(metrics.Current.Errors);
                Assert.AreEqual(BrowseStep, entry.StepName);
                Assert.AreEqual("HTTP 500", entry.Message);
                Assert.AreEqual(3, entry.Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), entry.FirstSeen);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), entry.LastSeen);
                Assert.AreEqual(0.0, entry.RatePerSecond);

                RecordBrowseFailures(collector, 2);
                Tick(collector, metrics, 2);
                entry = Assert.ContainsSingle(metrics.Current.Errors);
                Assert.AreEqual(5, entry.Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), entry.FirstSeen);
                Assert.AreEqual(SyntheticLoadSnapshots.At(2), entry.LastSeen);
                Assert.AreEqual(2.0, entry.RatePerSecond, 1e-9);

                // A quiet sample: the rate drops to zero while first and last seen stay.
                Tick(collector, metrics, 3);
                entry = Assert.ContainsSingle(metrics.Current.Errors);
                Assert.AreEqual(5, entry.Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), entry.FirstSeen);
                Assert.AreEqual(SyntheticLoadSnapshots.At(2), entry.LastSeen);
                Assert.AreEqual(0.0, entry.RatePerSecond);

                // A late tick two seconds after the previous sample: four new occurrences over 2 s.
                RecordBrowseFailures(collector, 4);
                Tick(collector, metrics, 5);
                entry = Assert.ContainsSingle(metrics.Current.Errors);
                Assert.AreEqual(9, entry.Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), entry.FirstSeen);
                Assert.AreEqual(SyntheticLoadSnapshots.At(5), entry.LastSeen);
                Assert.AreEqual(2.0, entry.RatePerSecond, 1e-9);
            })
            .Step("An error already present at the baseline Record is first seen there and rated from it at the first sample", context =>
            {
                var collector = CreateMeasuringCollector();
                var metrics = CreateMetrics();
                RecordBrowseFailures(collector, 2);
                Tick(collector, metrics, 0);

                var entry = Assert.ContainsSingle(metrics.Current.Errors);
                Assert.AreEqual(2, entry.Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(0), entry.FirstSeen);
                Assert.AreEqual(SyntheticLoadSnapshots.At(0), entry.LastSeen);
                Assert.AreEqual(0.0, entry.RatePerSecond);

                RecordBrowseFailures(collector, 3);
                Tick(collector, metrics, 1);
                entry = Assert.ContainsSingle(metrics.Current.Errors);
                Assert.AreEqual(5, entry.Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(0), entry.FirstSeen);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), entry.LastSeen);
                Assert.AreEqual(3.0, entry.RatePerSecond, 1e-9);
            })
            .Step("Each distinct error keeps its own timestamps and rate", context =>
            {
                var (collector, metrics) = CreateBaselined();
                RecordBrowseFailures(collector, 2);
                RecordOrderFailures(collector, 1);
                Tick(collector, metrics, 1);

                RecordOrderFailures(collector, 4);
                Tick(collector, metrics, 2);

                var view = metrics.Current;
                Assert.HasCount(2, view.Errors);
                // Most recently active first.
                var declined = view.Errors[0];
                Assert.AreEqual(OrderStep, declined.StepName);
                Assert.AreEqual("Card declined", declined.Message);
                Assert.AreEqual(5, declined.Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), declined.FirstSeen);
                Assert.AreEqual(SyntheticLoadSnapshots.At(2), declined.LastSeen);
                Assert.AreEqual(4.0, declined.RatePerSecond, 1e-9);

                var serverError = view.Errors[1];
                Assert.AreEqual(BrowseStep, serverError.StepName);
                Assert.AreEqual("HTTP 500", serverError.Message);
                Assert.AreEqual(2, serverError.Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), serverError.FirstSeen);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), serverError.LastSeen);
                Assert.AreEqual(0.0, serverError.RatePerSecond);
            })
            .Run();
    }

    [Test]
    public async Task Verify_series_are_bounded_and_ordered_past_capacity()
    {
        await Scenario()
            .Step("Past capacity the oldest entries drop from the scenario's and every step's series, and order stays oldest to newest", context =>
            {
                var (collector, metrics) = CreateBaselined();

                // (tick % 3) + 1 passing iterations at every tick, so the delta at tick t identifies t.
                var appendedSamples = ScenarioLiveMetrics.SampleCapacity + 5;
                for (var tick = 1; tick <= appendedSamples; tick++)
                {
                    RecordPassingIterations(collector, tick % 3 + 1);
                    Tick(collector, metrics, tick);
                }

                // 305 intervals closed into a 300-entry ring: ticks 1..5 dropped, tick 6 is the oldest held.
                var oldestRetainedTick = appendedSamples - ScenarioLiveMetrics.SampleCapacity + 1;
                var expected = new double[ScenarioLiveMetrics.SampleCapacity];
                for (var index = 0; index < expected.Length; index++)
                    expected[index] = (oldestRetainedTick + index) % 3 + 1;

                var view = metrics.Current;
                CollectionAssert.AreEqual(expected, view.OkDeltaSeries.ToList());
                CollectionAssert.AreEqual(expected, view.RequestsPerSecondSeries.ToList());
                Assert.HasCount(ScenarioLiveMetrics.SampleCapacity, view.ResponseTimeMedianSeries);
                Assert.HasCount(ScenarioLiveMetrics.SampleCapacity, view.ResponseTimePercentile99Series);
                Assert.HasCount(ScenarioLiveMetrics.SampleCapacity, view.LatencyBucketSeries);
                // Every retained interval had (t % 3) + 1 iterations of 110 ms in the ≤200 ms bucket.
                for (var index = 0; index < expected.Length; index++)
                    Assert.AreEqual((int)expected[index], view.LatencyBucketSeries[index][7]);

                Assert.HasCount(2, view.Steps);
                foreach (var row in view.Steps)
                {
                    CollectionAssert.AreEqual(expected, row.OkDeltaSeries.ToList());
                    CollectionAssert.AreEqual(expected, row.RequestsPerSecondSeries.ToList());
                    CollectionAssert.AreEqual(new double[ScenarioLiveMetrics.SampleCapacity], row.FailedDeltaSeries.ToList());
                    Assert.HasCount(ScenarioLiveMetrics.SampleCapacity, row.ResponseTimePercentile95Series);
                    Assert.AreEqual(expected[expected.Length - 1], row.RequestsPerSecond);
                }
                // 305 % 3 + 1 = 3 iterations in the newest interval.
                Assert.AreEqual(3.0, view.Steps[0].RequestsPerSecond);
                Assert.AreEqual(3, view.IntervalRequestCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_published_step_series_and_error_entries_are_immutable()
    {
        await Scenario()
            .Step("A captured view's step series, latency series, interval fields and error entries never change when later Records publish new ones", context =>
            {
                var (collector, metrics) = CreateBaselined();
                RecordPassingIterations(collector, 2);
                RecordBrowseFailures(collector, 1);
                Tick(collector, metrics, 1);
                var captured = metrics.Current;

                RecordPassingIterations(collector, 3);
                RecordBrowseFailures(collector, 4);
                Tick(collector, metrics, 2);

                Assert.AreNotSame(captured, metrics.Current);
                var capturedBrowse = StepRow(captured, BrowseStep);
                CollectionAssert.AreEqual(new double[] { 2 }, capturedBrowse.OkDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 1 }, capturedBrowse.FailedDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 3 }, capturedBrowse.RequestsPerSecondSeries.ToList());
                Assert.HasCount(1, captured.LatencyBucketSeries);
                Assert.AreEqual(2, captured.LatencyBucketSeries[0][7]);
                Assert.AreEqual(3, captured.IntervalRequestCount);
                Assert.AreEqual(2, captured.IntervalLatency.RequestCount);
                var capturedError = Assert.ContainsSingle(captured.Errors);
                Assert.AreEqual(1, capturedError.Count);
                Assert.AreEqual(0.0, capturedError.RatePerSecond);

                var current = metrics.Current;
                var currentBrowse = StepRow(current, BrowseStep);
                CollectionAssert.AreEqual(new double[] { 2, 3 }, currentBrowse.OkDeltaSeries.ToList());
                CollectionAssert.AreEqual(new double[] { 1, 4 }, currentBrowse.FailedDeltaSeries.ToList());
                Assert.AreNotSame(capturedBrowse.OkDeltaSeries, currentBrowse.OkDeltaSeries);
                Assert.AreEqual(7, current.IntervalRequestCount);
                var currentError = Assert.ContainsSingle(current.Errors);
                Assert.AreEqual(5, currentError.Count);
                Assert.AreEqual(4.0, currentError.RatePerSecond, 1e-9);
            })
            .Step("A reader concurrent with the ingest loop only ever sees coherent step and latency series", async context =>
            {
                var (collector, metrics) = CreateBaselined();
                var tickCount = 1000;
                var minimumReadCount = 1000;
                var violations = 0;
                var readCount = 0;

                var writer = Task.Run(() =>
                {
                    // One passing iteration per one-second tick, so every coherent view shows a
                    // 1 in every step's ok-delta and rate entry, a 0 in every failed-delta
                    // entry, and one entry per sample in every series.
                    for (var tick = 1; tick <= tickCount; tick++)
                    {
                        RecordPassingIterations(collector, 1);
                        Tick(collector, metrics, tick);
                    }
                });

                var reader = Task.Run(() =>
                {
                    // The read floor keeps the reader racing the writer for real instead of
                    // exiting after a handful of reads when the writer finishes quickly.
                    do
                    {
                        var view = metrics.Current;
                        readCount++;

                        var sampleCount = view.Samples.Count;
                        if (view.ResponseTimeMedianSeries.Count != sampleCount
                            || view.ResponseTimePercentile99Series.Count != sampleCount
                            || view.LatencyBucketSeries.Count != sampleCount)
                            violations++;
                        if (sampleCount > 0 && view.IntervalRequestCount != view.Samples[sampleCount - 1].OkDelta + view.Samples[sampleCount - 1].FailedDelta)
                            violations++;

                        foreach (var row in view.Steps)
                        {
                            if (row.OkDeltaSeries.Count != sampleCount
                                || row.FailedDeltaSeries.Count != sampleCount
                                || row.RequestsPerSecondSeries.Count != sampleCount
                                || row.ResponseTimePercentile95Series.Count != sampleCount)
                            {
                                violations++;
                                continue;
                            }

                            for (var index = 0; index < sampleCount; index++)
                            {
                                if (row.OkDeltaSeries[index] != 1.0 || row.FailedDeltaSeries[index] != 0.0 || row.RequestsPerSecondSeries[index] != 1.0)
                                    violations++;
                            }
                        }
                    } while (!writer.IsCompleted || readCount < minimumReadCount);
                });

                await Task.WhenAll(writer, reader);

                Assert.AreEqual(0, violations);
                Assert.IsGreaterThanOrEqualTo(minimumReadCount, readCount);
            })
            .Run();
    }
}
