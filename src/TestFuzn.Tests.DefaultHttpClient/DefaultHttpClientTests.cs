using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests.DefaultHttpClient;

[TestClass]
public class DefaultHttpClientTests : Test
{
    [Test]
    public async Task Verify_DefaultHttpClient_Works()
    {
        await Scenario()
            .Step("Call ping endpoint and verify response", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:7058/api/Ping").Get<string>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.AreEqual("Pong", response.Data);
            })
            .Run();
    }
}
