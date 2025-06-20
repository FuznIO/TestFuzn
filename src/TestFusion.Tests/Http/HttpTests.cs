using TestFusion.HttpTesting;

namespace TestFusion.Tests.Http;

[FeatureTest]
public class GetProductsE2ETests : BaseFeatureTest
{
    public override string FeatureName => "Get products";

    [ScenarioTest]
    public async Task Verify()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products").Get();

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Ping_LoadTest()
    {
        await Scenario()
            .Step("Verify ping returns pong", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Ping").Get();

                Assert.IsTrue(response.Ok);
                Assert.AreEqual("Pong", response.BodyAs<string>());
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(1500, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)))
            .Run();
    }

    [ScenarioTest]
    public async Task Verify_Load()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products").Get();

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(50, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)))
            .Run();
    }
}
