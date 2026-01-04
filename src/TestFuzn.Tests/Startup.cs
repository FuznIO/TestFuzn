using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Plugins.Playwright;
using Fuzn.TestFuzn.Plugins.WebSocket;
using Fuzn.TestFuzn.Sinks.InfluxDB;

namespace Fuzn.TestFuzn.Tests;

[TestClass]
public class Startup : IStartup, IBeforeSuite, IAfterSuite
{
    public static bool BeforeSuiteExecuted = false;
    public static bool AfterSuiteExecuted = false;
    
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
    {
        await TestFuznIntegration.Init(testContext);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        await TestFuznIntegration.Cleanup(testContext);
        Assert.IsTrue(AfterSuiteExecuted);
    }

    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.Suite = new SuiteInfo
        {
            Id = "TestFuzn.Tests",
            Name = "TestFuzn Tests",
            Metadata = new Dictionary<string, string>
            {
                { "Owner", "Fuzn" },
                { "OwnerID", "123" },
            }
        };
        configuration.LoggingVerbosity = LoggingVerbosity.Normal;
        configuration.UsePlaywright(c =>
        {
            c.BrowserTypes = new List<string> { "chromium" };
            c.ConfigureBrowserLaunchOptions = (browserType, launchOptions) =>
            {
                launchOptions.Args =
                [
                    "--disable-web-security",
                    "--disable-features=IsolateOrigins,site-per-process"
                ];
                launchOptions.Headless = true;
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
        configuration.UseHttp();
        configuration.UseWebSocket(config =>
        {
            config.DefaultConnectionTimeout = TimeSpan.FromSeconds(10);
            config.DefaultKeepAliveInterval = TimeSpan.FromSeconds(30);
            config.LogFailedConnectionsToTestConsole = true;
        });
        configuration.UseInfluxDB();
        // Only one serializer can be used, last one set wins, have these 2 lines just to show both options.
        configuration.SerializerProvider = new NewtonsoftSerializerProvider();
        configuration.SerializerProvider = new SystemTextJsonSerializerProvider();
    }

    public Task BeforeSuite(Context context)
    {
        BeforeSuiteExecuted = true;
        return Task.CompletedTask;
    }

    public Task AfterSuite(Context context)
    {
        AfterSuiteExecuted = true;
        return Task.CompletedTask;
    }
}
