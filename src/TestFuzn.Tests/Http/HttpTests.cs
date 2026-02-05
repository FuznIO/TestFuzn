using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;
using System.Security.Cryptography;

namespace Fuzn.TestFuzn.Tests.Http;

[TestClass]
public class HttpTests : Test
{
    public static async Task<string> GetAuthToken(Context context)
    {
        var response = await context.CreateHttpRequest($"https://localhost:44316/api/Auth/token")
            .WithContent(new { Username = "admin", Password = "admin123" })
            .Post<TokenResponse>();

        Assert.IsTrue(response.IsSuccessful, $"Authentication failed: {response.StatusCode}");
        Assert.IsNotNull(response.Data);
        Assert.IsFalse(string.IsNullOrEmpty(response.Data.Token), "Token should not be empty");
        return response.Data.Token;
    }

    [Test]
    public async Task Verify_Using_SystemText_Set_During_Startup()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var token = await GetAuthToken(context);
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                                .WithAuthBearer(token)
                                .Get<List<Product>>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.IsNotNull(response.Data);
                Assert.IsNotEmpty(response.Data, "Expected more than one product to be returned.");
            })
            .Run();
    }

    [Test]
    public async Task Verify_Using_Custom_JsonOptions()
    {
        await Scenario()
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                var token = await GetAuthToken(context);
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                    .WithAuthBearer(token)
                    .WithJsonOptions(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    .Get<List<Product>>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.IsNotNull(response.Data);
                Assert.IsNotEmpty(response.Data, "Expected more than one product to be returned.");
            })
            .Run();
    }

    [Test]
    public async Task Ping_LoadTest()
    {
        await Scenario()
            .Step("Verify ping returns pong", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Ping").Get<string>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.AreEqual("Pong", response.Data);
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
                                .WithAuthBearer(token)
                                .Get<List<Product>>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.IsNotNull(response.Data);
                Assert.IsNotEmpty(response.Data, "Expected more than one product to be returned.");
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
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Ping").Get<string>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.AreEqual("Pong", response.Data);
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
                    .WithAuthBearer(token)
                    .WithContent($@"
                        {{
                            ""id"": ""{productId}"",
                            ""name"": ""{name}"",
                            ""price"": {price}
                        }}")
                    .Post();

                Assert.IsTrue(postResponse.IsSuccessful);

                var getResponse = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{productId}")
                                    .WithAuthBearer(token)
                                    .Get<Product>();

                Assert.IsTrue(getResponse.IsSuccessful);
                Assert.IsNotNull(getResponse.Data);
                Assert.AreEqual(productId, getResponse.Data.Id);
                Assert.AreEqual(name, getResponse.Data.Name);
                Assert.AreEqual(price, getResponse.Data.Price);
            })
            .Run();
    }
}
