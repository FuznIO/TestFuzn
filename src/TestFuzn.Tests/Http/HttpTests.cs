using Fuzn.TestFuzn.Plugins.Http;
using System.Security.Cryptography;
namespace Fuzn.TestFuzn.Tests.Http;

[TestClass]
public class HttpTests : Test
{
    public static async Task<string> GetAuthToken(Context context)
    {
        var response = await context.CreateHttpRequest($"https://localhost:44316/api/Auth/token")
            .Body(new { Username = "admin", Password = "admin123" })
            .Post();

        Assert.IsTrue(response.Ok, $"Authentication failed: {response.StatusCode}");
        var tokenResponse = response.BodyAs<TokenResponse>();
        Assert.IsNotNull(tokenResponse);
        Assert.IsFalse(string.IsNullOrEmpty(tokenResponse.Token), "Token should not be empty");
        return tokenResponse.Token;
    }

    [Test]
    public async Task Verify_Using_SystemText_Set_During_Startup()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var token = await GetAuthToken(context);
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                                .AuthBearer(token)
                                .Get();

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsNotNull(products);
                Assert.IsNotEmpty(products, "Expected more than one product to be returned.");
            })
            .Run();
    }

    [Test]
    public async Task Verify_Using_SystemText_Override()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var token = await GetAuthToken(context);
                var systemTextJsonSerializer = new SystemTextJsonSerializerProvider();
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                    .AuthBearer(token)
                    .SerializerProvider(systemTextJsonSerializer)
                    .Get();

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsNotEmpty(products, "Expected more than one product to be returned.");
            })
            .Run();
    }

    [Test]
    public async Task Verify_Using_Newtonsoft()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var token = await GetAuthToken(context);
                var newtonsoftSerializer = new NewtonsoftSerializerProvider();
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                                        .AuthBearer(token)
                                        .SerializerProvider(newtonsoftSerializer).Get();

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsNotEmpty(products, "Expected more than one product to be returned.");
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
        var token = "";

        await Scenario()
            .BeforeScenario(async context =>
            {
                token = await GetAuthToken(context);
            })
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                                .AuthBearer(token)
                                .Get();

                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsNotNull(products);
                Assert.IsNotEmpty(products, "Expected more than one product to be returned.");
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15)))
            .Run();
    }

    [Test]
    public async Task Max_Load()
    {
        await Scenario()
            .Step("Call ping and expect pong", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Ping").Get();

                Assert.IsTrue(response.Ok);
                Assert.AreEqual("Pong", response.BodyAs<string>());
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

                var token = await GetAuthToken(context);
                var postResponse = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                    .AuthBearer(token)
                    .Body($@"
                        {{
                            ""id"": ""{productId}"",
                            ""name"": ""{name}"",
                            ""price"": {price}
                        }}")
                    .Post();

                Assert.IsTrue(postResponse.Ok);

                var getResponse = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{productId}")
                                    .AuthBearer(token)
                                    .Get();

                Assert.IsTrue(getResponse.Ok);
                var product = getResponse.BodyAsJson();
                Assert.IsNotNull(product);
                Assert.AreEqual(productId.ToString(), product.id.ToString());
                Assert.AreEqual(name, product.name);
                Assert.AreEqual(price, product.price);
            })
            .Run();
    }
}
