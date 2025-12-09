using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Plugins.Playwright;

namespace SampleApp.Tests;

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

    public TestFuznConfiguration Configuration()
    {
        var configuration = new TestFuznConfiguration();
        configuration.UseHttp();
        configuration.UsePlaywright(c =>
        {
            c.BrowserTypesToUse = new List<string> { "chromium" };
            c.ConfigureBrowserLaunchOptions = (browserType, launchOptions) =>
            {
                launchOptions.Args =
                [
                    "--disable-web-security",
                    "--disable-features=IsolateOrigins,site-per-process"
                ];
                launchOptions.Headless = false;
            };
            c.ConfigureContextOptions = (browserType, contextOptions) =>
            {
                contextOptions.IgnoreHTTPSErrors = true;
            };
            c.AfterPageCreated = (browserType, page) =>
            {
                page.SetDefaultTimeout(10000);
                return Task.CompletedTask;
            };
        });

        return configuration;
    }

    public Task InitGlobal(Context context)
    {
        return Task.CompletedTask;
    }

    public Task CleanupGlobal(Context context)
    {
        return Task.CompletedTask;
    }
}
