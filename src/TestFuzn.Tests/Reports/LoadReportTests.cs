namespace Fuzn.TestFuzn.Tests.Reports;

[FeatureTest]
public class LoadReportTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task ShortRunning_WithErrors()
    {
        await Scenario()
            .Step("Test", (context) =>
            {
            })
            .Step("This step should fail now and then", (context) =>
            {
                if (Random.Shared.NextDouble() < 0.33)
                    Assert.Fail();
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(50))
            .Run();
    }

    [ScenarioTest]
    public async Task ShortRunning_NoErrors()
    {
        await Scenario()
            .Step("Test Step 1", (context) =>
            {
            })
            .Step("Test Step 2", (context) =>
            {
            })
            .Step("Test Step 3", (context) =>
            {
            })
            .Step("Test Step 4", (context) =>
            {
            })
            .Step("Test Step 5", (context) =>
            {
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(50))
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
                .Load().Simulations((context, simulations) => simulations.FixedLoad(3, TimeSpan.FromSeconds(10)))
                .Run();
    }
}
