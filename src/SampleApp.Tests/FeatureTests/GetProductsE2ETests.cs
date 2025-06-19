using SampleApp.WebApp.Models;
using TestFusion.HttpTesting;
using TestFusion;

namespace SampleApp.Tests.FeatureTests;

[FeatureTest]
public class GetProductsE2ETests : BaseFeatureTest
{
    public override string FeatureName => "Get products";

    [ScenarioTest]
    public async Task Verify()
    {
        await Scenario("Verify that get products works")
            .Step("Call /Products", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products").Get();

                Assert.IsTrue(response.Ok);

                context.SetSharedData("response", response);
            })
            .Step("Ensure that more than one product is returned", (context) =>
            {
                var products = context.GetSharedData<HttpResponse>("response").BodyAs<List<Product>>();

                Assert.IsTrue(products.Count > 0);
            })
            .Run();
    }
}
