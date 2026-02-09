using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests.Http;

[TestClass]
public class SerializerProviderTests : Test
{
    [Test]
    public async Task Verify_Default_Serialization()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var token = await HttpTests.GetAuthToken(context);

                var response = await context.CreateHttpRequest("/api/Products")
                                .WithAuthBearer(token)
                                .Get<List<Product>>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.IsNotNull(response.Data);
                Assert.IsNotEmpty(response.Data, "Expected more than one product to be returned.");
            })
            .Run();
    }

    [Test]
    public async Task Verify_Custom_JsonOptions()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var token = await HttpTests.GetAuthToken(context);

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var response = await context.CreateHttpRequest("/api/Products")
                                        .WithAuthBearer(token)
                                        .WithJsonOptions(options)
                                        .Get<List<Product>>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.IsNotNull(response.Data);
                Assert.IsNotEmpty(response.Data, "Expected more than one product to be returned.");
            })
            .Run();
    }
}
