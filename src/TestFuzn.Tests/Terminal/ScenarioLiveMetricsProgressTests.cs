using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class ScenarioLiveMetricsProgressTests : Test
{
    private static ScenarioLiveMetrics CreateMetricsWithWarmup()
    {
        // 10s warmup + 30s gradual + 60s fixed = 100s planned in total.
        return new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[]
        {
            new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)) { IsWarmup = true },
            new GradualLoadIncreaseConfiguration(10, 100, TimeSpan.FromSeconds(30)),
            new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
        });
    }

    private static ScenarioLoadResult SnapshotAtPhase(
        DateTime? initStart = null,
        DateTime? warmupStart = null,
        DateTime? measurementStart = null,
        DateTime? measurementEnd = null,
        DateTime? cleanupEnd = null,
        bool isCompleted = false)
    {
        var snapshot = SyntheticLoadSnapshots.Snapshot();
        if (initStart != null)
            snapshot.InitStartTime = initStart.Value;
        if (warmupStart != null)
            snapshot.WarmupStartTime = warmupStart.Value;
        if (measurementStart != null)
            snapshot.MeasurementStartTime = measurementStart.Value;
        if (measurementEnd != null)
            snapshot.MeasurementEndTime = measurementEnd.Value;
        if (cleanupEnd != null)
            snapshot.CleanupEndTime = cleanupEnd.Value;
        snapshot.IsCompleted = isCompleted;
        return snapshot;
    }

    [Test]
    public async Task Verify_snapshots_carry_the_plan_entries()
    {
        await Scenario()
            .Step("The initial view and every recorded view carry the plan's entries in producer order, warmup first", context =>
            {
                var metrics = CreateMetricsWithWarmup();
                var initial = metrics.Current.PlanEntries;

                Assert.HasCount(3, initial);
                Assert.AreEqual("Fixed Load 5 rps", initial[0].Label);
                Assert.AreEqual(TimeSpan.FromSeconds(10), initial[0].Duration);
                Assert.IsTrue(initial[0].IsWarmup);
                Assert.AreEqual("Gradual Load 10→100 rps", initial[1].Label);
                Assert.AreEqual(TimeSpan.FromSeconds(30), initial[1].Duration);
                Assert.IsFalse(initial[1].IsWarmup);
                Assert.AreEqual("Fixed Load 50 rps", initial[2].Label);
                Assert.AreEqual(TimeSpan.FromSeconds(60), initial[2].Duration);
                Assert.IsFalse(initial[2].IsWarmup);

                metrics.Record(SnapshotAtPhase(initStart: SyntheticLoadSnapshots.At(0), warmupStart: SyntheticLoadSnapshots.At(1)), SyntheticLoadSnapshots.At(5));

                Assert.AreSame(initial, metrics.Current.PlanEntries);
            })
            .Step("A count-based simulation is an entry without a duration, and a model without simulations has no entries", context =>
            {
                var metrics = new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[] { new OneTimeLoadConfiguration(500) });
                var entry = Assert.ContainsSingle(metrics.Current.PlanEntries);
                Assert.AreEqual("One Time Load 500 iterations", entry.Label);
                Assert.IsNull(entry.Duration);

                Assert.IsEmpty(new ScenarioLiveMetrics("Checkout flow", Array.Empty<ILoadConfiguration>()).Current.PlanEntries);
            })
            .Run();
    }

    [Test]
    public async Task Verify_progress_eta_and_labels_through_the_phases()
    {
        await Scenario()
            .Step("Before anything starts the view is init with full planned time remaining", context =>
            {
                var metrics = CreateMetricsWithWarmup();
                metrics.Record(SnapshotAtPhase(), SyntheticLoadSnapshots.At(0));

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Init, view.Phase);
                Assert.AreEqual("init", view.PhaseLabel);
                Assert.AreEqual(TimeSpan.Zero, view.Duration);
                Assert.AreEqual(TimeSpan.FromSeconds(100), view.PlannedDuration);
                Assert.AreEqual(TimeSpan.FromSeconds(90), view.PlannedMeasurementDuration);
                Assert.AreEqual(0.0, view.ProgressFraction);
                Assert.AreEqual(TimeSpan.FromSeconds(100), view.EstimatedTimeRemaining);
            })
            .Step("During warmup progress runs against the warmup segment's planned duration", context =>
            {
                var metrics = CreateMetricsWithWarmup();
                var snapshot = SnapshotAtPhase(initStart: SyntheticLoadSnapshots.At(0), warmupStart: SyntheticLoadSnapshots.At(3));
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(7));

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Warmup, view.Phase);
                Assert.AreEqual("warmup: Fixed Load 5 rps", view.PhaseLabel);
                Assert.AreEqual(TimeSpan.FromSeconds(7), view.Duration);
                Assert.AreEqual(0.04, view.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.FromSeconds(96), view.EstimatedTimeRemaining);
            })
            .Step("A warmup running past its planned duration caps at the segment boundary", context =>
            {
                var metrics = CreateMetricsWithWarmup();
                var snapshot = SnapshotAtPhase(initStart: SyntheticLoadSnapshots.At(0), warmupStart: SyntheticLoadSnapshots.At(3));
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(20));

                var view = metrics.Current;
                Assert.AreEqual(0.1, view.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.FromSeconds(90), view.EstimatedTimeRemaining);
                Assert.AreEqual("warmup: Fixed Load 5 rps", view.PhaseLabel);
            })
            .Step("When measurement starts, progress lands exactly on the warmup boundary", context =>
            {
                var metrics = CreateMetricsWithWarmup();
                var snapshot = SnapshotAtPhase(
                    initStart: SyntheticLoadSnapshots.At(0),
                    warmupStart: SyntheticLoadSnapshots.At(3),
                    measurementStart: SyntheticLoadSnapshots.At(21));
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(21));

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Measurement, view.Phase);
                Assert.AreEqual("sim 1/2: Gradual Load 10→100 rps", view.PhaseLabel);
                Assert.AreEqual(0.1, view.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.FromSeconds(90), view.EstimatedTimeRemaining);
            })
            .Step("At the exact boundary between simulations the label advances to the next one", context =>
            {
                var metrics = CreateMetricsWithWarmup();
                var snapshot = SnapshotAtPhase(
                    initStart: SyntheticLoadSnapshots.At(0),
                    warmupStart: SyntheticLoadSnapshots.At(3),
                    measurementStart: SyntheticLoadSnapshots.At(21));
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(51));

                var view = metrics.Current;
                Assert.AreEqual("sim 2/2: Fixed Load 50 rps", view.PhaseLabel);
                Assert.AreEqual(0.4, view.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.FromSeconds(60), view.EstimatedTimeRemaining);
            })
            .Step("Running past the planned end clamps progress at one with zero remaining", context =>
            {
                var metrics = CreateMetricsWithWarmup();
                var snapshot = SnapshotAtPhase(
                    initStart: SyntheticLoadSnapshots.At(0),
                    warmupStart: SyntheticLoadSnapshots.At(3),
                    measurementStart: SyntheticLoadSnapshots.At(21));
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(115));

                var view = metrics.Current;
                Assert.AreEqual(1.0, view.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.Zero, view.EstimatedTimeRemaining);
                Assert.AreEqual("sim 2/2: Fixed Load 50 rps", view.PhaseLabel);
            })
            .Step("A completed measurement shows cleanup until cleanup has ended", context =>
            {
                var metrics = CreateMetricsWithWarmup();
                var snapshot = SnapshotAtPhase(
                    initStart: SyntheticLoadSnapshots.At(0),
                    warmupStart: SyntheticLoadSnapshots.At(3),
                    measurementStart: SyntheticLoadSnapshots.At(21),
                    measurementEnd: SyntheticLoadSnapshots.At(116),
                    isCompleted: true);
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(116.5));

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Cleanup, view.Phase);
                Assert.AreEqual("cleanup", view.PhaseLabel);
                Assert.IsTrue(view.IsCompleted);
                Assert.AreEqual(1.0, view.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.Zero, view.EstimatedTimeRemaining);
            })
            .Step("After cleanup ends the label is completed and the duration freezes", context =>
            {
                var metrics = CreateMetricsWithWarmup();
                var snapshot = SnapshotAtPhase(
                    initStart: SyntheticLoadSnapshots.At(0),
                    warmupStart: SyntheticLoadSnapshots.At(3),
                    measurementStart: SyntheticLoadSnapshots.At(21),
                    measurementEnd: SyntheticLoadSnapshots.At(116),
                    cleanupEnd: SyntheticLoadSnapshots.At(120),
                    isCompleted: true);
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(500));

                var view = metrics.Current;
                Assert.AreEqual("completed", view.PhaseLabel);
                Assert.AreEqual(TimeSpan.FromSeconds(120), view.Duration);
                Assert.AreEqual(1.0, view.ProgressFraction.Value, 1e-9);
            })
            .Run();
    }

    [Test]
    public async Task Verify_indeterminate_plans_expose_no_fake_numbers()
    {
        await Scenario()
            .Step("A count-based simulation leaves planned duration, progress and ETA null", context =>
            {
                var metrics = new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[]
                {
                    new OneTimeLoadConfiguration(500)
                });

                var initial = metrics.Current;
                Assert.IsNull(initial.PlannedDuration);
                Assert.IsNull(initial.ProgressFraction);
                Assert.IsNull(initial.EstimatedTimeRemaining);

                var snapshot = SnapshotAtPhase(initStart: SyntheticLoadSnapshots.At(0), measurementStart: SyntheticLoadSnapshots.At(1));
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(5));

                var view = metrics.Current;
                Assert.AreEqual("One Time Load 500 iterations", view.PhaseLabel);
                Assert.IsNull(view.PlannedDuration);
                Assert.IsNull(view.PlannedMeasurementDuration);
                Assert.IsNull(view.ProgressFraction);
                Assert.IsNull(view.EstimatedTimeRemaining);
                Assert.AreEqual(TimeSpan.FromSeconds(5), view.Duration);
            })
            .Step("A determinate warmup before a count-based simulation still shows no fake progress", context =>
            {
                var metrics = new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)) { IsWarmup = true },
                    new FixedConcurrentLoadConfiguration(2, 100)
                });

                var snapshot = SnapshotAtPhase(initStart: SyntheticLoadSnapshots.At(0), warmupStart: SyntheticLoadSnapshots.At(1));
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(4));

                var view = metrics.Current;
                Assert.AreEqual("warmup: Fixed Load 5 rps", view.PhaseLabel);
                Assert.IsNull(view.ProgressFraction);
                Assert.IsNull(view.EstimatedTimeRemaining);
                Assert.AreEqual(TimeSpan.FromSeconds(4), view.Duration);
            })
            .Run();
    }

    [Test]
    public async Task Verify_measurement_only_progress_when_the_warmup_plan_is_indeterminate()
    {
        // Count-based warmup (indeterminate) followed by a fully determinate 60s measurement.
        static ScenarioLiveMetrics CreateMetricsWithCountBasedWarmup()
        {
            return new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[]
            {
                new FixedConcurrentLoadConfiguration(2, 100) { IsWarmup = true },
                new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
            });
        }

        await Scenario()
            .Step("While the count-based warmup runs, progress and ETA stay null but the measurement plan is exposed", context =>
            {
                var metrics = CreateMetricsWithCountBasedWarmup();

                var initial = metrics.Current;
                Assert.IsNull(initial.PlannedDuration);
                Assert.AreEqual(TimeSpan.FromSeconds(60), initial.PlannedMeasurementDuration);
                Assert.IsNull(initial.ProgressFraction);
                Assert.IsNull(initial.EstimatedTimeRemaining);

                var snapshot = SnapshotAtPhase(initStart: SyntheticLoadSnapshots.At(0), warmupStart: SyntheticLoadSnapshots.At(1));
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(10));

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Warmup, view.Phase);
                Assert.IsNull(view.ProgressFraction);
                Assert.IsNull(view.EstimatedTimeRemaining);
            })
            .Step("Once measurement starts, progress and ETA run against the measurement segment alone", context =>
            {
                var metrics = CreateMetricsWithCountBasedWarmup();
                var snapshot = SnapshotAtPhase(
                    initStart: SyntheticLoadSnapshots.At(0),
                    warmupStart: SyntheticLoadSnapshots.At(1),
                    measurementStart: SyntheticLoadSnapshots.At(30));

                metrics.Record(snapshot, SyntheticLoadSnapshots.At(30));
                var atStart = metrics.Current;
                Assert.AreEqual(0.0, atStart.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.FromSeconds(60), atStart.EstimatedTimeRemaining);

                metrics.Record(snapshot, SyntheticLoadSnapshots.At(60));
                var midway = metrics.Current;
                Assert.AreEqual(0.5, midway.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.FromSeconds(30), midway.EstimatedTimeRemaining);

                metrics.Record(snapshot, SyntheticLoadSnapshots.At(120));
                var pastPlannedEnd = metrics.Current;
                Assert.AreEqual(1.0, pastPlannedEnd.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.Zero, pastPlannedEnd.EstimatedTimeRemaining);
            })
            .Step("After measurement completes, the measurement-only progress reads full through cleanup", context =>
            {
                var metrics = CreateMetricsWithCountBasedWarmup();
                var snapshot = SnapshotAtPhase(
                    initStart: SyntheticLoadSnapshots.At(0),
                    warmupStart: SyntheticLoadSnapshots.At(1),
                    measurementStart: SyntheticLoadSnapshots.At(30),
                    measurementEnd: SyntheticLoadSnapshots.At(90),
                    isCompleted: true);
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(91));

                var view = metrics.Current;
                Assert.AreEqual(LoadTestPhase.Cleanup, view.Phase);
                Assert.AreEqual(1.0, view.ProgressFraction.Value, 1e-9);
                Assert.AreEqual(TimeSpan.Zero, view.EstimatedTimeRemaining);
            })
            .Run();
    }
}
