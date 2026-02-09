using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests.CustomHttpClient;

[TestClass]
public class CustomHttpClientTests : Test
{
    [Test]
    public async Task CustomHttpClientIsUsed()
    {
        await Scenario("Verify custom HTTP client usage")
            .Step("Make an HTTP request and verify custom client was used", context =>
            {
                var initialCount = CustomTestHttpClient.UsageCount;

                context.CreateHttpRequest("https://localhost:49830/api/ping");

                Assert.AreEqual(initialCount + 1, CustomTestHttpClient.UsageCount,
                    "Custom HTTP client should have been used");
            })
            .Run();
    }
}
