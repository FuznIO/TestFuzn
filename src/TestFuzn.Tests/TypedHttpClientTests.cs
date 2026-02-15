using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests;

[TestClass]
public class TypedHttpClientTests : Test
{
    [Test]
    public async Task TypedHttpClientIsUsed()
    {
        await Scenario("Verify typed HTTP client usage")
            .Step("Make an HTTP request with typed client and verify it was used", context =>
            {
                var initialCount = TestHttpClient.UsageCount;

                // Use the typed TestHttpClient explicitly
                context.CreateHttpRequest<TestHttpClient>("https://localhost:7058/api/ping");

                Assert.AreEqual(initialCount + 1, TestHttpClient.UsageCount,
                    "Typed HTTP client should have been used");
            })
            .Run();
    }
}
