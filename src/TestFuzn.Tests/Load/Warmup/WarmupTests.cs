namespace Fuzn.TestFuzn.Tests.Load.Warmup;

[TestClass]
public class WarmupTests : BaseFeatureTest
{
    [Test]
    public async Task Verify()
    {
        var executionCount = 0;

        await Scenario()
            .Step("Step 1", async (context) =>
            {
                Interlocked.Increment(ref executionCount);
            })
            .Load().Warmup((context, simulations) =>
            {
                simulations.OneTimeLoad(10);
            })
            .Load().Simulations((context, simulations) =>
            {
                simulations.OneTimeLoad(20);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(30, executionCount);
                Assert.AreEqual(20, stats.RequestCount);
            })
            .Run();
    }
}
