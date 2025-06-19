namespace TestFusion.Tests.Reports;

[FeatureTest]
public class DefaultReportsTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task ShortRunning()
    {
        await Scenario("Verify report")
            .Step("Test", (context) =>
            {
            })
            .Step("This step should fail now and then", (context) =>
            {
                if (Random.Shared.NextDouble() < 0.33)
                    Assert.Fail();
            })
            .Load().OneTimeLoad(50)
            .Run();
    }

    [ScenarioTest]
    public async Task LongRunning()
    {
            int i = 0;

            await Scenario()
                .Step("Step 1", (context) => { })
                .Step("Step 2", (context) => { })
                .Step("Step 3", (context) => { })
                .Step("Step 4", (context) => { })
                //.Step("Step 4", async (context) => { await Task.Delay(TimeSpan.FromMilliseconds(100));  })
                .Step("Step 5", (context) =>
                {
                    Interlocked.Increment(ref i);
                    if (i % 3 == 0)
                        Assert.Fail();
                })
                .Step("Step 6", (context) => { })
                .Step("Step 7", (context) => { })
                .Load().FixedLoad(3, TimeSpan.FromSeconds(10))
                .Run();
    }
}
