using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins <see cref="ThresholdEvaluator"/>: the live state rule with its warning band and
/// zero-limit exception, the live states and breach durations sample by sample through a REAL
/// in-memory <see cref="ScenarioLoadCollector"/> and <see cref="ScenarioLiveMetrics"/> fed the
/// way the dashboard wiring feeds them, the phase rules — the placeholder before measurement
/// and before the first interval, the freeze after measurement — and the completion verdict on
/// cumulative results with the exact violation message. Every expectation is hand-derived from
/// the recordings. Hermetic — no TestWebApp, sink or terminal.
/// </summary>
[TestClass]
public class ThresholdEvaluatorTests : Test
{
    private const string CheckoutStepName = "Checkout step";

    private static DateTime At(double seconds)
    {
        return SyntheticLoadSnapshots.At(seconds);
    }

    /// <summary>Thresholds declared through the public builder, as <c>.Load().Thresholds(...)</c> declares them.</summary>
    private static IReadOnlyList<Threshold> Declare(Action<ThresholdsBuilder> declare)
    {
        var thresholds = new List<Threshold>();
        declare(new ThresholdsBuilder(thresholds));
        return thresholds;
    }

    /// <summary>A one-step checkout scenario collector with init completed at tick 0 and no later phase started.</summary>
    private static ScenarioLoadCollector CreateInitializedCollector()
    {
        var scenario = new Scenario("Checkout flow");
        scenario.Steps.Add(new Step { Name = CheckoutStepName, Id = "checkout-step" });

        var collector = new ScenarioLoadCollector(scenario);
        collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
        collector.MarkPhaseAsCompleted(LoadTestPhase.Init, At(0));
        return collector;
    }

    /// <summary>A one-step checkout scenario collector in its measurement phase since tick 0.</summary>
    private static ScenarioLoadCollector CreateMeasuringCollector()
    {
        var collector = CreateInitializedCollector();
        collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, At(0));
        return collector;
    }

    private static ScenarioLiveMetrics CreateMetrics(IReadOnlyList<Threshold> thresholds)
    {
        return new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[]
        {
            new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
        }, thresholds);
    }

    /// <summary>Force-refreshes the collector and records the result at the given tick, as the 1 Hz wiring does.</summary>
    private static void Tick(ScenarioLoadCollector collector, ScenarioLiveMetrics metrics, double seconds)
    {
        metrics.Record(collector.GetCurrentResult(true), At(seconds));
    }

    /// <summary>Records measurement iterations whose single step matches the scenario status, each with the given execute duration.</summary>
    private static void RecordIterations(ScenarioLoadCollector collector, int count, TestStatus status, TimeSpan executeDuration)
    {
        for (var index = 0; index < count; index++)
        {
            var iterationResult = new IterationResult();
            iterationResult.ExecuteStartTime = SyntheticLoadSnapshots.BaseTime;
            iterationResult.ExecuteEndTime = SyntheticLoadSnapshots.BaseTime + executeDuration;

            var stepResult = new StepStandardResult();
            stepResult.Name = CheckoutStepName;
            stepResult.Id = "checkout-step";
            stepResult.Status = status == TestStatus.Passed ? StepStatus.Passed : StepStatus.Failed;
            if (status == TestStatus.Failed)
                stepResult.Exception = new Exception("HTTP 500");
            stepResult.Duration = executeDuration;
            iterationResult.StepResults.Add(CheckoutStepName, stepResult);

            collector.RecordMeasurement(status, iterationResult);
        }
    }

    /// <summary>
    /// Asserts a millisecond value to within HdrHistogram's 3-significant-digit resolution: the
    /// histogram reports the highest value equivalent to the recorded one, at most 0.1 % above it.
    /// </summary>
    private static void AssertResolution(double expectedMilliseconds, double actualMilliseconds)
    {
        Assert.IsInRange(expectedMilliseconds, expectedMilliseconds * 1.001, actualMilliseconds);
    }

    /// <summary>Asserts a live reading that is not breached: its current value and state, and no breach duration.</summary>
    private static void AssertReads(LiveThreshold actual, double expectedCurrent, ThresholdState expectedState)
    {
        Assert.AreEqual(expectedCurrent, actual.Current, 1e-9);
        Assert.AreEqual(expectedState, actual.State);
        Assert.AreEqual(TimeSpan.Zero, actual.BreachedFor);
    }

    /// <summary>Asserts a breached live reading: its current value and how long the breach has lasted.</summary>
    private static void AssertBreached(LiveThreshold actual, double expectedCurrent, TimeSpan expectedBreachedFor)
    {
        Assert.AreEqual(expectedCurrent, actual.Current, 1e-9);
        Assert.AreEqual(ThresholdState.Breached, actual.State);
        Assert.AreEqual(expectedBreachedFor, actual.BreachedFor);
    }

    /// <summary>Asserts the placeholder: every threshold Ok with a Current of 0 and no breach.</summary>
    private static void AssertPlaceholder(IReadOnlyList<LiveThreshold> thresholds, int expectedCount)
    {
        Assert.HasCount(expectedCount, thresholds);
        foreach (var threshold in thresholds)
            AssertReads(threshold, 0.0, ThresholdState.Ok);
    }

    private static void AssertVerdict(ThresholdResult actual, ThresholdMetric expectedMetric, double expectedCurrent, bool expectedPassed)
    {
        Assert.AreEqual(expectedMetric, actual.Threshold.Metric);
        Assert.AreEqual(expectedCurrent, actual.Current, 1e-9);
        Assert.AreEqual(expectedPassed, actual.Passed);
    }

    [Test]
    public async Task Verify_state_rule_for_maximum_and_minimum_thresholds()
    {
        await Scenario()
            .Step("A maximum is Ok below 80 % of its limit, Warning from 80 % up to the limit itself, and Breached only above it", context =>
            {
                var p95 = new Threshold(ThresholdMetric.ResponseTimePercentile95, 100, ThresholdComparison.LessThanOrEqualTo);
                Assert.AreEqual(ThresholdState.Ok, ThresholdEvaluator.StateOf(p95, 0));
                Assert.AreEqual(ThresholdState.Ok, ThresholdEvaluator.StateOf(p95, 79.9));
                Assert.AreEqual(ThresholdState.Warning, ThresholdEvaluator.StateOf(p95, 80));
                Assert.AreEqual(ThresholdState.Warning, ThresholdEvaluator.StateOf(p95, 99.9));
                Assert.AreEqual(ThresholdState.Warning, ThresholdEvaluator.StateOf(p95, 100));
                Assert.AreEqual(ThresholdState.Breached, ThresholdEvaluator.StateOf(p95, 100.1));
                Assert.IsFalse(ThresholdEvaluator.IsViolated(p95, 100));
                Assert.IsTrue(ThresholdEvaluator.IsViolated(p95, 100.1));
            })
            .Step("A minimum mirrors it: Ok above 125 % of its limit, Warning from 125 % down to the limit itself, and Breached only below it", context =>
            {
                var rps = new Threshold(ThresholdMetric.RequestsPerSecond, 50, ThresholdComparison.GreaterThanOrEqualTo);
                Assert.AreEqual(ThresholdState.Ok, ThresholdEvaluator.StateOf(rps, 100));
                Assert.AreEqual(ThresholdState.Ok, ThresholdEvaluator.StateOf(rps, 62.6));
                Assert.AreEqual(ThresholdState.Warning, ThresholdEvaluator.StateOf(rps, 62.5));
                Assert.AreEqual(ThresholdState.Warning, ThresholdEvaluator.StateOf(rps, 50.1));
                Assert.AreEqual(ThresholdState.Warning, ThresholdEvaluator.StateOf(rps, 50));
                Assert.AreEqual(ThresholdState.Breached, ThresholdEvaluator.StateOf(rps, 49.9));
                Assert.AreEqual(ThresholdState.Breached, ThresholdEvaluator.StateOf(rps, 0));
                Assert.IsFalse(ThresholdEvaluator.IsViolated(rps, 50));
                Assert.IsTrue(ThresholdEvaluator.IsViolated(rps, 49.9));
            })
            .Step("A zero limit has no warning band: Ok until crossed", context =>
            {
                var noErrors = new Threshold(ThresholdMetric.ErrorRate, 0, ThresholdComparison.LessThanOrEqualTo);
                Assert.AreEqual(ThresholdState.Ok, ThresholdEvaluator.StateOf(noErrors, 0));
                Assert.AreEqual(ThresholdState.Breached, ThresholdEvaluator.StateOf(noErrors, 0.001));

                var anyRate = new Threshold(ThresholdMetric.RequestsPerSecond, 0, ThresholdComparison.GreaterThanOrEqualTo);
                Assert.AreEqual(ThresholdState.Ok, ThresholdEvaluator.StateOf(anyRate, 0));
                Assert.AreEqual(ThresholdState.Ok, ThresholdEvaluator.StateOf(anyRate, 5));
            })
            .Run();
    }

    [Test]
    public async Task Verify_live_states_and_breach_duration_through_a_real_collector()
    {
        await Scenario()
            .Step("Each measurement sample reads the newest interval against the limits, and a breach's duration runs on the sample timestamps until the state leaves Breached", context =>
            {
                var collector = CreateMeasuringCollector();
                var metrics = CreateMetrics(Declare(thresholds => thresholds
                    .ResponseTimePercentile95(TimeSpan.FromMilliseconds(100))
                    .ErrorRate(0.1)
                    .RequestsPerSecond(minimum: 2)));
                Tick(collector, metrics, 0);

                // Tick 1: five requests at 110 ms — the p95 is past its 100 ms limit; 5 rps is
                // clear of the 2 rps minimum and its 2.5 rps warning band.
                RecordIterations(collector, 5, TestStatus.Passed, TimeSpan.FromMilliseconds(110));
                Tick(collector, metrics, 1);
                var view = metrics.Current;
                Assert.HasCount(3, view.Thresholds);
                Assert.AreEqual(ThresholdMetric.ResponseTimePercentile95, view.Thresholds[0].Threshold.Metric);
                Assert.AreEqual(ThresholdMetric.ErrorRate, view.Thresholds[1].Threshold.Metric);
                Assert.AreEqual(ThresholdMetric.RequestsPerSecond, view.Thresholds[2].Threshold.Metric);
                AssertResolution(110, view.Thresholds[0].Current);
                Assert.AreEqual(ThresholdState.Breached, view.Thresholds[0].State);
                Assert.AreEqual(TimeSpan.Zero, view.Thresholds[0].BreachedFor);
                AssertReads(view.Thresholds[1], 0.0, ThresholdState.Ok);
                AssertReads(view.Thresholds[2], 5.0, ThresholdState.Ok);

                // Tick 2: still 110 ms — the breach is one second old.
                RecordIterations(collector, 5, TestStatus.Passed, TimeSpan.FromMilliseconds(110));
                Tick(collector, metrics, 2);
                var capturedAtTick2 = metrics.Current;
                Assert.AreEqual(ThresholdState.Breached, capturedAtTick2.Thresholds[0].State);
                Assert.AreEqual(TimeSpan.FromSeconds(1), capturedAtTick2.Thresholds[0].BreachedFor);

                // Tick 4, a late tick two seconds on: four requests at 85 ms — inside the limit
                // but at 80 % of it or more, so Warning, and the breach duration is gone; four
                // requests over two seconds are 2 rps, exactly the minimum: Warning as well.
                RecordIterations(collector, 4, TestStatus.Passed, TimeSpan.FromMilliseconds(85));
                Tick(collector, metrics, 4);
                view = metrics.Current;
                AssertResolution(85, view.Thresholds[0].Current);
                Assert.AreEqual(ThresholdState.Warning, view.Thresholds[0].State);
                Assert.AreEqual(TimeSpan.Zero, view.Thresholds[0].BreachedFor);
                AssertReads(view.Thresholds[1], 0.0, ThresholdState.Ok);
                AssertReads(view.Thresholds[2], 2.0, ThresholdState.Warning);

                // Tick 5: one passed and one failed request at 50 ms — the p95 is comfortably
                // inside, while a failed share of 50 % breaches the 10 % error rate.
                RecordIterations(collector, 1, TestStatus.Passed, TimeSpan.FromMilliseconds(50));
                RecordIterations(collector, 1, TestStatus.Failed, TimeSpan.FromMilliseconds(50));
                Tick(collector, metrics, 5);
                view = metrics.Current;
                AssertResolution(50, view.Thresholds[0].Current);
                Assert.AreEqual(ThresholdState.Ok, view.Thresholds[0].State);
                AssertBreached(view.Thresholds[1], 0.5, TimeSpan.Zero);
                AssertReads(view.Thresholds[2], 2.0, ThresholdState.Warning);

                // Tick 6: one failed request only — the error rate breach is a second old, the
                // rate falls below its minimum and begins a breach of its own, and with no
                // successful request the p95 reads zero, which is Ok.
                RecordIterations(collector, 1, TestStatus.Failed, TimeSpan.FromMilliseconds(50));
                Tick(collector, metrics, 6);
                view = metrics.Current;
                AssertReads(view.Thresholds[0], 0.0, ThresholdState.Ok);
                AssertBreached(view.Thresholds[1], 1.0, TimeSpan.FromSeconds(1));
                AssertBreached(view.Thresholds[2], 1.0, TimeSpan.Zero);

                // Tick 8, two seconds on: one failed request over two seconds is 0.5 rps — both
                // breaches continue, each timed from the sample it began at.
                RecordIterations(collector, 1, TestStatus.Failed, TimeSpan.FromMilliseconds(50));
                Tick(collector, metrics, 8);
                view = metrics.Current;
                AssertBreached(view.Thresholds[1], 1.0, TimeSpan.FromSeconds(3));
                AssertBreached(view.Thresholds[2], 0.5, TimeSpan.FromSeconds(2));

                // Tick 9: an idle second with no request at all — the error rate has no share
                // to judge and reads Ok, which ends its breach, while a rate of 0 is a stalled
                // target: the rate breach continues, three seconds old now.
                Tick(collector, metrics, 9);
                view = metrics.Current;
                Assert.AreEqual(0, view.IntervalRequestCount);
                AssertReads(view.Thresholds[0], 0.0, ThresholdState.Ok);
                AssertReads(view.Thresholds[1], 0.0, ThresholdState.Ok);
                AssertBreached(view.Thresholds[2], 0.0, TimeSpan.FromSeconds(3));

                // Tick 10: ten passed and one failed — a failed share of 1/11 (9.1 %) sits in
                // the warning band under the 10 % limit; 11 rps ends the rate breach.
                RecordIterations(collector, 10, TestStatus.Passed, TimeSpan.FromMilliseconds(50));
                RecordIterations(collector, 1, TestStatus.Failed, TimeSpan.FromMilliseconds(50));
                Tick(collector, metrics, 10);
                view = metrics.Current;
                AssertReads(view.Thresholds[1], 1.0 / 11, ThresholdState.Warning);
                AssertReads(view.Thresholds[2], 11.0, ThresholdState.Ok);

                // A view captured earlier never changes: the tick-2 view still reads its breach.
                Assert.AreNotSame(capturedAtTick2.Thresholds, view.Thresholds);
                Assert.AreEqual(ThresholdState.Breached, capturedAtTick2.Thresholds[0].State);
                Assert.AreEqual(TimeSpan.FromSeconds(1), capturedAtTick2.Thresholds[0].BreachedFor);
            })
            .Step("The mean, p99 and error-rate thresholds read the interval's own mean, p99 and failed share", context =>
            {
                var collector = CreateMeasuringCollector();
                var metrics = CreateMetrics(Declare(thresholds => thresholds
                    .ResponseTimeMean(TimeSpan.FromMilliseconds(30))
                    .ResponseTimePercentile99(TimeSpan.FromMilliseconds(400))));
                Tick(collector, metrics, 0);

                // 95 requests at 10 ms and 5 at 500 ms: a mean of 34.5 ms over the 30 ms
                // limit, a p99 of 500 ms over the 400 ms limit.
                RecordIterations(collector, 95, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                RecordIterations(collector, 5, TestStatus.Passed, TimeSpan.FromMilliseconds(500));
                Tick(collector, metrics, 1);

                var view = metrics.Current;
                Assert.AreEqual(view.IntervalLatency.ResponseTimeMean.TotalMilliseconds, view.Thresholds[0].Current);
                Assert.IsInRange(34.4, 34.6, view.Thresholds[0].Current);
                Assert.AreEqual(ThresholdState.Breached, view.Thresholds[0].State);
                AssertResolution(500, view.Thresholds[1].Current);
                Assert.AreEqual(ThresholdState.Breached, view.Thresholds[1].State);

                // The next interval: 100 requests at 10 ms — a mean of 10 ms, Ok; a p99 of
                // 10 ms, Ok — while the cumulative p99 still reads over its limit: 5 of the 200
                // requests so far took 500 ms, which is where the lifetime p99 stays.
                RecordIterations(collector, 100, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                Tick(collector, metrics, 2);

                view = metrics.Current;
                Assert.IsInRange(9.9, 10.1, view.Thresholds[0].Current);
                Assert.AreEqual(ThresholdState.Ok, view.Thresholds[0].State);
                Assert.AreEqual(ThresholdState.Ok, view.Thresholds[1].State);
                Assert.IsGreaterThan(TimeSpan.FromMilliseconds(400), view.Ok.ResponseTimePercentile99);
            })
            .Run();
    }

    [Test]
    public async Task Verify_thresholds_are_placeholders_before_measurement_and_frozen_after_it()
    {
        await Scenario()
            .Step("Before any sample every threshold is Ok with a Current of 0 and no breach — from the constructor and after the baseline Record alike", context =>
            {
                var collector = CreateMeasuringCollector();
                // Zero limits included: a Current of 0 is no breach and no warning for them either.
                var thresholds = Declare(builder => builder
                    .ErrorRate(0)
                    .RequestsPerSecond(minimum: 50)
                    .ResponseTimeMean(TimeSpan.Zero));
                var metrics = CreateMetrics(thresholds);
                AssertPlaceholder(metrics.Current.Thresholds, 3);
                Assert.AreSame(thresholds[1], metrics.Current.Thresholds[1].Threshold);

                // Requests in the baseline close no interval: still the placeholder, even for the
                // rate minimum a first judged interval would breach.
                RecordIterations(collector, 3, TestStatus.Passed, TimeSpan.FromMilliseconds(110));
                Tick(collector, metrics, 0);
                Assert.IsEmpty(metrics.Current.Samples);
                AssertPlaceholder(metrics.Current.Thresholds, 3);
            })
            .Step("Nothing is judged before measurement: warmup traffic, failures included, leaves the placeholder standing, and the first measurement interval is judged", context =>
            {
                var collector = CreateInitializedCollector();
                collector.MarkPhaseAsStarted(LoadTestPhase.Warmup, At(0));
                var metrics = CreateMetrics(Declare(thresholds => thresholds
                    .RequestsPerSecond(minimum: 50)
                    .ErrorRate(0.01)));
                Tick(collector, metrics, 0);

                // Two warmup seconds at 5 rps with a failure in each: 5 rps would breach the
                // 50 rps minimum and a failed share of 20 % the 1 % error rate — were they judged.
                for (var tick = 1; tick <= 2; tick++)
                {
                    for (var index = 0; index < 4; index++)
                        collector.RecordWarmup(TestStatus.Passed);
                    collector.RecordWarmup(TestStatus.Failed);
                    Tick(collector, metrics, tick);

                    var warmupView = metrics.Current;
                    Assert.AreEqual(LoadTestPhase.Warmup, warmupView.Phase);
                    Assert.AreEqual(5, warmupView.IntervalRequestCount);
                    Assert.AreEqual(0.2, warmupView.ErrorRate);
                    Assert.AreEqual(5.0, warmupView.IntervalRequestsPerSecond);
                    AssertPlaceholder(warmupView.Thresholds, 2);
                }

                // Measurement starts: the first measurement interval, three requests at 10 ms
                // without a failure, is judged — 3 rps breaches the minimum, the error rate is Ok.
                collector.MarkPhaseAsCompleted(LoadTestPhase.Warmup, At(2));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, At(2));
                RecordIterations(collector, 3, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                Tick(collector, metrics, 3);

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Measurement, view.Phase);
                AssertBreached(view.Thresholds[0], 3.0, TimeSpan.Zero);
                AssertReads(view.Thresholds[1], 0.0, ThresholdState.Ok);

                // A hundred requests in the next second clear the minimum and end the breach.
                RecordIterations(collector, 100, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                Tick(collector, metrics, 4);
                AssertReads(metrics.Current.Thresholds[0], 100.0, ThresholdState.Ok);
            })
            .Step("Once measurement completes the last measurement-phase states are frozen: the drained final sample and every later one re-publish them unchanged", context =>
            {
                var collector = CreateMeasuringCollector();
                var metrics = CreateMetrics(Declare(thresholds => thresholds
                    .ResponseTimePercentile95(TimeSpan.FromMilliseconds(100))
                    .RequestsPerSecond(minimum: 2)));
                Tick(collector, metrics, 0);
                RecordIterations(collector, 3, TestStatus.Passed, TimeSpan.FromMilliseconds(110));
                Tick(collector, metrics, 1);
                RecordIterations(collector, 3, TestStatus.Passed, TimeSpan.FromMilliseconds(110));
                Tick(collector, metrics, 2);

                var lastMeasured = metrics.Current.Thresholds;
                AssertResolution(110, lastMeasured[0].Current);
                Assert.AreEqual(ThresholdState.Breached, lastMeasured[0].State);
                Assert.AreEqual(TimeSpan.FromSeconds(1), lastMeasured[0].BreachedFor);
                AssertReads(lastMeasured[1], 3.0, ThresholdState.Ok);

                // Completing measurement force-refreshes the collector out of band, closing the
                // interval with these three requests before the next sample can: that sample
                // carries their deltas with an Empty latency — and the frozen states.
                RecordIterations(collector, 3, TestStatus.Passed, TimeSpan.FromMilliseconds(110));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, At(3));
                Tick(collector, metrics, 3);

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Cleanup, view.Phase);
                Assert.HasCount(3, view.Samples);
                Assert.AreEqual(3, view.Samples[2].OkDelta);
                Assert.AreSame(IntervalLatency.Empty, view.IntervalLatency);
                Assert.AreSame(lastMeasured, view.Thresholds);

                // Through cleanup and after it: still the same states, the breach still a second old.
                collector.MarkPhaseAsStarted(LoadTestPhase.Cleanup, At(3));
                Tick(collector, metrics, 4);
                Assert.AreSame(lastMeasured, metrics.Current.Thresholds);

                collector.MarkPhaseAsCompleted(LoadTestPhase.Cleanup, At(5));
                Tick(collector, metrics, 6);
                Assert.AreEqual("completed", metrics.Current.PhaseLabel);
                Assert.AreSame(lastMeasured, metrics.Current.Thresholds);
                Assert.AreEqual(TimeSpan.FromSeconds(1), metrics.Current.Thresholds[0].BreachedFor);
            })
            .Step("A measurement phase that ends before its first interval closes freezes the placeholder", context =>
            {
                var collector = CreateMeasuringCollector();
                var metrics = CreateMetrics(Declare(thresholds => thresholds.RequestsPerSecond(minimum: 50)));
                Tick(collector, metrics, 0);

                RecordIterations(collector, 2, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, At(0.5));
                Tick(collector, metrics, 1);

                // The interval that closes here would breach the minimum at 2 rps, but the phase
                // has left measurement: the placeholder stands.
                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Cleanup, view.Phase);
                Assert.AreEqual(2, Assert.ContainsSingle(view.Samples).OkDelta);
                AssertPlaceholder(view.Thresholds, 1);
            })
            .Step("A scenario without thresholds publishes an empty list on every snapshot and evaluates nothing", context =>
            {
                var collector = CreateMeasuringCollector();
                var metrics = new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
                });
                Assert.IsEmpty(metrics.Current.Thresholds);

                Tick(collector, metrics, 0);
                RecordIterations(collector, 5, TestStatus.Failed, TimeSpan.FromMilliseconds(500));
                Tick(collector, metrics, 1);
                Assert.IsEmpty(metrics.Current.Thresholds);
                Assert.AreEqual(1.0, metrics.Current.ErrorRate);

                Assert.IsEmpty(ThresholdEvaluator.EvaluateVerdict(Array.Empty<Threshold>(), collector.GetCurrentResult(true)));
                Assert.IsEmpty(new ThresholdEvaluator(Array.Empty<Threshold>()).Evaluate(LoadTestPhase.Measurement, true, 5, 1.0, 5.0, IntervalLatency.Empty, At(1)));
            })
            .Step("The evaluator and a live reading reject malformed input", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => new ThresholdEvaluator(null!));
                Assert.ThrowsExactly<ArgumentNullException>(() => ThresholdEvaluator.InitialStates(null!));
                Assert.ThrowsExactly<ArgumentNullException>(() => new ThresholdEvaluator(Array.Empty<Threshold>()).Evaluate(LoadTestPhase.Measurement, true, 0, 0.0, 0.0, null!, At(0)));

                var threshold = new Threshold(ThresholdMetric.ErrorRate, 0.1, ThresholdComparison.LessThanOrEqualTo);
                Assert.ThrowsExactly<ArgumentNullException>(() => new LiveThreshold(null!, 0.0, ThresholdState.Ok, TimeSpan.Zero));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LiveThreshold(threshold, 0.5, ThresholdState.Breached, TimeSpan.FromSeconds(-1)));
                Assert.ThrowsExactly<ArgumentException>(() => new LiveThreshold(threshold, 0.05, ThresholdState.Ok, TimeSpan.FromSeconds(1)));
            })
            .Run();
    }

    [Test]
    public async Task Verify_verdict_and_violation_message_on_cumulative_results()
    {
        await Scenario()
            .Step("The verdict reads the cumulative Ok stats and the failed share, and the failure message lists every violation in declaration order in the metric's unit", context =>
            {
                var result = SyntheticLoadSnapshots.Snapshot(ok: 976, failed: 24, okPercentile95Ms: 812);
                result.Ok.ResponseTimeMean = TimeSpan.FromMilliseconds(300);
                result.Ok.ResponseTimePercentile99 = TimeSpan.FromMilliseconds(1200);
                result.Ok.RequestsPerSecond = 42;

                var thresholds = Declare(builder => builder
                    .ResponseTimePercentile95(TimeSpan.FromMilliseconds(500))
                    .ResponseTimePercentile99(TimeSpan.FromSeconds(2))
                    .ResponseTimeMean(TimeSpan.FromMilliseconds(250))
                    .ErrorRate(0.01)
                    .RequestsPerSecond(minimum: 50));

                var verdict = ThresholdEvaluator.EvaluateVerdict(thresholds, result);
                Assert.HasCount(5, verdict);
                AssertVerdict(verdict[0], ThresholdMetric.ResponseTimePercentile95, 812.0, false);
                AssertVerdict(verdict[1], ThresholdMetric.ResponseTimePercentile99, 1200.0, true);
                AssertVerdict(verdict[2], ThresholdMetric.ResponseTimeMean, 300.0, false);
                // 24 failed of 976 + 24 requests.
                AssertVerdict(verdict[3], ThresholdMetric.ErrorRate, 0.024, false);
                AssertVerdict(verdict[4], ThresholdMetric.RequestsPerSecond, 42.0, false);
                Assert.AreSame(thresholds[3], verdict[3].Threshold);

                var violations = verdict.Where(thresholdResult => !thresholdResult.Passed).ToList();
                Assert.HasCount(4, violations);
                Assert.AreEqual("Threshold violated: p95 812 ms > 500 ms; mean 300 ms > 250 ms; error rate 2.4 % > 1 %; rps 42 < 50", new ThresholdViolationException(violations).Message);
                Assert.AreEqual("p99 1200 ms ≤ 2000 ms", verdict[1].ToString());
            })
            .Step("A value on the limit passes, and no requests give a zero error rate", context =>
            {
                var result = SyntheticLoadSnapshots.Snapshot(ok: 100, okPercentile95Ms: 500);
                result.Ok.RequestsPerSecond = 50;

                var verdict = ThresholdEvaluator.EvaluateVerdict(Declare(builder => builder
                    .ResponseTimePercentile95(TimeSpan.FromMilliseconds(500))
                    .RequestsPerSecond(minimum: 50)
                    .ErrorRate(0)), result);
                Assert.HasCount(3, verdict);
                AssertVerdict(verdict[0], ThresholdMetric.ResponseTimePercentile95, 500.0, true);
                AssertVerdict(verdict[1], ThresholdMetric.RequestsPerSecond, 50.0, true);
                AssertVerdict(verdict[2], ThresholdMetric.ErrorRate, 0.0, true);
                Assert.IsEmpty(verdict.Where(thresholdResult => !thresholdResult.Passed).ToList());

                var idle = ThresholdEvaluator.EvaluateVerdict(Declare(builder => builder.ErrorRate(0)), SyntheticLoadSnapshots.Snapshot());
                AssertVerdict(Assert.ContainsSingle(idle), ThresholdMetric.ErrorRate, 0.0, true);

                // A bare result without stats reads zero everywhere.
                var bare = new ScenarioLoadResult();
                foreach (var threshold in Declare(builder => builder.ResponseTimeMean(TimeSpan.Zero).ResponseTimePercentile95(TimeSpan.Zero).ResponseTimePercentile99(TimeSpan.Zero).ErrorRate(0).RequestsPerSecond(minimum: 0)))
                    Assert.AreEqual(0.0, ThresholdEvaluator.CumulativeCurrentOf(threshold, bare));
            })
            .Run();
    }
}
