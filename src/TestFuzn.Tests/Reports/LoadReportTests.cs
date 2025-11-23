
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Tests.Reports;

[FeatureTest]
public class LoadReportTests : BaseFeatureTest
{
    public override string FeatureId { get => "FeatureID-1"; }
    public override string FeatureName { get => "Feature-Name-1"; }
    public override Dictionary<string, string> FeatureMetadata { get => new Dictionary<string, string>() { { "Meta1", "Value1" } }; }

    [ScenarioTest]
    [TestCategory("Category1")]
    [TestCategory("Category2")]
    [TestCategory("Category3")]
    public async Task ShortRunning_NoErrors()
    {
        await Scenario()
            .Id("ID-1234")
            .Metadata("MetadataScenarioKey1", "Value1")
            .Metadata("MetadataScenarioKey2", "Value2")
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
    public async Task ShortRunning_WithErrors_NoAssert()
    {
        await Scenario()
            .Id("ScenarioId-1234")
            .Metadata("Scenario-Meta1", "Value1")
            .Step("Step 1", "Test-1234", (context) =>
            {
                return Task.CompletedTask;
            })
            .Step("Step 2", (context) =>
            {
                if (Random.Shared.NextDouble() < 0.33)
                    Assert.Fail();
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(50))
            .Run();
    }

    [ScenarioTest]
    public async Task ShouldFail_ShortRunning_WithErrors_WithFailingAssertWhenDone()
    {
        bool catchWasRun = false;

        try
        {
            await Scenario("Short running report with errors and failing assert")
                .Id("ScenarioId-1234")
                .Metadata("Scenario-Meta1", "Value1")
                .Step("Step 1", "Test-1234", (context) =>
                {
                })
                .Step("Step 2", (context) =>
                {
                })
                .Step("Step 3", (context) =>
                {
                    if (Random.Shared.NextDouble() < 0.33)
                    {
                        throw new Exception("Some random error: " + Guid.NewGuid().ToString());
                    }
                })
                .Step("Step 4", (context) =>
                {
                })
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(50))
                .Load().AssertWhenDone((context, result) =>
                {
                    if (result.Failed.RequestCount > 0)
                        Assert.Fail("There should be no failing steps"); // This will make the scenario fail.
                })
                .Run();
        }
        catch (AssertFailedException ex)
        {
            catchWasRun = true;
            Assert.Contains("There should be no failing steps", ex.Message);
        }

        Assert.IsTrue(catchWasRun);
    }

    //[ScenarioTest]
    //public async Task LongRunning()
    //{
    //        int i = 0;

    //        await Scenario()
    //            .Step("Step 1", (context) => { })
    //            .Step("Step 2", (context) => { })
    //            .Step("Step 3", (context) => { })
    //            .Step("Step 4", (context) => { })
    //            //.Step("Step 4", async (context) => { await Task.Delay(TimeSpan.FromMilliseconds(100));  })
    //            .Step("Step 5", (context) =>
    //            {
    //                Interlocked.Increment(ref i);
    //                if (i % 3 == 0)
    //                    Assert.Fail();
    //            })
    //            .Step("Step 6", (context) => { })
    //            .Step("Step 7", (context) => { })
    //            .Load().Simulations((context, simulations) => simulations.FixedLoad(3, TimeSpan.FromSeconds(10)))
    //            .Run();
    //}
}
