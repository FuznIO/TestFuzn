namespace Fuzn.TestFuzn.Tests.Reports;

[TestClass]
[Tags("integration", "load")]
public class LoadReportTests : Test
{
    [Test( Description = "Test of description")]
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
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(50));
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
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(50));
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
                    if (result.Failed.Count > 0)
                        Assert.Fail("There should be no failing steps"); // This will make the scenario fail.
                });
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
            .Load().Simulations((context, simulations) => simulations.FixedLoad(300, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15)));
    }

    [Test]
    [Tags("Checkout", "Catalog", "Nightly")]
    public async Task ZLongRunningTwoScenarios()
    {
        var orders = 0;

        var placeOrder = Scenario("Place order")
            .Id("place-order")
            .Step("Open cart", async context => await Pause(20, 40))
            .Step("Check stock", async context =>
            {
                await Pause(10, 60);
                if (Random.Shared.Next(100) < 4)
                    throw new TimeoutException("Stock service timed out");
            })
            .Step("Apply discount", async context => await Pause(5, 15))
            .Step("Calculate shipping", async context => await Pause(10, 30))
            .Step("Pay", async context =>
            {
                await Pause(40, 80);
                if (Interlocked.Increment(ref orders) % 12 == 0)
                    throw new InvalidOperationException("Payment declined");
            })
            .Step("Confirm order", async context => await Pause(10, 20))
            .Step("Send receipt", async context => await Pause(5, 10))
            .Load().Simulations((context, simulations) => simulations.FixedConcurrentLoad(10, TimeSpan.FromSeconds(40)));

        await Scenario("Browse products")
            .Id("browse-products")
            .Step("Open start page", async context => await Pause(5, 15))
            .Step("Search", async context => await Pause(15, 40))
            .Step("Filter results", async context => await Pause(5, 20))
            .Step("Open product", async context =>
            {
                await Pause(10, 30);
                if (Random.Shared.Next(100) < 3)
                    throw new InvalidOperationException("Product not found");
            })
            .Step("Read reviews", async context => await Pause(10, 25))
            .Step("Compare prices", async context => await Pause(5, 15))
            .Step("Add to cart", async context => await Pause(10, 20))
            .Load().Simulations((context, simulations) =>
            {
                simulations.GradualLoadIncrease(5, 40, TimeSpan.FromSeconds(20));
                simulations.FixedLoad(40, TimeSpan.FromSeconds(20));
            })
            .Load().IncludeScenario(placeOrder);
    }

    private static Task Pause(int minMs, int maxMs) => Task.Delay(Random.Shared.Next(minMs, maxMs + 1));
}
