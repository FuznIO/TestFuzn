namespace Fuzn.TestFuzn.Tests.Reports;

[TestClass]
public class LoadReportTests : Test
{
    [Test]
    [Tags("Category1", "Category2", "Category3")]
    [Metadata("MetadataKey1", "MetadataValue1")]
    [Metadata("ClassMetaKey1", "ClassMetaValue1")]
    public async Task ShortRunning_NoErrors()
    {
        await Scenario()
            .Id("ID-1234")
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

    [Test]
    [Metadata("MetaKey", "MetaValue")]
    public async Task ShortRunning_WithErrors_NoAssert()
    {
        await Scenario()
            .Id("ScenarioId-1234")
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

    [Test]
    [Metadata("MetaKey", "MetaValue")]
    public async Task ShouldFail_ShortRunning_WithErrors_WithFailingAssertWhenDone()
    {
        bool catchWasRun = false;

        try
        {
            await Scenario("Short running report with errors and failing assert")
                .Id("ScenarioId-1234")
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

    [Test]
    public async Task ZLongRunning()
    {
        int i = 0;

        await Scenario()
            .Step("Step 1", (context) => { })
            .Step("Step 2", (context) => { })
            .Step("Step 3", (context) => { })
            .Step("Step 4", (context) => { })
            .Step("Step 5", (context) =>
            {
                Interlocked.Increment(ref i);
                if (i % 3 == 0)
                    Assert.Fail();
            })
            .Step("Step 6", (context) => { })
            .Step("Step 7", (context) => { })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(300, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15)))
            .Run();
    }
}
