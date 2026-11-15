using Microsoft.Extensions.Logging;

namespace Fuzn.TestFuzn.Tests.Cluster;

// Load tests that run the same locally and on a cluster; ClusterRunTests runs them on the cluster.
[TestClass]
[Tags("integration", "load")]
public class ClusterLoadTests : Test
{
    [Test]
    public async Task Load_test()
    {
        var testerRuns = 0;

        await Scenario()
            .Step("Test", async context => await Task.Delay(5))
            .Step("Tester", async context =>
            {
                await Task.Delay(5);
                if (Interlocked.Increment(ref testerRuns) % 4 == 0)
                    throw new InvalidOperationException("Every 4th run fails.");
            })
            .Step("Testerer", context =>
            {
                context.Step("Testerer inner en", subContext => { });
                context.Step("Testerert inner to", subContext => { });
            })
            .Load().Warmup((context, simulations) => simulations.OneTimeLoad(2))
            .Load().Simulations((context, simulations) => simulations.FixedLoad(100, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(120)));
    }

    [Test]
    public async Task Fixed_load_with_warmup()
    {
        await Scenario()
            .Step("Work", async context => await Task.Delay(5))
            .Load().Warmup((context, simulations) => simulations.OneTimeLoad(2))
            .Load().Simulations((context, simulations) => simulations.FixedLoad(10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)))
            .Load().AssertWhenDone((context, stats) => Assert.AreEqual(30, stats.Count));
    }

    [Test]
    public async Task Once_data_feeds_the_input_data()
    {
        await Scenario()
            .Load().BeforeScenarioOnce(context => Enumerable.Range(1, 10).ToList())
            .InputDataFromList(context => context.OnceData<List<int>>().Cast<object>().ToList())
            .Step("Use item", context =>
            {
                if (context.InputData<int>() < 1)
                    throw new InvalidOperationException("The input data did not come from the once data.");
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(10, stats.Count);
                Assert.AreEqual(10, stats.Ok.Count);
            })
            .Load().AfterScenarioOnce(context => context.Logger.LogInformation("Cleaned up the once data."));
    }

    [Test]
    public async Task Gradual_load_adds_up_over_the_workers()
    {
        await Scenario()
            .Step("Work", async context => await Task.Delay(5))
            .Load().Simulations((context, simulations) => simulations.GradualLoadIncrease(4, 9, TimeSpan.FromSeconds(3)))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.IsGreaterThan(18, stats.Count);
                Assert.IsLessThan(23, stats.Count);
            });
    }

    [Test]
    public async Task Random_load_adds_up_over_the_workers()
    {
        await Scenario()
            .Step("Work", async context => await Task.Delay(5))
            .Load().Simulations((context, simulations) => simulations.RandomLoadPerSecond(6, 6, TimeSpan.FromSeconds(2)))
            .Load().AssertWhenDone((context, stats) => Assert.AreEqual(12, stats.Count));
    }

    [Test]
    public async Task Concurrent_load_adds_up_over_the_workers()
    {
        await Scenario()
            .Step("Work", async context => await Task.Delay(200))
            .Load().Simulations((context, simulations) => simulations.FixedConcurrentLoad(6, TimeSpan.FromSeconds(2)))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.IsGreaterThan(49, stats.Count);
                Assert.IsLessThan(73, stats.Count);
            });
    }

    // Long enough to be stopped from another process in the end-to-end tests.
    [Test]
    public async Task Steady_load()
    {
        await Scenario()
            .Step("Work", async context => await Task.Delay(5))
            .Load().Simulations((context, simulations) => simulations.FixedLoad(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(20)));
    }

    // Fails only on a cluster (the controller sees WorkerCount 2), so a local run of the suite stays green.
    [Test]
    public async Task ShouldFail_in_cluster_assert_while_running()
    {
        await Scenario()
            .Step("Work", async context => await Task.Delay(5))
            .Load().Simulations((context, simulations) => simulations.FixedLoad(10, TimeSpan.FromSeconds(4)))
            .Load().AssertWhileRunning((context, stats) =>
            {
                if (context.Info.WorkerCount > 1 && stats.Count > 5)
                    throw new InvalidOperationException("Too many iterations for this test.");
            });
    }
}
