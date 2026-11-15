using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Tests.Reports;

[TestClass]
[Tags("unit")]
public class LoadTargetTests : Test
{
    [Test]
    public async Task Verify_planned_rate_follows_the_simulations()
    {
        await Scenario()
            .Step("A fixed load of 500 every second plans 500 per second, and nothing after it ends", context =>
            {
                var simulations = new List<ILoadConfiguration> { new FixedLoadConfiguration(500, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15)) };

                Assert.AreEqual(500.0, LoadTarget.PerSecond(simulations, 0, 3));
                Assert.AreEqual(250.0, LoadTarget.PerSecond(simulations, 13, 17));
                Assert.AreEqual(0.0, LoadTarget.PerSecond(simulations, 16, 18));
            })
            .Step("A fixed load of 100 every 2 seconds plans 50 per second", context =>
            {
                var simulations = new List<ILoadConfiguration> { new FixedLoadConfiguration(100, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)) };

                Assert.AreEqual(50.0, LoadTarget.PerSecond(simulations, 0, 4));
            })
            .Step("Warmup is left out, so the measurement starts with the first measured simulation", context =>
            {
                var simulations = new List<ILoadConfiguration>
                {
                    new FixedLoadConfiguration(10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)) { IsWarmup = true },
                    new FixedLoadConfiguration(200, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10))
                };

                Assert.AreEqual(200.0, LoadTarget.PerSecond(simulations, 0, 2));
            })
            .Step("A gradual load rises over the run", context =>
            {
                var simulations = new List<ILoadConfiguration> { new GradualLoadIncreaseConfiguration(10, 20, TimeSpan.FromSeconds(10)) };

                var early = LoadTarget.PerSecond(simulations, 0, 1);
                var late = LoadTarget.PerSecond(simulations, 9, 10);
                Assert.IsNotNull(early);
                Assert.IsNotNull(late);
                Assert.IsGreaterThan(early.Value, late.Value);
            })
            .Step("A gradual load rises evenly, so the plan follows the line between its rates", context =>
            {
                var simulations = new List<ILoadConfiguration> { new GradualLoadIncreaseConfiguration(10, 20, TimeSpan.FromSeconds(10)) };

                Assert.AreEqual(15.0, LoadTarget.PerSecond(simulations, 0, 10));
                Assert.AreEqual(10.5, LoadTarget.PerSecond(simulations, 0, 1));
                Assert.AreEqual(19.5, LoadTarget.PerSecond(simulations, 9, 10));
            })
            .Step("A gradual load can ramp down", context =>
            {
                var simulations = new List<ILoadConfiguration> { new GradualLoadIncreaseConfiguration(20, 10, TimeSpan.FromSeconds(10)) };

                Assert.AreEqual(19.5, LoadTarget.PerSecond(simulations, 0, 1));
                Assert.AreEqual(10.5, LoadTarget.PerSecond(simulations, 9, 10));
            })
            .Step("A fixed load stops at its duration, also partway through an interval", context =>
            {
                var simulations = new List<ILoadConfiguration> { new FixedLoadConfiguration(100, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)) };

                Assert.AreEqual(50.0, LoadTarget.PerSecond(simulations, 0, 3));
                Assert.AreEqual(0.0, LoadTarget.PerSecond(simulations, 3, 4));
            })
            .Step("A pause after a load plans nothing", context =>
            {
                var simulations = new List<ILoadConfiguration>
                {
                    new FixedLoadConfiguration(10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
                    new PauseLoadConfiguration(TimeSpan.FromSeconds(5))
                };

                Assert.AreEqual(0.0, LoadTarget.PerSecond(simulations, 3, 5));
            })
            .Step("A run without a rate has no target, also after a one-time burst", context =>
            {
                Assert.IsNull(LoadTarget.PerSecond([new OneTimeLoadConfiguration(10)], 5, 6));
                Assert.IsNull(LoadTarget.PerSecond([new PauseLoadConfiguration(TimeSpan.FromSeconds(5))], 0, 5));
            })
            .Step("A cluster controller only knows the descriptions, so it has no target", context =>
            {
                Assert.IsNull(LoadTarget.PerSecond([new DescribedLoadConfiguration("Fixed Load - Rate: 10", false)], 0, 1));
                Assert.IsNull(LoadTarget.PerSecond([new DescribedLoadConfiguration("Fixed Load - Rate: 10", false)], 5, 6));
            })
            .Step("Loads without a rate have no target", context =>
            {
                Assert.IsNull(LoadTarget.PerSecond([new FixedConcurrentLoadConfiguration(10, TimeSpan.FromSeconds(10))], 0, 5));
                Assert.IsNull(LoadTarget.PerSecond([new FixedConcurrentLoadConfiguration(10, 100)], 50, 60));
                Assert.IsNull(LoadTarget.PerSecond([new RandomLoadPerSecondConfiguration(1, 10, TimeSpan.FromSeconds(10))], 0, 5));
                Assert.IsNull(LoadTarget.PerSecond([new OneTimeLoadConfiguration(10)], 0, 1));
            })
            .Step("Without simulations or time there is no target", context =>
            {
                Assert.IsNull(LoadTarget.PerSecond(null, 0, 5));
                Assert.IsNull(LoadTarget.PerSecond([new PauseLoadConfiguration(TimeSpan.FromSeconds(5))], 3, 3));
            });
    }
}
