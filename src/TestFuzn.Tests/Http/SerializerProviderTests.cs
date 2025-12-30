using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests.Http;

[TestClass]
public class SerializerProviderTests : TestBase
{
    public override FeatureInfo Feature => new() { Name = "Http - Serialization" };

    [Test]
    public async Task Verify_Using_SystemText_Set_During_Startup()
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

    [Test]
    public async Task Verify_Using_SystemText_Override()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var systemTextJsonSerializer = new SystemTextJsonSerializerProvider();
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products").SerializerProvider(systemTextJsonSerializer).Get();

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
            })
            .Run();
    }

    [Test]
    public async Task Verify_Using_Newtonsoft()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var newtonsoftSerializer = new NewtonsoftSerializerProvider();
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products").SerializerProvider(newtonsoftSerializer).Get();

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
            })
            .Run();
    }
}
