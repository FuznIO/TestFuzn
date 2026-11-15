using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

namespace Fuzn.TestFuzn.Tests.Cluster;

[TestClass]
[Tags("unit")]
public class LoadSplitTests : Test
{
    [Test]
    public async Task Verify_parts_add_up_to_the_total_with_the_leftover_on_the_lowest_indexes()
    {
        await Scenario()
            .Step("10 over 4 workers is 3, 3, 2, 2", context =>
            {
                CollectionAssert.AreEqual(new[] { 3, 3, 2, 2 }, Parts(10, 4, isWarmup: false));
            })
            .Step("1 over 4 workers runs once in the whole cluster", context =>
            {
                CollectionAssert.AreEqual(new[] { 1, 0, 0, 0 }, Parts(1, 4, isWarmup: false));
            })
            .Step("Every total and worker count adds up exactly", context =>
            {
                for (var total = 0; total <= 50; total++)
                {
                    for (var workers = 1; workers <= 7; workers++)
                        Assert.AreEqual(total, Parts(total, workers, isWarmup: false).Sum());
                }
            })
            .Step("Warmup gives every worker at least one", context =>
            {
                CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, Parts(1, 4, isWarmup: true));
                CollectionAssert.AreEqual(new[] { 0, 0 }, Parts(0, 2, isWarmup: true));
            })
            .Step("A worker index outside the run is rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LoadSplit.Part(10, 4, 4, isWarmup: false));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LoadSplit.Part(10, 0, 0, isWarmup: false));
            });
    }

    [Test]
    public async Task Verify_each_simulation_splits_its_load()
    {
        await Scenario()
            .Step("One time, fixed and fixed concurrent loads split their count or rate", context =>
            {
                Assert.AreEqual(3, ((OneTimeLoadConfiguration) new OneTimeLoadConfiguration(10).Split(1, 4, 0)).Count);

                var fixedLoad = (FixedLoadConfiguration) new FixedLoadConfiguration(10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)).Split(3, 4, 0);
                Assert.AreEqual(2, fixedLoad.Rate);
                Assert.AreEqual(TimeSpan.FromSeconds(30), fixedLoad.Duration);

                var concurrent = (FixedConcurrentLoadConfiguration) new FixedConcurrentLoadConfiguration(5, TimeSpan.FromSeconds(10)).Split(0, 2, 0);
                Assert.AreEqual(3, concurrent.FixedCount);
                Assert.AreEqual(TimeSpan.FromSeconds(10), concurrent.Duration);
            })
            .Step("A gradual load splits its start and end rate and keeps its duration", context =>
            {
                var gradual = (GradualLoadIncreaseConfiguration) new GradualLoadIncreaseConfiguration(4, 20, TimeSpan.FromMinutes(1)).Split(1, 2, 0);
                Assert.AreEqual(2, gradual.StartRate);
                Assert.AreEqual(10, gradual.EndRate);
                Assert.AreEqual(TimeSpan.FromMinutes(1), gradual.Duration);
            })
            .Step("A pause is the same on every worker, and the warmup flag is kept", context =>
            {
                var pause = new PauseLoadConfiguration(TimeSpan.FromSeconds(5)) { IsWarmup = true }.Split(2, 3, 0);
                Assert.AreEqual(TimeSpan.FromSeconds(5), ((PauseLoadConfiguration) pause).Duration);
                Assert.IsTrue(pause.IsWarmup);
            })
            .Step("A random load keeps its range and carries the shared seed and the worker's index", context =>
            {
                var random = (RandomLoadPerSecondConfiguration) new RandomLoadPerSecondConfiguration(10, 20, TimeSpan.FromSeconds(5)).Split(1, 3, 42);
                Assert.AreEqual(10, random.MinRate);
                Assert.AreEqual(20, random.MaxRate);
                Assert.AreEqual(42, random.Seed);
                Assert.AreEqual(1, random.WorkerIndex);
                Assert.AreEqual(3, random.WorkerCount);
            });
    }

    private static int[] Parts(int total, int workerCount, bool isWarmup)
    {
        return Enumerable.Range(0, workerCount).Select(index => LoadSplit.Part(total, index, workerCount, isWarmup)).ToArray();
    }
}
