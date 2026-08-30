using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Internals.Thresholds;
using Fuzn.TestFuzn.StandaloneRunner;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Pins the <c>run --demo</c> script hermetically: the whole scripted run plays in milliseconds
/// under a fake clock — a host whose delays advance the clock instead of waiting — into a real
/// <see cref="TestExecutionState"/> and its <see cref="ScenarioLoadCollector"/>. Everything is
/// pinned at three points of the duration range the runner accepts — the shortest run
/// (<see cref="LiveViewDemoScript.MinimumDuration"/>), the default one, and an 80 s run, a
/// duration whose ramp is long enough that a ramp-phase failure would land in a second holding
/// too few requests to absorb it — since every phase, count and window is a share of the
/// duration: the phases marked in pipeline order at the scripted times, the simulations filling
/// the plan at init, the iteration totals exactly what the simulations promise, the failure
/// schedule (only in the steady state, three distinct errors on the steps they belong to, later
/// steps skipped), the scripted incident — its degraded latency and its 503 burst breaching the
/// declared thresholds live while the completion verdict on the cumulative statistics passes —
/// the live metrics model walking the phase labels in order when sampled at 1 Hz as the console
/// manager samples and ending completed at full progress, the run's determinism, and a stop —
/// Ctrl+C or the quit key — ending the execution at the next tick with the measurement finalized
/// and cleanup still run in full. And that the final summary and the live view read the same
/// collector snapshot: an iteration failing on an early step is one failed request to both,
/// while the steps skipped after the failure count no request in their own step totals.
///
/// Every expected number here is derived from the script's own arithmetic — the rates times the
/// durations, the schedules' every-Nth over the windows they run in — never read off a run.
/// </summary>
[TestClass]
public class LiveViewDemoScriptTests : Test
{
    /// <summary>
    /// The shortest run: 18 s of load — warmup 8 % = 1.44 s at 10 rps = 14 iterations, ramp 25 %
    /// = 4.5 s from 10 to 100 rps = 55 × 4.5 = 247 (its average rate over its duration), the
    /// steady rest = 12.06 s at 100 rps = 1206. Its incident opens 40 % into the steady phase, at
    /// 0.40 × 12.06 = 4.824 s, and its degraded latency covers 0.15 × 12.06 = 1.809 s of it, its
    /// burst 0.05 × 12.06 = 0.603 s — which the 100 ms ticks round to six of them: 60 iterations,
    /// every fourth failing = 15. The trickle fails every 200th measurement iteration that falls
    /// in the steady state, which is those past the ramp's 247: 400 to 1400, 6 of the 1453.
    /// </summary>
    private static readonly DemoRun ShortestRun = new DemoRun(
        duration: TimeSpan.FromSeconds(20),
        warmupDuration: TimeSpan.FromSeconds(1.44),
        rampDuration: TimeSpan.FromSeconds(4.5),
        steadyDuration: TimeSpan.FromSeconds(12.06),
        incidentStartOffset: TimeSpan.FromSeconds(4.824),
        latencyIncidentDuration: TimeSpan.FromSeconds(1.809),
        errorBurstDuration: TimeSpan.FromSeconds(0.603),
        warmupCount: 14,
        rampCount: 247,
        steadyCount: 1206,
        burstFailureCount: 15,
        trickleFailureCount: 6);

    /// <summary>
    /// The default run: 58 s of load — warmup 4.64 s = 46 iterations, ramp 14.5 s = 55 × 14.5 =
    /// 797, steady 38.86 s = 3886. Its incident opens 0.40 × 38.86 = 15.544 s into the steady
    /// phase, its degraded latency covers 0.15 × 38.86 = 5.829 s and its burst 0.05 × 38.86 =
    /// 1.943 s, nineteen ticks of ten iterations with every fourth failing = 47. The trickle
    /// fails the steady state's multiples of 200, those past the ramp's 797: 800 to 4600, 20 of
    /// the 4683.
    /// </summary>
    private static readonly DemoRun DefaultRun = new DemoRun(
        duration: TimeSpan.FromSeconds(60),
        warmupDuration: TimeSpan.FromSeconds(4.64),
        rampDuration: TimeSpan.FromSeconds(14.5),
        steadyDuration: TimeSpan.FromSeconds(38.86),
        incidentStartOffset: TimeSpan.FromSeconds(15.544),
        latencyIncidentDuration: TimeSpan.FromSeconds(5.829),
        errorBurstDuration: TimeSpan.FromSeconds(1.943),
        warmupCount: 46,
        rampCount: 797,
        steadyCount: 3886,
        burstFailureCount: 47,
        trickleFailureCount: 20);

    /// <summary>
    /// An 80 s run: the duration the trickle's old rate gate broke at. Its ramp climbed past
    /// 40 rps at iteration 8.333 × 19.5 = 163 and reached 50 rps only at 13.333 × 19.5 = 260, so
    /// the 200th measurement iteration fell in between — a second holding about 42 requests,
    /// where one failure reads 2.4 % and turned the error-rate gauge red long before the
    /// incident. 78 s of load — warmup 6.24 s = 62 iterations, ramp 19.5 s = 55 × 19.5 = 1072,
    /// steady 52.26 s = 5226. Its incident opens 0.40 × 52.26 = 20.904 s into the steady phase,
    /// its degraded latency covers 0.15 × 52.26 = 7.839 s and its burst 0.05 × 52.26 = 2.613 s,
    /// twenty-six ticks of ten iterations with every fourth failing = 65. The trickle fails the
    /// steady state's multiples of 200, those past the ramp's 1072: 1200 to 6200, 26 of the 6298.
    /// </summary>
    private static readonly DemoRun LongRun = new DemoRun(
        duration: TimeSpan.FromSeconds(80),
        warmupDuration: TimeSpan.FromSeconds(6.24),
        rampDuration: TimeSpan.FromSeconds(19.5),
        steadyDuration: TimeSpan.FromSeconds(52.26),
        incidentStartOffset: TimeSpan.FromSeconds(20.904),
        latencyIncidentDuration: TimeSpan.FromSeconds(7.839),
        errorBurstDuration: TimeSpan.FromSeconds(2.613),
        warmupCount: 62,
        rampCount: 1072,
        steadyCount: 5226,
        burstFailureCount: 65,
        trickleFailureCount: 26);

    private static readonly IReadOnlyList<DemoRun> AllRuns = new[] { ShortestRun, DefaultRun, LongRun };

    /// <summary>
    /// How many requests a one-second live interval must hold for a single failure in it to stay
    /// inside the declared error rate: 1 / <see cref="LiveViewDemoScript.ErrorRateLimit"/> =
    /// 1 / 0.02 = 50. One failure in 49 reads 2.04 % and turns the gauge red; one in 50 reads
    /// exactly the limit, which passes.
    /// </summary>
    private const int RequestsOneFailureNeeds = 50;

    /// <summary>
    /// The fewest requests an interval carrying a trickle failure can hold, at any duration the
    /// runner accepts. The trickle fires only inside the steady state, and a live interval is a
    /// whole second give or take one of the script's 100 ms ticks (a sample lands on the first
    /// tick at or after its nominal second), so it spans more than 0.9 s. Over that span the
    /// slowest traffic it can reach back over is the ramp's last second, whose mean rate is
    /// 100 − 90 / (2 × 4.5 s) = 90 rps at the shortest run and closer to 100 at every longer
    /// one; everything after it runs at <see cref="LiveViewDemoScript.SteadyRate"/> = 100 rps.
    /// So 0.9 s × 90 rps, less the one iteration flooring a due count can shave: 80 requests —
    /// 1.6 times the <see cref="RequestsOneFailureNeeds"/> a lone failure needs.
    /// </summary>
    private const int SmallestTrickleIntervalRequestCount = 80;

    private static DateTime At(double seconds)
    {
        return SyntheticLoadSnapshots.At(seconds);
    }

    [Test]
    public async Task Verify_script_plays_the_phases_in_pipeline_order_with_the_promised_counts()
    {
        await Scenario()
            .Step("The phases land on the collector and the test result at the scripted times, and the simulations fill the plan at init", async context =>
            {
                foreach (var run in AllRuns)
                {
                    var harness = new Harness(run.Duration);
                    await harness.RunScript();

                    var result = harness.Collector.GetCurrentResult(true);
                    Assert.AreEqual(At(0), result.InitStartTime, run.Label);
                    Assert.AreEqual(run.InitEndTime, result.InitEndTime, run.Label);
                    Assert.AreEqual(run.InitEndTime, result.WarmupStartTime, run.Label);
                    Assert.AreEqual(run.MeasurementStartTime, result.WarmupEndTime, run.Label);
                    Assert.AreEqual(run.MeasurementStartTime, result.MeasurementStartTime, run.Label);
                    Assert.IsTrue(result.IsCompleted, run.Label);
                    Assert.AreNotEqual(default, result.MeasurementEndTime, run.Label);
                    Assert.AreEqual(run.CleanupStartTime, result.CleanupStartTime, run.Label);
                    Assert.AreEqual(run.EndTime, result.CleanupEndTime, run.Label);
                    Assert.AreEqual(TestStatus.Passed, result.Status, run.Label);

                    var testResult = harness.State.TestResult;
                    Assert.AreEqual(run.InitEndTime, testResult.InitEndTime, run.Label);
                    Assert.AreEqual(run.MeasurementStartTime, testResult.ExecuteStartTime, run.Label);
                    Assert.AreEqual(run.CleanupStartTime, testResult.ExecuteEndTime, run.Label);
                    Assert.AreEqual(run.CleanupStartTime, testResult.CleanupStartTime, run.Label);
                    Assert.AreEqual(run.EndTime, testResult.CleanupEndTime, run.Label);

                    // The script's phase durations are the shares of the load time: init and
                    // cleanup take their fixed second, and what is left splits 8 / 25 / the rest.
                    Assert.AreEqual(run.Duration, harness.Script.Duration, run.Label);
                    Assert.AreEqual(run.WarmupDuration, harness.Script.WarmupDuration, run.Label);
                    Assert.AreEqual(run.RampDuration, harness.Script.RampDuration, run.Label);
                    Assert.AreEqual(run.SteadyDuration, harness.Script.SteadyDuration, run.Label);
                    Assert.AreEqual(run.Duration, LiveViewDemoScript.InitDuration + run.PlannedDuration + LiveViewDemoScript.CleanupDuration, run.Label);

                    var simulations = harness.Scenario.SimulationsInternal;
                    Assert.HasCount(3, simulations, run.Label);
                    var warmup = Assert.IsInstanceOfType<FixedLoadConfiguration>(simulations[0]);
                    Assert.IsTrue(warmup.IsWarmup, run.Label);
                    Assert.AreEqual(LiveViewDemoScript.WarmupRate, warmup.Rate, run.Label);
                    Assert.AreEqual(run.WarmupDuration, warmup.Duration, run.Label);
                    var ramp = Assert.IsInstanceOfType<GradualLoadIncreaseConfiguration>(simulations[1]);
                    Assert.IsFalse(ramp.IsWarmup, run.Label);
                    Assert.AreEqual(LiveViewDemoScript.RampStartRate, ramp.StartRate, run.Label);
                    Assert.AreEqual(LiveViewDemoScript.RampEndRate, ramp.EndRate, run.Label);
                    Assert.AreEqual(run.RampDuration, ramp.Duration, run.Label);
                    var steady = Assert.IsInstanceOfType<FixedLoadConfiguration>(simulations[2]);
                    Assert.IsFalse(steady.IsWarmup, run.Label);
                    Assert.AreEqual(LiveViewDemoScript.SteadyRate, steady.Rate, run.Label);
                    Assert.AreEqual(run.SteadyDuration, steady.Duration, run.Label);
                    Assert.AreEqual(run.PlannedDuration, new SimulationPlan(simulations).PlannedDuration, run.Label);
                    Assert.HasCount(3, result.Simulations, run.Label);

                    Assert.AreEqual(ExecutionStatus.Completed, harness.State.ExecutionStatus, run.Label);
                    Assert.IsTrue(harness.State.IsConsumingCompleted, run.Label);
                    Assert.IsTrue(harness.State.IsScenarioExecutionComplete(harness.Scenario.Name), run.Label);
                    Assert.IsFalse(harness.State.CancellationToken.IsCancellationRequested, run.Label);
                }
            })
            .Step("The iteration totals are exactly what the simulations promise: 10 rps through the warmup, (10 + 100) / 2 through the ramp and 100 rps through the steady state, one result per step per iteration", async context =>
            {
                foreach (var run in AllRuns)
                {
                    var harness = new Harness(run.Duration);
                    await harness.RunScript();

                    var result = harness.Collector.GetCurrentResult(true);
                    Assert.AreEqual(run.WarmupCount, result.WarmupRequestCountOk, run.Label);
                    Assert.AreEqual(0, result.WarmupRequestCountFailed, run.Label);
                    Assert.AreEqual(run.MeasurementCount, result.RequestCount, run.Label);
                    Assert.AreEqual(run.MeasurementCount, result.Ok.RequestCount + result.Failed.RequestCount, run.Label);

                    Assert.HasCount(3, result.Steps, run.Label);
                    CollectionAssert.AreEqual(new[] { "Browse products", "Add to cart", "Place order" }, result.Steps.Keys.ToList(), run.Label);
                    foreach (var step in result.Steps.Values)
                        Assert.AreEqual(run.MeasurementCount, step.Ok.RequestCount + step.Failed.RequestCount + step.SkippedCount, run.Label + " " + step.Name);

                    // Every delay the script asked for was a positive slice no longer than a phase hook.
                    Assert.IsNotEmpty(harness.Host.Delays, run.Label);
                    foreach (var delay in harness.Host.Delays)
                        Assert.IsInRange(TimeSpan.FromTicks(1), TimeSpan.FromSeconds(1), delay, run.Label);
                }
            })
            .Step("Two runs of the script record identical numbers: the schedule and the generator, not the wall clock, decide them", async context =>
            {
                var first = new Harness(DefaultRun.Duration);
                await first.RunScript();
                var second = new Harness(DefaultRun.Duration);
                await second.RunScript();

                var firstResult = first.Collector.GetCurrentResult(true);
                var secondResult = second.Collector.GetCurrentResult(true);
                Assert.AreEqual(firstResult.Ok.RequestCount, secondResult.Ok.RequestCount);
                Assert.AreEqual(firstResult.Failed.RequestCount, secondResult.Failed.RequestCount);
                Assert.AreEqual(firstResult.Ok.ResponseTimePercentile95, secondResult.Ok.ResponseTimePercentile95);
                Assert.AreEqual(firstResult.Ok.ResponseTimeMax, secondResult.Ok.ResponseTimeMax);
                Assert.AreEqual(firstResult.Failed.ResponseTimeMean, secondResult.Failed.ResponseTimeMean);
                foreach (var stepName in firstResult.Steps.Keys)
                {
                    Assert.AreEqual(firstResult.Steps[stepName].Failed.RequestCount, secondResult.Steps[stepName].Failed.RequestCount, stepName);
                    Assert.AreEqual(firstResult.Steps[stepName].Ok.ResponseTimePercentile95, secondResult.Steps[stepName].Ok.ResponseTimePercentile95, stepName);
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_failures_are_occasional_under_load_with_three_distinct_errors_on_their_steps()
    {
        await Scenario()
            .Step("Outside the incident only steady-state measurement iterations fail, every 200th of them; with the burst's failures on top they stay a small fraction of the run", async context =>
            {
                foreach (var run in AllRuns)
                {
                    var harness = new Harness(run.Duration);
                    await harness.RunScript();

                    var result = harness.Collector.GetCurrentResult(true);
                    Assert.AreEqual(run.FailureCount, result.Failed.RequestCount, run.Label);
                    Assert.IsLessThan(run.MeasurementCount / 25, result.Failed.RequestCount, run.Label);
                    Assert.AreEqual(run.MeasurementCount - run.FailureCount, result.Ok.RequestCount, run.Label);

                    var browse = result.Steps["Browse products"];
                    var addToCart = result.Steps["Add to cart"];
                    var placeOrder = result.Steps["Place order"];
                    Assert.AreEqual(0, browse.SkippedCount, run.Label);
                    Assert.AreEqual(browse.Failed.RequestCount, addToCart.SkippedCount, run.Label);
                    Assert.AreEqual(browse.Failed.RequestCount + addToCart.Failed.RequestCount, placeOrder.SkippedCount, run.Label);
                    Assert.AreEqual(result.Failed.RequestCount, browse.Failed.RequestCount + addToCart.Failed.RequestCount + placeOrder.Failed.RequestCount, run.Label);
                }
            })
            .Step("Each step carries its one distinct error with the count of its failures: the 503 on Place order most often — its third of the trickle plus every one of the burst's — the timeout on Add to cart, the 500 on Browse products", async context =>
            {
                foreach (var run in AllRuns)
                {
                    var harness = new Harness(run.Duration);
                    await harness.RunScript();

                    var result = harness.Collector.GetCurrentResult(true);
                    var browseError = Assert.ContainsSingle(result.Steps["Browse products"].Errors);
                    Assert.AreEqual("HTTP 500 Internal Server Error", browseError.Key, run.Label);
                    Assert.AreEqual(run.BrowseFailureCount, browseError.Value.Count, run.Label);
                    var addToCartError = Assert.ContainsSingle(result.Steps["Add to cart"].Errors);
                    Assert.AreEqual("Request timed out after 2000 ms", addToCartError.Key, run.Label);
                    Assert.AreEqual(run.AddToCartFailureCount, addToCartError.Value.Count, run.Label);
                    var placeOrderError = Assert.ContainsSingle(result.Steps["Place order"].Errors);
                    Assert.AreEqual("HTTP 503 Service Unavailable", placeOrderError.Key, run.Label);
                    Assert.AreEqual(run.PlaceOrderFailureCount, placeOrderError.Value.Count, run.Label);
                    Assert.IsGreaterThan(addToCartError.Value.Count, placeOrderError.Value.Count, run.Label);
                    Assert.IsGreaterThan(browseError.Value.Count, placeOrderError.Value.Count, run.Label);
                    Assert.AreEqual(run.FailureCount, browseError.Value.Count + addToCartError.Value.Count + placeOrderError.Value.Count, run.Label);

                    // A timed-out iteration is the slowest failure: its 2 s step plus the step before it.
                    Assert.IsGreaterThan(TimeSpan.FromMilliseconds(2000), result.Failed.ResponseTimeMax, run.Label);
                    Assert.IsLessThan(TimeSpan.FromMilliseconds(2000), result.Ok.ResponseTimeMax, run.Label);
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_incident_breaches_the_declared_thresholds_live_while_the_completion_verdict_passes()
    {
        await Scenario()
            .Step("The demo scenario declares the two thresholds the incident plays against, in declaration order", context =>
            {
                var scenario = LiveViewDemoScript.CreateScenario();

                Assert.HasCount(2, scenario.Thresholds);
                Assert.AreEqual(ThresholdMetric.ResponseTimePercentile95, scenario.Thresholds[0].Metric);
                Assert.AreEqual(LiveViewDemoScript.ResponseTimePercentile95Limit.TotalMilliseconds, scenario.Thresholds[0].Limit);
                Assert.AreEqual(ThresholdComparison.LessThanOrEqualTo, scenario.Thresholds[0].Comparison);
                Assert.AreEqual(ThresholdMetric.ErrorRate, scenario.Thresholds[1].Metric);
                Assert.AreEqual(LiveViewDemoScript.ErrorRateLimit, scenario.Thresholds[1].Limit);
                Assert.AreEqual(ThresholdComparison.LessThanOrEqualTo, scenario.Thresholds[1].Comparison);
            })
            .Step("The incident's window is a share of the steady phase: it opens 40 % into it, the degraded latency lasts 15 % of it and the burst 5 %", context =>
            {
                foreach (var run in AllRuns)
                {
                    var script = new Harness(run.Duration).Script;

                    // The shares worked out by hand against each run's steady duration — 40 %,
                    // 15 % and 5 % of 12.06 s, 38.86 s and 52.26 s — not the script's own
                    // expression read back: a changed formula must fail here, not just a
                    // changed constant.
                    Assert.AreEqual(run.IncidentStartOffset, script.IncidentStartOffset, run.Label);
                    Assert.AreEqual(run.LatencyIncidentDuration, script.LatencyIncidentDuration, run.Label);
                    Assert.AreEqual(run.ErrorBurstDuration, script.ErrorBurstDuration, run.Label);
                    Assert.IsGreaterThan(script.ErrorBurstDuration, script.LatencyIncidentDuration, run.Label);
                }
            })
            .Step("Sampled at 1 Hz as the console manager samples, every interval inside the incident breaches its threshold and every interval outside it reads inside the limit: the gauges turn red while it lasts and green again once it has passed", async context =>
            {
                foreach (var run in AllRuns)
                {
                    var harness = new Harness(run.Duration);
                    var sampler = new Sampler(harness.Collector, harness.Scenario);
                    harness.Host.BeforeDelay = sampler.OnDelay;
                    await harness.RunScript();

                    var incidentStartTime = run.IncidentStartTime;
                    var latencyEndTime = run.LatencyIncidentEndTime;
                    var burstEndTime = run.ErrorBurstEndTime;

                    var latencyBreachCount = 0;
                    var burstBreachCount = 0;
                    foreach (var sample in sampler.Snapshots)
                    {
                        // A sample judges the second before it: a second lying wholly inside a
                        // window is all degraded traffic and must read Breached, a second that
                        // does not touch the window at all is all healthy traffic and must read
                        // inside the limit, and one straddling an edge holds a mix of the two —
                        // breaching or not on the share it caught, which is why only the run's
                        // total of them is pinned.
                        var intervalStartTime = sample.Key - TimeSpan.FromSeconds(1);
                        var touchesLatencyIncident = intervalStartTime < latencyEndTime && sample.Key > incidentStartTime;
                        var touchesErrorBurst = intervalStartTime < burstEndTime && sample.Key > incidentStartTime;
                        var latency = ThresholdStateOf(sample.Value, ThresholdMetric.ResponseTimePercentile95);
                        var errorRate = ThresholdStateOf(sample.Value, ThresholdMetric.ErrorRate);
                        var label = run.Label + " at " + (sample.Key - At(0));

                        if (intervalStartTime >= incidentStartTime && sample.Key <= latencyEndTime)
                            Assert.AreEqual(ThresholdState.Breached, latency, label);
                        if (intervalStartTime >= incidentStartTime && sample.Key <= burstEndTime)
                            Assert.AreEqual(ThresholdState.Breached, errorRate, label);

                        // Before the incident and once the run has recovered from it both gauges
                        // read inside their limits: the healthy steady state is well clear of
                        // them, and so are the warmup and the ramp.
                        if (!touchesLatencyIncident)
                            Assert.AreNotEqual(ThresholdState.Breached, latency, label);
                        if (!touchesErrorBurst)
                            Assert.AreNotEqual(ThresholdState.Breached, errorRate, label);

                        if (touchesLatencyIncident && latency == ThresholdState.Breached)
                            latencyBreachCount++;
                        if (touchesErrorBurst && errorRate == ThresholdState.Breached)
                            burstBreachCount++;
                    }

                    // Even the shortest run's incident is long enough to turn both gauges red for
                    // a second or more of the dashboard's samples.
                    Assert.IsGreaterThan(1, latencyBreachCount, run.Label);
                    Assert.IsGreaterThan(1, burstBreachCount, run.Label);
                }
            })
            .Step("The trickle can never turn the error-rate gauge red, at any duration: it fires only in the steady state, so every interval outside the burst that carries one holds far more requests than a lone failure needs to stay inside the limit", async context =>
            {
                // The limit the margin is measured against, read through the evaluator that judges
                // the live gauge: one failure in 50 requests reads exactly 2 % and holds, one in
                // 49 reads 2.04 % and turns the gauge red.
                var errorRate = LiveViewDemoScript.CreateScenario().Thresholds.Single(threshold => threshold.Metric == ThresholdMetric.ErrorRate);
                Assert.IsFalse(ThresholdEvaluator.IsViolated(errorRate, 1.0 / RequestsOneFailureNeeds));
                Assert.IsTrue(ThresholdEvaluator.IsViolated(errorRate, 1.0 / (RequestsOneFailureNeeds - 1)));

                foreach (var run in AllRuns)
                {
                    var harness = new Harness(run.Duration);
                    var sampler = new Sampler(harness.Collector, harness.Scenario);
                    harness.Host.BeforeDelay = sampler.OnDelay;
                    await harness.RunScript();

                    var smallestRequestCount = int.MaxValue;
                    var failureIntervalCount = 0;
                    foreach (var sample in sampler.Snapshots)
                    {
                        // The burst's own intervals are the incident, judged in the step above;
                        // and the sample after the measurement is a drained partial second the
                        // evaluator never judges — it re-publishes the last measurement states.
                        var intervalStartTime = sample.Key - TimeSpan.FromSeconds(1);
                        if (intervalStartTime < run.ErrorBurstEndTime && sample.Key > run.IncidentStartTime)
                            continue;
                        if (sample.Key >= run.CleanupStartTime)
                            continue;

                        // The baseline Record, which opens the first interval instead of closing one.
                        if (sample.Value.Samples.Count == 0)
                            continue;

                        var interval = sample.Value.Samples[^1];
                        if (interval.FailedDelta == 0)
                            continue;

                        var label = run.Label + " at " + (sample.Key - At(0));
                        failureIntervalCount++;

                        // The trickle waits for the steady state, so no interval that closed
                        // before it began carries one of its failures.
                        Assert.IsGreaterThan(run.SteadyStartTime, sample.Key, label);

                        // 200 iterations at 100 rps are two seconds apart: never two failures in
                        // the one second the gauge judges, so the interval's error rate is one
                        // over its request count.
                        Assert.AreEqual(1, interval.FailedDelta, label);
                        var requestCount = interval.OkDelta + interval.FailedDelta;
                        if (requestCount < smallestRequestCount)
                            smallestRequestCount = requestCount;
                    }

                    // Every run's trickle reaches the samples — the property below is about
                    // intervals that exist.
                    Assert.IsGreaterThan(0, failureIntervalCount, run.Label);
                    Assert.IsGreaterThanOrEqualTo(RequestsOneFailureNeeds, smallestRequestCount, run.Label);
                    Assert.IsGreaterThanOrEqualTo(SmallestTrickleIntervalRequestCount, smallestRequestCount, run.Label);
                }
            })
            .Step("The completion verdict on the cumulative statistics passes both thresholds, and the scenario finishes Passed with no exception: the demo exits 0", async context =>
            {
                foreach (var run in AllRuns)
                {
                    var harness = new Harness(run.Duration);
                    await harness.RunScript();

                    var result = harness.Collector.GetCurrentResult(true);
                    Assert.HasCount(2, result.ThresholdResults, run.Label);
                    foreach (var thresholdResult in result.ThresholdResults)
                        Assert.IsTrue(thresholdResult.Passed, run.Label + " " + thresholdResult.Threshold);

                    Assert.IsLessThan(LiveViewDemoScript.ResponseTimePercentile95Limit, result.Ok.ResponseTimePercentile95, run.Label);
                    Assert.IsLessThan(LiveViewDemoScript.ErrorRateLimit, (double)result.Failed.RequestCount / result.RequestCount, run.Label);
                    Assert.AreEqual(TestStatus.Passed, result.Status, run.Label);
                    Assert.IsNull(result.AssertWhenDoneException, run.Label);
                    Assert.IsNull(harness.State.FirstException, run.Label);

                    // The incident is on "Place order": only that step's successful requests
                    // reach past the p95 limit, the other two stay well below it.
                    Assert.IsGreaterThan(LiveViewDemoScript.ResponseTimePercentile95Limit, result.Steps["Place order"].Ok.ResponseTimeMax, run.Label);
                    Assert.IsLessThan(LiveViewDemoScript.ResponseTimePercentile95Limit, result.Steps["Browse products"].Ok.ResponseTimeMax, run.Label);
                    Assert.IsLessThan(LiveViewDemoScript.ResponseTimePercentile95Limit, result.Steps["Add to cart"].Ok.ResponseTimeMax, run.Label);
                }
            })
            .Step("A violated threshold would fail the scenario as an assert failure does — the execution manager's pathway, mirrored", async context =>
            {
                var harness = new Harness(ShortestRun.Duration);
                // A mean response time of one millisecond no run can hold: the verdict must report it.
                new ThresholdsBuilder(harness.Scenario.Thresholds).ResponseTimeMean(TimeSpan.FromMilliseconds(1));

                await harness.RunScript();

                var result = harness.Collector.GetCurrentResult(true);
                Assert.HasCount(3, result.ThresholdResults);
                var violation = Assert.ContainsSingle(result.ThresholdResults.Where(thresholdResult => !thresholdResult.Passed));
                Assert.AreEqual(ThresholdMetric.ResponseTimeMean, violation.Threshold.Metric);
                Assert.AreEqual(TestStatus.Failed, result.Status);
                Assert.AreEqual(TestStatus.Failed, harness.State.TestResult.Status);
                var exception = Assert.IsInstanceOfType<ThresholdViolationException>(harness.State.FirstException);
                Assert.AreSame(exception, result.AssertWhenDoneException);
                Assert.ContainsSingle(exception.Violations);
            })
            .Run();
    }

    [Test]
    public async Task Verify_live_model_walks_the_phase_labels_in_order_and_ends_completed_at_full_progress()
    {
        await Scenario()
            .Step("Sampled at 1 Hz as the console manager samples, the model shows init, the warmup, both measurement simulations and cleanup in order, and the final Record after cleanup lands completed", async context =>
            {
                var run = DefaultRun;
                var harness = new Harness(run.Duration);
                var sampler = new Sampler(harness.Collector, harness.Scenario);
                harness.Host.BeforeDelay = sampler.OnDelay;
                await harness.RunScript();

                // The one final force-refreshed Record the console manager makes after cleanup.
                sampler.Sample(harness.Host.UtcNow);

                CollectionAssert.AreEqual(new[]
                {
                    "init",
                    "warmup: Fixed Load 10 rps",
                    "sim 1/2: Gradual Load 10→100 rps",
                    "sim 2/2: Fixed Load 100 rps",
                    "cleanup",
                    "completed"
                }, sampler.PhaseLabels);

                var final = sampler.Snapshots[run.EndTime];
                Assert.IsTrue(final.IsCompleted);
                Assert.AreEqual("completed", final.PhaseLabel);
                Assert.AreEqual(1.0, final.ProgressFraction);
                Assert.AreEqual(TimeSpan.Zero, final.EstimatedTimeRemaining);
                Assert.AreEqual(run.PlannedDuration, final.PlannedDuration);
                Assert.AreEqual(run.Duration, final.Duration);
                Assert.AreEqual(TestStatus.Passed, final.Status);
                Assert.IsNull(final.StatusDetail);
                Assert.AreEqual(run.WarmupCount, final.WarmupRequestCountOk);
                Assert.AreEqual(run.MeasurementCount, final.RequestCountOk + final.RequestCountFailed);
                Assert.HasCount(3, final.Errors);
                CollectionAssert.AreEqual(new[] { "Browse products", "Add to cart", "Place order" }, final.Steps.Select(step => step.Name).ToList());
            })
            .Step("The per-second deltas follow the simulations: 10 through the warmup, climbing through the ramp, 100 through the steady state; progress tracks the planned time", async context =>
            {
                var run = DefaultRun;
                var harness = new Harness(run.Duration);
                var sampler = new Sampler(harness.Collector, harness.Scenario);
                harness.Host.BeforeDelay = sampler.OnDelay;
                await harness.RunScript();

                // The seconds that lie inside one simulation, away from the edges a phase starts
                // in the middle of: a warmup second delivers its 10 iterations, a ramp second
                // more than the one before it, a steady second its 100.
                foreach (var sampleTime in sampler.SampleTimesIn(run.InitEndTime, run.MeasurementStartTime))
                    Assert.AreEqual(LiveViewDemoScript.WarmupRate, sampler.RequestDeltaAt(sampleTime), "warmup at " + (sampleTime - At(0)));
                var rampSampleTimes = sampler.SampleTimesIn(run.MeasurementStartTime, run.SteadyStartTime);
                for (var index = 1; index < rampSampleTimes.Count; index++)
                    Assert.IsGreaterThan(sampler.RequestDeltaAt(rampSampleTimes[index - 1]), sampler.RequestDeltaAt(rampSampleTimes[index]), "ramp at " + (rampSampleTimes[index] - At(0)));
                foreach (var sampleTime in sampler.SampleTimesIn(run.SteadyStartTime, run.CleanupStartTime))
                    Assert.AreEqual(LiveViewDemoScript.SteadyRate, sampler.RequestDeltaAt(sampleTime), "steady at " + (sampleTime - At(0)));

                // Progress is the load time elapsed over the planned time: zero at the first
                // sample, never going backwards, and full from the moment cleanup starts.
                Assert.AreEqual(0.0, sampler.Snapshots[At(1)].ProgressFraction);
                Assert.AreEqual(1.0, sampler.Snapshots[run.CleanupStartTime].ProgressFraction);
                var previousProgress = 0.0;
                foreach (var sampleTime in sampler.Snapshots.Keys.OrderBy(sampleTime => sampleTime))
                {
                    var progress = sampler.Snapshots[sampleTime].ProgressFraction.GetValueOrDefault();
                    Assert.IsInRange(previousProgress, 1.0, progress, "progress at " + (sampleTime - At(0)));
                    previousProgress = progress;
                }

                foreach (var sampleTime in sampler.SampleTimesIn(run.MeasurementStartTime, run.CleanupStartTime))
                    Assert.AreEqual((sampleTime - run.InitEndTime).TotalSeconds / run.PlannedDuration.TotalSeconds, sampler.Snapshots[sampleTime].ProgressFraction.GetValueOrDefault(), 1e-9, "progress at " + (sampleTime - At(0)));

                // Every measurement interval closed with successful requests, so the p95 sparkline
                // has a point for each — except the cleanup sample's: completing the measurement
                // force-refreshes the collector, which closes the interval first, as on a real
                // run. The step means keep the steps' order of unloaded times, since every
                // request's factors apply to all steps alike.
                foreach (var sampleTime in sampler.SampleTimesIn(run.MeasurementStartTime, run.CleanupStartTime))
                    Assert.IsGreaterThan(TimeSpan.Zero, sampler.Snapshots[sampleTime].Samples[^1].ResponseTimePercentile95, "measurement at " + (sampleTime - At(0)));
                Assert.AreEqual(TimeSpan.Zero, sampler.Snapshots[run.CleanupStartTime].Samples[^1].ResponseTimePercentile95);

                var steps = sampler.Snapshots[run.CleanupStartTime].Steps;
                Assert.IsGreaterThan(steps[0].ResponseTimeMean, steps[1].ResponseTimeMean);
                Assert.IsGreaterThan(steps[1].ResponseTimeMean, steps[2].ResponseTimeMean);
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_stop_ends_the_execution_at_the_next_tick_and_cleanup_still_runs_in_full()
    {
        // The shortest run's ramp starts 2.44 s in and ticks every tenth of a second, so a stop
        // asked for at five seconds lands on the tick at 5.04 — 2.6 s into the ramp, where
        // 10 × 2.6 + 45 × 2.6² / 4.5 = 93.6 iterations have come due.
        var stopTime = At(5.04);
        const int stoppedIterationCount = 93;

        await Scenario()
            .Step("Ctrl+C mid-ramp: nothing more is recorded, the measurement is finalized as stopped, consuming is not marked complete, no verdict is evaluated, and cleanup takes its full second", async context =>
            {
                var harness = new Harness(ShortestRun.Duration);
                harness.StopAt(stopTime, () => harness.TestFramework.Cancel());

                await harness.Script.Init();
                await harness.Script.Execute();

                harness.AssertStoppedLikeCtrlC();
                Assert.AreEqual(stopTime, harness.Host.UtcNow);
                var result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(stoppedIterationCount, result.RequestCount);
                Assert.AreEqual(ShortestRun.WarmupCount, result.WarmupRequestCountOk);
                Assert.AreEqual(ShortestRun.MeasurementStartTime, result.MeasurementStartTime);
                Assert.IsTrue(result.IsCompleted);
                Assert.AreNotEqual(default, result.MeasurementEndTime);
                Assert.AreEqual(stopTime, harness.State.TestResult.ExecuteEndTime);
                Assert.IsFalse(harness.State.IsConsumingCompleted);
                Assert.IsEmpty(result.ThresholdResults);

                await harness.Script.Cleanup();

                result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(stoppedIterationCount, result.RequestCount);
                Assert.AreEqual(stopTime, result.CleanupStartTime);
                Assert.AreEqual(stopTime + LiveViewDemoScript.CleanupDuration, result.CleanupEndTime);
                Assert.AreEqual(stopTime + LiveViewDemoScript.CleanupDuration, harness.Host.UtcNow);

                var metrics = new ScenarioLiveMetrics(harness.Scenario.Name, harness.Scenario.SimulationsInternal.ToArray());
                metrics.Record(result, harness.Host.UtcNow);
                Assert.AreEqual("completed", metrics.Current.PhaseLabel);
                Assert.IsTrue(metrics.Current.IsCompleted);
                Assert.AreEqual(stopTime + LiveViewDemoScript.CleanupDuration - At(0), metrics.Current.Duration);
            })
            .Step("The quit key's stop request lands the same way", async context =>
            {
                var harness = new Harness(ShortestRun.Duration);
                Task? stopRequest = null;
                harness.StopAt(stopTime, () => stopRequest = harness.State.RequestStop());

                await harness.Script.Init();
                await harness.Script.Execute();
                Assert.IsNotNull(stopRequest);
                await stopRequest;

                harness.AssertStoppedLikeCtrlC();
                Assert.AreEqual(stopTime, harness.Host.UtcNow);
                Assert.AreEqual(stoppedIterationCount, harness.Collector.GetCurrentResult(true).RequestCount);
                Assert.IsTrue(harness.Collector.GetCurrentResult(true).IsCompleted);
                // The request's callback sets the status Stopped off the script's thread; the
                // finalization must not mark consuming completed in the window before it runs.
                Assert.IsFalse(harness.State.IsConsumingCompleted);

                await harness.Script.Cleanup();
                Assert.AreEqual(stopTime + LiveViewDemoScript.CleanupDuration, harness.Collector.GetCurrentResult(true).CleanupEndTime);
            })
            .Step("A stop during init ends init with the cancellation, as a hook observing the token would; no simulations are added, and cleanup still marks its phase", async context =>
            {
                var harness = new Harness(ShortestRun.Duration);
                harness.StopAt(At(0), () => harness.TestFramework.Cancel());

                await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await harness.Script.Init());

                harness.AssertStoppedLikeCtrlC();
                var result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(At(0), result.InitStartTime);
                Assert.AreEqual(default, result.InitEndTime);
                Assert.IsEmpty(harness.Scenario.SimulationsInternal);
                Assert.AreEqual(At(0), harness.Host.UtcNow);

                await harness.Script.Cleanup();
                result = harness.Collector.GetCurrentResult(true);
                Assert.AreEqual(At(0), result.CleanupStartTime);
                Assert.AreEqual(At(1), result.CleanupEndTime);
                Assert.AreEqual(0, result.RequestCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_summary_and_live_view_read_the_same_collector_counts_and_a_skipped_step_is_not_a_request()
    {
        await Scenario()
            .Step("One iteration failing on the first step with the later steps skipped — as the step handler leaves it — is one failed request to the summary's source and to the live model alike; the skipped steps count no request of their own", async context =>
            {
                var harness = new Harness(ShortestRun.Duration);
                harness.RecordIterationFailingOnFirstStep();

                // The summary reads the collector's plain GetCurrentResult (ConsoleWriter.WriteSummaryLoad)
                // and prints RequestCount, Ok.RequestCount and Failed.RequestCount as its Requests table
                // (BaseStandaloneRunnerAdapter.WriteSummary); the live model reads the same fields of a
                // force-refreshed result.
                var summarySource = harness.Collector.GetCurrentResult();
                Assert.AreEqual(1, summarySource.RequestCount);
                Assert.AreEqual(0, summarySource.Ok.RequestCount);
                Assert.AreEqual(1, summarySource.Failed.RequestCount);

                var metrics = new ScenarioLiveMetrics(harness.Scenario.Name, harness.Scenario.SimulationsInternal.ToArray());
                metrics.Record(harness.Collector.GetCurrentResult(true), At(1));
                Assert.AreEqual(0, metrics.Current.RequestCountOk);
                Assert.AreEqual(1, metrics.Current.RequestCountFailed);

                // Per step the summary prints Ok + Failed as the step's total (StepLoadResult.RequestCount):
                // the failed step ran once; a skipped step ran nothing and is counted as skipped instead.
                var browse = summarySource.Steps["Browse products"];
                Assert.AreEqual(1, browse.RequestCount);
                Assert.AreEqual(1, browse.Failed.RequestCount);
                Assert.AreEqual(0, browse.SkippedCount);
                var browseError = Assert.ContainsSingle(browse.Errors);
                Assert.AreEqual("HTTP 500 Internal Server Error", browseError.Key);
                foreach (var stepName in new[] { "Add to cart", "Place order" })
                {
                    var step = summarySource.Steps[stepName];
                    Assert.AreEqual(0, step.RequestCount, stepName);
                    Assert.AreEqual(0, step.Failed.RequestCount, stepName);
                    Assert.AreEqual(1, step.SkippedCount, stepName);
                    Assert.IsNull(step.Errors, stepName);
                }
            })
            .Step("After the whole script the summary's snapshot is the very one the final live Record used, and each step's total is the iterations minus those skipped at that step", async context =>
            {
                foreach (var run in AllRuns)
                {
                    var harness = new Harness(run.Duration);
                    await harness.RunScript();

                    // The final Record after cleanup force-refreshes; the summary's plain GetCurrentResult
                    // right after hits the cache: the same snapshot object.
                    var finalRecord = harness.Collector.GetCurrentResult(true);
                    var summarySource = harness.Collector.GetCurrentResult();
                    Assert.AreSame(finalRecord, summarySource, run.Label);

                    var metrics = new ScenarioLiveMetrics(harness.Scenario.Name, harness.Scenario.SimulationsInternal.ToArray());
                    metrics.Record(finalRecord, run.EndTime);
                    var live = metrics.Current;
                    Assert.AreEqual(run.MeasurementCount, summarySource.RequestCount, run.Label);
                    Assert.AreEqual(summarySource.RequestCount, live.RequestCountOk + live.RequestCountFailed, run.Label);
                    Assert.AreEqual(summarySource.Ok.RequestCount, live.RequestCountOk, run.Label);
                    Assert.AreEqual(summarySource.Failed.RequestCount, live.RequestCountFailed, run.Label);
                    Assert.AreEqual(run.MeasurementCount - run.FailureCount, live.RequestCountOk, run.Label);
                    Assert.AreEqual(run.FailureCount, live.RequestCountFailed, run.Label);

                    var browse = summarySource.Steps["Browse products"];
                    var addToCart = summarySource.Steps["Add to cart"];
                    var placeOrder = summarySource.Steps["Place order"];
                    Assert.AreEqual(run.MeasurementCount, browse.RequestCount, run.Label);
                    Assert.AreEqual(0, browse.SkippedCount, run.Label);
                    Assert.AreEqual(run.MeasurementCount - run.BrowseFailureCount, addToCart.RequestCount, run.Label);
                    Assert.AreEqual(run.BrowseFailureCount, addToCart.SkippedCount, run.Label);
                    Assert.AreEqual(run.MeasurementCount - run.BrowseFailureCount - run.AddToCartFailureCount, placeOrder.RequestCount, run.Label);
                    Assert.AreEqual(run.BrowseFailureCount + run.AddToCartFailureCount, placeOrder.SkippedCount, run.Label);
                    foreach (var step in summarySource.Steps.Values)
                        Assert.AreEqual(run.MeasurementCount, step.RequestCount + step.SkippedCount, run.Label + " " + step.Name);
                }
            })
            .Run();
    }

    /// <summary>The live state of one metric's threshold in a snapshot.</summary>
    private static ThresholdState ThresholdStateOf(LiveMetricsSnapshot snapshot, ThresholdMetric metric)
    {
        return snapshot.Thresholds.Single(threshold => threshold.Threshold.Metric == metric).State;
    }

    /// <summary>
    /// What one demo duration promises, all of it arithmetic on the script's constants: the phase
    /// durations as shares of the load time, the iteration counts as a rate over a duration, and
    /// the failures as the schedules' every-Nth over the windows they run in.
    /// </summary>
    private sealed class DemoRun
    {
        public DemoRun(TimeSpan duration, TimeSpan warmupDuration, TimeSpan rampDuration, TimeSpan steadyDuration,
            TimeSpan incidentStartOffset, TimeSpan latencyIncidentDuration, TimeSpan errorBurstDuration,
            int warmupCount, int rampCount, int steadyCount, int burstFailureCount, int trickleFailureCount)
        {
            Duration = duration;
            WarmupDuration = warmupDuration;
            RampDuration = rampDuration;
            SteadyDuration = steadyDuration;
            IncidentStartOffset = incidentStartOffset;
            LatencyIncidentDuration = latencyIncidentDuration;
            ErrorBurstDuration = errorBurstDuration;
            WarmupCount = warmupCount;
            RampCount = rampCount;
            SteadyCount = steadyCount;
            BurstFailureCount = burstFailureCount;
            TrickleFailureCount = trickleFailureCount;
        }

        /// <summary>What the whole run takes.</summary>
        public TimeSpan Duration { get; }

        public TimeSpan WarmupDuration { get; }

        public TimeSpan RampDuration { get; }

        public TimeSpan SteadyDuration { get; }

        /// <summary>How far into the steady phase the incident opens: its 40 % share of this run's steady duration.</summary>
        public TimeSpan IncidentStartOffset { get; }

        /// <summary>How long the degraded latency lasts from there: the 15 % share.</summary>
        public TimeSpan LatencyIncidentDuration { get; }

        /// <summary>How long the 503 burst lasts from there: the 5 % share.</summary>
        public TimeSpan ErrorBurstDuration { get; }

        /// <summary>The warmup iterations: its rate over its duration.</summary>
        public int WarmupCount { get; }

        /// <summary>The ramp's iterations: its average rate over its duration.</summary>
        public int RampCount { get; }

        /// <summary>The steady state's iterations: its rate over its duration.</summary>
        public int SteadyCount { get; }

        /// <summary>The burst's failures: every fourth iteration of the ticks that fall inside its window.</summary>
        public int BurstFailureCount { get; }

        /// <summary>The trickle's failures: the multiples of 200 the measurement count reaches inside the steady state — the ramp's own are passed over.</summary>
        public int TrickleFailureCount { get; }

        /// <summary>Where the scripted incident opens on the clock.</summary>
        public DateTime IncidentStartTime => SteadyStartTime + IncidentStartOffset;

        /// <summary>Where the degraded latency recovers.</summary>
        public DateTime LatencyIncidentEndTime => IncidentStartTime + LatencyIncidentDuration;

        /// <summary>Where the 503 burst recovers.</summary>
        public DateTime ErrorBurstEndTime => IncidentStartTime + ErrorBurstDuration;

        /// <summary>The run in an assertion message, so a failure names the duration it came from.</summary>
        public string Label => Duration.TotalSeconds + " s";

        public int MeasurementCount => RampCount + SteadyCount;

        public int FailureCount => BurstFailureCount + TrickleFailureCount;

        /// <summary>The trickle cycles its three errors in turn, so the 500 on "Browse products", the last of them, takes every third failure.</summary>
        public int BrowseFailureCount => TrickleFailureCount / 3;

        /// <summary>The timeout on "Add to cart" is the second of the cycle.</summary>
        public int AddToCartFailureCount => (TrickleFailureCount + 1) / 3;

        /// <summary>The 503 on "Place order" is the first of the cycle, with every one of the burst's failures on top.</summary>
        public int PlaceOrderFailureCount => (TrickleFailureCount + 2) / 3 + BurstFailureCount;

        /// <summary>What the dashboard plans: the warmup and both measurement simulations.</summary>
        public TimeSpan PlannedDuration => WarmupDuration + RampDuration + SteadyDuration;

        public DateTime InitEndTime => SyntheticLoadSnapshots.BaseTime + LiveViewDemoScript.InitDuration;

        public DateTime MeasurementStartTime => InitEndTime + WarmupDuration;

        public DateTime SteadyStartTime => MeasurementStartTime + RampDuration;

        public DateTime CleanupStartTime => SteadyStartTime + SteadyDuration;

        public DateTime EndTime => CleanupStartTime + LiveViewDemoScript.CleanupDuration;
    }

    /// <summary>
    /// The demo script over a real execution state — the demo scenario, a fake standalone-style
    /// adapter whose token Ctrl+C would cancel, and the fake clock host — plus the state's
    /// collector for the scenario.
    /// </summary>
    private sealed class Harness
    {
        public FakeClockHost Host { get; } = new FakeClockHost();

        public FakeTestFrameworkAdapter TestFramework { get; } = new FakeTestFrameworkAdapter(new List<string>());

        public Scenario Scenario { get; }

        public TestExecutionState State { get; }

        public ScenarioLoadCollector Collector { get; }

        public LiveViewDemoScript Script { get; }

        public Harness(TimeSpan duration)
        {
            Scenario = LiveViewDemoScript.CreateScenario();
            State = new TestExecutionState(new TestSession("live-view-demo-script-tests"));
            State.Init(TestFramework, new FakeTest(), Scenario);
            Collector = State.LoadCollectors[Scenario.Name];
            Script = new LiveViewDemoScript(State, Host, duration);
        }

        /// <summary>The whole script, phase after phase, as the demo runs it.</summary>
        public async Task RunScript()
        {
            await Script.Init();
            await Script.Execute();
            await Script.Cleanup();
        }

        /// <summary>
        /// Runs the stop once, on the first tick at or after the given time: the script's ticks
        /// follow its phases' starts, not the whole seconds.
        /// </summary>
        public void StopAt(DateTime time, Action stop)
        {
            var isStopped = false;
            Host.BeforeDelay = now =>
            {
                if (isStopped || now < time)
                    return;

                isStopped = true;
                stop();
            };
        }

        /// <summary>
        /// Records one measurement iteration shaped as the step handler leaves it when the first
        /// step throws — that step Failed with its exception, every later step Skipped, the
        /// scenario status Failed — as the scenario message handler records it.
        /// </summary>
        public void RecordIterationFailingOnFirstStep()
        {
            var iterationResult = new IterationResult();
            iterationResult.ExecuteStartTime = SyntheticLoadSnapshots.BaseTime;
            iterationResult.ExecuteEndTime = SyntheticLoadSnapshots.BaseTime + TimeSpan.FromMilliseconds(80);

            for (var index = 0; index < LiveViewDemoScript.Steps.Count; index++)
            {
                var stepResult = new StepStandardResult();
                stepResult.Name = LiveViewDemoScript.Steps[index].Name;
                stepResult.Id = LiveViewDemoScript.Steps[index].Id;
                if (index == 0)
                {
                    stepResult.Status = StepStatus.Failed;
                    stepResult.Exception = new InvalidOperationException("HTTP 500 Internal Server Error");
                    stepResult.Duration = TimeSpan.FromMilliseconds(80);
                }
                else
                {
                    stepResult.Status = StepStatus.Skipped;
                    stepResult.Duration = TimeSpan.Zero;
                }

                iterationResult.StepResults.Add(stepResult.Name, stepResult);
            }

            Collector.RecordMeasurement(TestStatus.Failed, iterationResult);
        }

        /// <summary>The state as Ctrl+C leaves it — the same shape the console manager tests pin for the quit key.</summary>
        public void AssertStoppedLikeCtrlC()
        {
            Assert.AreEqual(ExecutionStatus.Stopped, State.ExecutionStatus);
            Assert.IsTrue(State.CancellationToken.IsCancellationRequested);
            Assert.IsNull(State.ExecutionStoppedReason);
            Assert.IsNull(State.FirstException);
        }
    }

    /// <summary>
    /// Samples the collector the way the console manager does: once per second on the fake
    /// clock, force-refreshed, the model created at the first sample after init has completed
    /// and the init placeholder's label before that — and keeps every snapshot by its time.
    /// </summary>
    private sealed class Sampler
    {
        private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

        private readonly ScenarioLoadCollector _collector;
        private readonly Scenario _scenario;
        private ScenarioLiveMetrics? _metrics;
        private DateTime? _nextSampleTime;

        public Sampler(ScenarioLoadCollector collector, Scenario scenario)
        {
            _collector = collector;
            _scenario = scenario;
        }

        /// <summary>Every snapshot taken, by the time it was taken at.</summary>
        public Dictionary<DateTime, LiveMetricsSnapshot> Snapshots { get; } = new Dictionary<DateTime, LiveMetricsSnapshot>();

        /// <summary>The phase labels seen, each once, in the order they first appeared.</summary>
        public List<string> PhaseLabels { get; } = new List<string>();

        public void OnDelay(DateTime now)
        {
            if (_nextSampleTime == null)
                _nextSampleTime = now;
            if (now < _nextSampleTime.Value)
                return;

            Sample(now);
            _nextSampleTime = _nextSampleTime.Value + SampleInterval;
        }

        public void Sample(DateTime now)
        {
            var snapshot = _collector.GetCurrentResult(true);
            if (_metrics == null)
            {
                if (snapshot.InitEndTime == default)
                {
                    AddPhaseLabel("init");
                    return;
                }

                _metrics = new ScenarioLiveMetrics(_scenario.Name, _scenario.SimulationsInternal.ToArray(), _scenario.Thresholds.ToArray());
            }

            _metrics.Record(snapshot, now);
            Snapshots[now] = _metrics.Current;
            AddPhaseLabel(_metrics.Current.PhaseLabel);
        }

        /// <summary>
        /// The sample times whose whole second lies inside the given span, in order: the samples
        /// away from a phase's edges, where the second before the sample is all that phase's
        /// traffic. The sample taken at the end of the span is left out — the phase ends between
        /// the samples, so that one's second is a short one.
        /// </summary>
        public List<DateTime> SampleTimesIn(DateTime startTime, DateTime endTime)
        {
            return Snapshots.Keys.Where(sampleTime => sampleTime - SampleInterval >= startTime && sampleTime < endTime).OrderBy(sampleTime => sampleTime).ToList();
        }

        /// <summary>The requests (ok and failed) recorded in the interval that closed at the given sample time.</summary>
        public int RequestDeltaAt(DateTime sampleTime)
        {
            var sample = Snapshots[sampleTime].Samples[^1];
            return sample.OkDelta + sample.FailedDelta;
        }

        private void AddPhaseLabel(string phaseLabel)
        {
            if (PhaseLabels.Count == 0 || PhaseLabels[PhaseLabels.Count - 1] != phaseLabel)
                PhaseLabels.Add(phaseLabel);
        }
    }

    /// <summary>
    /// An <see cref="ILiveViewHost"/> with a fake clock: a delay advances the clock by its interval
    /// and returns at once — or returns without advancing when the token is already cancelled,
    /// as the production delay returns at once then — after reporting the time it starts at to
    /// the test's hook, which is where a test samples or stops the run. The script never touches
    /// the terminal, so the host has none.
    /// </summary>
    private sealed class FakeClockHost : ILiveViewHost
    {
        public DateTime UtcNow { get; set; } = SyntheticLoadSnapshots.BaseTime;

        /// <summary>Called with the clock's time at the start of every delay, before it advances.</summary>
        public Action<DateTime>? BeforeDelay { get; set; }

        /// <summary>Every interval delayed by, in order.</summary>
        public List<TimeSpan> Delays { get; } = new List<TimeSpan>();

        public TerminalCapabilities DetectCapabilities()
        {
            throw new InvalidOperationException("The demo script never detects capabilities.");
        }

        public ITerminalWriter CreateTerminalWriter()
        {
            throw new InvalidOperationException("The demo script never writes to the terminal.");
        }

        public ITerminalReader CreateTerminalReader()
        {
            throw new InvalidOperationException("The demo script never reads keys.");
        }

        public Task Delay(TimeSpan interval, CancellationToken cancellationToken)
        {
            if (BeforeDelay != null)
                BeforeDelay(UtcNow);

            if (cancellationToken.IsCancellationRequested)
                return Task.CompletedTask;

            Delays.Add(interval);
            UtcNow = UtcNow + interval;
            return Task.CompletedTask;
        }
    }
}
