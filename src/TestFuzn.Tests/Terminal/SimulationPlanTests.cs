using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class SimulationPlanTests : Test
{
    [Test]
    public async Task Verify_compact_labels_and_planned_durations()
    {
        await Scenario()
            .Step("Fixed Load with a one-second interval labels as rps and plans its duration", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(100, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30))
                });

                var entry = Assert.ContainsSingle(plan.Entries);
                Assert.AreEqual("Fixed Load 100 rps", entry.Label);
                Assert.AreEqual(TimeSpan.FromSeconds(30), entry.Duration);
                Assert.AreEqual(TimeSpan.FromSeconds(30), plan.PlannedDuration);
            })
            .Step("Fixed Load with a custom interval labels the rate per interval", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(100, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30))
                });

                Assert.AreEqual("Fixed Load 100 per 30s", Assert.ContainsSingle(plan.Entries).Label);
            })
            .Step("Gradual, Random and Pause label compactly and plan their configured durations", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new GradualLoadIncreaseConfiguration(10, 100, TimeSpan.FromSeconds(90)),
                    new RandomLoadPerSecondConfiguration(5, 50, TimeSpan.FromSeconds(60)),
                    new PauseLoadConfiguration(TimeSpan.FromSeconds(90))
                });

                Assert.HasCount(3, plan.Entries);
                Assert.AreEqual("Gradual Load 10→100 rps", plan.Entries[0].Label);
                Assert.AreEqual("Random Load 5-50 rps", plan.Entries[1].Label);
                Assert.AreEqual("Pause Load 1m 30s", plan.Entries[2].Label);
                Assert.AreEqual(TimeSpan.FromSeconds(240), plan.PlannedDuration);
            })
            .Step("Count-based simulations label their counts and have no planned duration", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new OneTimeLoadConfiguration(500),
                    new FixedConcurrentLoadConfiguration(10, 500)
                });

                Assert.AreEqual("One Time Load 500 iterations", plan.Entries[0].Label);
                Assert.IsNull(plan.Entries[0].Duration);
                Assert.AreEqual("Fixed Concurrent Load 10, total 500", plan.Entries[1].Label);
                Assert.IsNull(plan.Entries[1].Duration);
                Assert.IsNull(plan.PlannedDuration);
            })
            .Step("Duration-based Fixed Concurrent Load plans its duration", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedConcurrentLoadConfiguration(10, TimeSpan.FromSeconds(45))
                });

                var entry = Assert.ContainsSingle(plan.Entries);
                Assert.AreEqual("Fixed Concurrent Load 10", entry.Label);
                Assert.AreEqual(TimeSpan.FromSeconds(45), entry.Duration);
            })
            .Step("Durations format compactly across units", context =>
            {
                var labels = new SimulationPlan(new ILoadConfiguration[]
                {
                    new PauseLoadConfiguration(TimeSpan.FromMilliseconds(500)),
                    new PauseLoadConfiguration(TimeSpan.FromSeconds(45)),
                    new PauseLoadConfiguration(TimeSpan.FromHours(1)),
                    new PauseLoadConfiguration(TimeSpan.FromMinutes(65)),
                    new PauseLoadConfiguration(TimeSpan.Zero)
                }).Entries.Select(entry => entry.Label).ToList();

                Assert.AreEqual("Pause Load 0.5s", labels[0]);
                Assert.AreEqual("Pause Load 45s", labels[1]);
                Assert.AreEqual("Pause Load 1h", labels[2]);
                Assert.AreEqual("Pause Load 1h 5m", labels[3]);
                Assert.AreEqual("Pause Load 0s", labels[4]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_segment_totals_split_warmup_and_measurement()
    {
        await Scenario()
            .Step("Warmup and measurement segments sum separately and together", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)) { IsWarmup = true },
                    new GradualLoadIncreaseConfiguration(10, 100, TimeSpan.FromSeconds(30)),
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
                });

                Assert.AreEqual(TimeSpan.FromSeconds(10), plan.PlannedWarmupDuration);
                Assert.AreEqual(TimeSpan.FromSeconds(90), plan.PlannedMeasurementDuration);
                Assert.AreEqual(TimeSpan.FromSeconds(100), plan.PlannedDuration);
            })
            .Step("Without warmup simulations the warmup segment plans to zero", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
                });

                Assert.AreEqual(TimeSpan.Zero, plan.PlannedWarmupDuration);
                Assert.AreEqual(TimeSpan.FromSeconds(60), plan.PlannedDuration);
            })
            .Step("A count-based measurement simulation makes only the affected totals indeterminate", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)) { IsWarmup = true },
                    new OneTimeLoadConfiguration(500)
                });

                Assert.AreEqual(TimeSpan.FromSeconds(10), plan.PlannedWarmupDuration);
                Assert.IsNull(plan.PlannedMeasurementDuration);
                Assert.IsNull(plan.PlannedDuration);
            })
            .Step("A count-based warmup simulation makes the warmup segment indeterminate", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedConcurrentLoadConfiguration(2, 100) { IsWarmup = true },
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
                });

                Assert.IsNull(plan.PlannedWarmupDuration);
                Assert.AreEqual(TimeSpan.FromSeconds(60), plan.PlannedMeasurementDuration);
                Assert.IsNull(plan.PlannedDuration);
            })
            .Run();
    }

    [Test]
    public async Task Verify_phase_labels_at_segment_boundaries()
    {
        await Scenario()
            .Step("Multiple measurement simulations are numbered and advance at exact boundaries", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new GradualLoadIncreaseConfiguration(10, 100, TimeSpan.FromSeconds(30)),
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
                });

                Assert.AreEqual("sim 1/2: Gradual Load 10→100 rps", plan.MeasurementPhaseLabel(TimeSpan.Zero));
                Assert.AreEqual("sim 1/2: Gradual Load 10→100 rps", plan.MeasurementPhaseLabel(TimeSpan.FromMilliseconds(29999)));
                Assert.AreEqual("sim 2/2: Fixed Load 50 rps", plan.MeasurementPhaseLabel(TimeSpan.FromSeconds(30)));
                Assert.AreEqual("sim 2/2: Fixed Load 50 rps", plan.MeasurementPhaseLabel(TimeSpan.FromSeconds(89)));
            })
            .Step("Past the end of a duration-based segment the last simulation stays current", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new GradualLoadIncreaseConfiguration(10, 100, TimeSpan.FromSeconds(30)),
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
                });

                Assert.AreEqual("sim 2/2: Fixed Load 50 rps", plan.MeasurementPhaseLabel(TimeSpan.FromSeconds(500)));
            })
            .Step("A single measurement simulation labels bare, without a sim prefix", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
                });

                Assert.AreEqual("Fixed Load 50 rps", plan.MeasurementPhaseLabel(TimeSpan.FromSeconds(5)));
            })
            .Step("Warmup labels carry the warmup prefix, numbered only when there are several", context =>
            {
                var single = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)) { IsWarmup = true },
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
                });
                Assert.AreEqual("warmup: Fixed Load 5 rps", single.WarmupPhaseLabel(TimeSpan.FromSeconds(3)));

                var multiple = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)) { IsWarmup = true },
                    new GradualLoadIncreaseConfiguration(5, 20, TimeSpan.FromSeconds(20)) { IsWarmup = true },
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
                });
                Assert.AreEqual("warmup 1/2: Fixed Load 5 rps", multiple.WarmupPhaseLabel(TimeSpan.FromSeconds(9)));
                Assert.AreEqual("warmup 2/2: Gradual Load 5→20 rps", multiple.WarmupPhaseLabel(TimeSpan.FromSeconds(10)));
            })
            .Step("A count-based simulation becomes current when reached and absorbs the rest of the segment", context =>
            {
                var plan = new SimulationPlan(new ILoadConfiguration[]
                {
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)),
                    new OneTimeLoadConfiguration(500),
                    new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30))
                });

                Assert.AreEqual("sim 1/3: Fixed Load 50 rps", plan.MeasurementPhaseLabel(TimeSpan.FromSeconds(10)));
                Assert.AreEqual("sim 2/3: One Time Load 500 iterations", plan.MeasurementPhaseLabel(TimeSpan.FromSeconds(30)));
                Assert.AreEqual("sim 2/3: One Time Load 500 iterations", plan.MeasurementPhaseLabel(TimeSpan.FromSeconds(10000)));
            })
            .Run();
    }
}
