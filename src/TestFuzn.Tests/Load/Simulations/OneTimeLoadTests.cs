namespace Fuzn.TestFuzn.Tests.Load.Simulations;

[FeatureTest]
public class OneTimeLoadTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task Test()
    {
        var stepExecutionCount = 0;

        await Scenario("Verify one time load simulation")
            .Step("Test", (context) =>
            {
                Interlocked.Increment(ref stepExecutionCount);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(50))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(50, stats.Ok.RequestCount);
            })
            .Run();

        Assert.AreEqual(50, stepExecutionCount);
    }

    [ScenarioTest]
    public async Task TestFailed()
    {
        var stepExecutionCount = 0;

        await Scenario("Verify one time load simulation with failing steps")
            .Step("Test", (context) =>
            {
                Interlocked.Increment(ref stepExecutionCount);

                if (stepExecutionCount >= 25 && stepExecutionCount <= 29)
                    throw new Exception($"Simulated failure at step {stepExecutionCount}");
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(50))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(45, stats.Ok.RequestCount);
                Assert.AreEqual(5, stats.Failed.RequestCount);
            })
            .Run();

        Assert.AreEqual(50, stepExecutionCount);
    }
}
