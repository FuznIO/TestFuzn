using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Drives <see cref="ScenarioLiveMetrics"/> through a REAL <see cref="ScenarioLoadCollector"/>
/// (hermetic, in-memory) the way the dashboard wiring must: force-refreshed snapshots
/// (<c>GetCurrentResult(true)</c>) at 1 Hz-style ticks with advancing timestamps. This proves
/// the model's contracts hold through the real producer path — live warmup deltas, a continuous
/// series across the warmup-to-measurement transition, the final completed tick, per-interval
/// p95 and per-step rates — not just against hand-built snapshots.
/// </summary>
[TestClass]
public class ScenarioLiveMetricsCollectorTests : Test
{
    private const string CheckoutStepName = "Checkout step";

    private static ScenarioLoadCollector CreateCollector()
    {
        var scenario = new Scenario("Checkout flow");
        scenario.Steps.Add(new Step { Name = CheckoutStepName, Id = "checkout-step" });
        return new ScenarioLoadCollector(scenario);
    }

    private static ScenarioLiveMetrics CreateMetrics()
    {
        return new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[]
        {
            new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)) { IsWarmup = true },
            new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
        });
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

    [Test]
    public async Task Verify_live_warmup_deltas_through_a_real_collector()
    {
        await Scenario()
            .Step("Force-refreshed snapshots show warmup activity live, not frozen init zeros", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Warmup, SyntheticLoadSnapshots.At(0));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(0));

                for (var tick = 1; tick <= 3; tick++)
                {
                    for (var iteration = 0; iteration < 5; iteration++)
                        collector.RecordWarmup(TestStatus.Passed);

                    metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(tick));
                }

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Warmup, view.Phase);
                Assert.AreEqual(15, view.WarmupRequestCountOk);
                Assert.HasCount(3, view.Samples);
                foreach (var sample in view.Samples)
                    Assert.AreEqual(5, sample.OkDelta);
            })
            .Step("The series stays continuous into measurement with no false spike at the transition", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Warmup, SyntheticLoadSnapshots.At(0));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(0));

                for (var tick = 1; tick <= 2; tick++)
                {
                    for (var iteration = 0; iteration < 5; iteration++)
                        collector.RecordWarmup(TestStatus.Passed);

                    metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(tick));
                }

                collector.MarkPhaseAsCompleted(LoadTestPhase.Warmup, SyntheticLoadSnapshots.At(2));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(2));
                RecordIterations(collector, 6, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(3));

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Measurement, view.Phase);
                Assert.AreEqual(10, view.WarmupRequestCountOk);
                Assert.AreEqual(6, view.RequestCountOk);
                Assert.HasCount(3, view.Samples);
                Assert.AreEqual(5.0, view.OkDeltaSeries[0]);
                Assert.AreEqual(5.0, view.OkDeltaSeries[1]);
                // The transition tick counts only the six new measurement requests — the ten
                // warmup requests already ticked live and must not spike here again.
                Assert.AreEqual(6.0, view.OkDeltaSeries[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_completed_phase_and_frozen_duration_through_a_real_collector()
    {
        await Scenario()
            .Step("The final force-refreshed Record after cleanup lands completed and freezes the duration", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(1));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(1));
                RecordIterations(collector, 3, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(2));

                collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(10));
                collector.MarkPhaseAsStarted(LoadTestPhase.Cleanup, SyntheticLoadSnapshots.At(10));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Cleanup, SyntheticLoadSnapshots.At(12));

                // The wiring contract's final tick: one more Record with a force-refreshed
                // snapshot once cleanup has completed.
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(13));

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Cleanup, view.Phase);
                Assert.AreEqual("completed", view.PhaseLabel);
                Assert.IsTrue(view.IsCompleted);
                Assert.AreEqual(TimeSpan.FromSeconds(12), view.Duration);
            })
            .Step("The duration stays frozen on ticks long after cleanup ended", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(1));
                RecordIterations(collector, 3, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(10));
                collector.MarkPhaseAsStarted(LoadTestPhase.Cleanup, SyntheticLoadSnapshots.At(10));
                collector.MarkPhaseAsCompleted(LoadTestPhase.Cleanup, SyntheticLoadSnapshots.At(12));

                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(13));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(500));

                var view = metrics.Current;
                Assert.AreEqual("completed", view.PhaseLabel);
                Assert.AreEqual(TimeSpan.FromSeconds(12), view.Duration);
            })
            .Run();
    }

    [Test]
    public async Task Verify_interval_p95_reacts_to_latency_shifts_within_one_tick()
    {
        await Scenario()
            .Step("A latency shift shows in the interval p95 on the next tick while the cumulative p95 barely moves", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(0));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(0));

                RecordIterations(collector, 95, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(1));

                RecordIterations(collector, 5, TestStatus.Passed, TimeSpan.FromMilliseconds(500));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(2));

                var view = metrics.Current;
                Assert.HasCount(2, view.Samples);
                // HdrHistogram stores values with 3 significant digits, so allow ~0.1% slack.
                Assert.IsInRange(9.9, 10.2, view.ResponseTimePercentile95Series[0]);
                Assert.IsInRange(499.0, 501.0, view.ResponseTimePercentile95Series[1]);
                // 95 of the 100 requests took 10 ms, so the lifetime p95 stays at the old
                // latency — a sparkline fed this cumulative value would never show the shift.
                Assert.IsLessThanOrEqualTo(TimeSpan.FromMilliseconds(11), view.Ok.ResponseTimePercentile95);
            })
            .Step("An idle interval reports zero interval p95 while the cumulative p95 is retained", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(0));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(0));

                RecordIterations(collector, 10, TestStatus.Passed, TimeSpan.FromMilliseconds(20));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(1));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(2));

                var view = metrics.Current;
                Assert.HasCount(2, view.Samples);
                Assert.IsInRange(19.9, 20.2, view.ResponseTimePercentile95Series[0]);
                Assert.AreEqual(0.0, view.ResponseTimePercentile95Series[1]);
                Assert.IsInRange(TimeSpan.FromMilliseconds(19.9), TimeSpan.FromMilliseconds(20.2), view.Ok.ResponseTimePercentile95);
            })
            .Run();
    }

    [Test]
    public async Task Verify_per_step_rates_through_a_real_collector()
    {
        await Scenario()
            .Step("Sub-1-rps steps keep their fractional rate instead of rounding to zero", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(0));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(0));

                RecordIterations(collector, 2, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                RecordIterations(collector, 2, TestStatus.Failed, TimeSpan.FromMilliseconds(10));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(5));

                var row = Assert.ContainsSingle(metrics.Current.Steps);
                Assert.AreEqual(2, row.RequestCountOk);
                Assert.AreEqual(2, row.RequestCountFailed);
                // 0.4 ok + 0.4 failed rps: one division over the combined delta gives 0.8 —
                // summing two independently rounded per-status rates would give 0.
                Assert.AreEqual(0.8, row.RequestsPerSecond, 1e-9);
                Assert.AreEqual(0.8, row.AverageRequestsPerSecond, 1e-9);
            })
            .Step("Under a ramp the current interval rate diverges from the lifetime average", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(0));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(0));

                RecordIterations(collector, 10, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(1));

                RecordIterations(collector, 2, TestStatus.Passed, TimeSpan.FromMilliseconds(10));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(2));

                var row = Assert.ContainsSingle(metrics.Current.Steps);
                Assert.AreEqual(2.0, row.RequestsPerSecond, 1e-9);
                Assert.AreEqual(6.0, row.AverageRequestsPerSecond, 1e-9);
            })
            .Run();
    }

    [Test]
    public async Task Verify_status_detail_surfaces_assert_failure_messages()
    {
        await Scenario()
            .Step("Status detail is null while no assert has failed", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(0));

                Assert.AreEqual(TestStatus.Passed, metrics.Current.Status);
                Assert.IsNull(metrics.Current.StatusDetail);
            })
            .Step("An assert-while-running failure surfaces its message as the status detail", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, SyntheticLoadSnapshots.At(0));
                collector.SetAssertWhileRunningException(new Exception("Response times exceeded the budget"));
                collector.SetStatus(TestStatus.Failed);
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(1));

                var view = metrics.Current;
                Assert.AreEqual(TestStatus.Failed, view.Status);
                Assert.AreEqual("Response times exceeded the budget", view.StatusDetail);
            })
            .Step("The earliest assert failure wins when several are set", context =>
            {
                var collector = CreateCollector();
                var metrics = CreateMetrics();

                collector.MarkPhaseAsStarted(LoadTestPhase.Init, SyntheticLoadSnapshots.At(0));
                collector.SetAssertWhileWarmingUpException(new Exception("Warmup error rate too high"));
                collector.SetAssertWhileRunningException(new Exception("Response times exceeded the budget"));
                collector.SetStatus(TestStatus.Failed);
                metrics.Record(collector.GetCurrentResult(true), SyntheticLoadSnapshots.At(1));

                Assert.AreEqual("Warmup error rate too high", metrics.Current.StatusDetail);
            })
            .Run();
    }
}
