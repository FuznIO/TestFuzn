using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn;

namespace SampleApp.Tests;

public static class TokenHelper
{
    public static async Task<string> GetAuthToken(IterationContext context)
    {
        var response = await context.CreateRequest($"https://localhost:44316/api/Auth/token")
            .WithContent(new { Username = "admin", Password = "admin123" })
            .Post<TokenResponse>();

        Assert.IsTrue(response.IsSuccessful, $"Authentication failed: {response.StatusCode}");
        Assert.IsNotNull(response.Data);
        Assert.IsFalse(string.IsNullOrEmpty(response.Data.Token), "Token should not be empty");
        return response.Data.Token;
    }

    private record TokenResponse(string Token, DateTime ExpiresAt);
}
