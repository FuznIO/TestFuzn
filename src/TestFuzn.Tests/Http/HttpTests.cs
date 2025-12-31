using Fuzn.TestFuzn.Plugins.Http;
using System.Security.Cryptography;
namespace Fuzn.TestFuzn.Tests.Http;

[TestClass]
public class GetProductsE2ETests : TestBase
{
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
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                                        .SerializerProvider(newtonsoftSerializer).Get();

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
            })
            .Run();
    }

    [Test]
    public async Task Ping_LoadTest()
    {
        await Scenario()
            .Step("Verify ping returns pong", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Ping").Get();

                Assert.IsTrue(response.Ok);
                Assert.AreEqual("Pong", response.BodyAs<string>());
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15)))
            .Run();
    }

    [Test]
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
            .Load().Simulations((context, simulations) => simulations.FixedLoad(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15)))
            .Run();
    }

    [Test]
    [Skip]
    public async Task Max_Load()
    {
        await Scenario()
            .Step("Call ping and expect pong", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Ping").Get();

                Assert.IsTrue(response.Ok);
                Assert.IsTrue(response.BodyAs<string>() == "Pong");
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(500, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15)))
            .Run();
    }

    [Test]
    public async Task Verify_Raw_JsonString()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                var productId = Guid.NewGuid();
                var name = $"ProductName_{Guid.NewGuid()}";
                var price = RandomNumberGenerator.GetInt32(10, 2000);

                var postResponse = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                    .Body($@"
                        {{
                            ""id"": ""{productId}"",
                            ""name"": ""{name}"",
                            ""price"": {price}
                        }}")
                    .Post();

                Assert.IsTrue(postResponse.Ok);

                var getResponse = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{productId}")
                    .Get();

                Assert.IsTrue(getResponse.Ok);
                var product = getResponse.BodyAsJson();
                Assert.AreEqual(productId.ToString(), product.id.ToString());
                Assert.AreEqual(name, product.name);
                Assert.AreEqual(price, product.price);
            })
            .Run();
    }
}
