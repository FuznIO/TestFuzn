using Fuzn.TestFuzn.Plugins.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Tests.DefaultHttpClient;

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
        configuration.Suite = new SuiteInfo
        {
            Id = "TestFuzn.Tests.DefaultHttpClient",
            Name = "TestFuzn Default HTTP Client Tests",
        };

        configuration.UseHttp();
    }
}
