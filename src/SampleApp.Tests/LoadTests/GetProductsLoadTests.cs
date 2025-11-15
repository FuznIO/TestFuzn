using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;

namespace Samples.LoadTests;

[FeatureTest]
public class GetProductsLoadTests : BaseFeatureTest
{

    [ScenarioTest]
    public async Task Test1()
    {
        await Scenario("Get products")
            .Step("Call get products", async context =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products").Get();

                Assert.IsTrue(response.Ok);
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(1000, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3)))
            .Run();
    }
}
