using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;

namespace SampleApp.Tests;

[TestClass]
public class AuthHttpTests : Test
{
    private const string BaseUrl = "https://localhost:44316";

    [Test]
    public async Task Verify_authentication_returns_valid_token()
    {
        await Scenario()
            .Step("Authenticate and retrieve JWT token", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Auth/token")
                    .Body(new { Username = "admin", Password = "admin123" })
                    .Post();

                Assert.IsTrue(response.Ok, $"Authentication failed: {response.StatusCode}");
                var tokenResponse = response.BodyAs<TokenResponse>();
                Assert.IsFalse(string.IsNullOrEmpty(tokenResponse.Token), "Token should not be empty");
            })
            .Run();
    }
}

record TokenResponse(string Token, DateTime ExpiresAt);
