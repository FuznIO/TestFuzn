using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn;

namespace SampleApp.Tests;

public static class TokenHelper
{
    public static async Task<string> GetAuthToken(IterationContext context)
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

    private record TokenResponse(string Token, DateTime ExpiresAt);
}
