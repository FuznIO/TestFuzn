using Fuzn.TestFuzn.Plugins.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Tests.CustomHttpClient;

[TestClass]
public class Startup : IStartup
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
    {
        await TestFuznIntegration.Init(testContext);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        await TestFuznIntegration.Cleanup(testContext);
    }

    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.UseHttp(httpConfig =>
        {
            // Register the custom HTTP client with HttpClientFactory
            httpConfig.Services.AddHttpClient<CustomTestHttpClient>(client =>
            {
            });

            // Switch to using the custom HTTP client instead of the default TestFuznHttpClient
            httpConfig.UseDefaultHttpClient<CustomTestHttpClient>();
        });
    }
}
