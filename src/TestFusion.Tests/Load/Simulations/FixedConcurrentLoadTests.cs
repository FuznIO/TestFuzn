namespace TestFusion.Tests.Load.Simulations;

[FeatureTest]
public class FixedConcurrentLoadTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task Test()
    {
        var stepExecutionCount = 0;

        await Scenario("Verify concurrrent load")
            .Step("Step 1", async (context) =>
            {
                Interlocked.Increment(ref stepExecutionCount);

                await Task.Delay(TimeSpan.FromSeconds(1));
            })
            .Load().FixedConcurrentLoad(50, TimeSpan.FromSeconds(3))
            .Load().OneTimeLoad(1)
            .Run();

        Assert.IsTrue(stepExecutionCount > 0);
    }
}

