using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Plugins.Playwright;
using Fuzn.TestFuzn.Plugins.WebSocket;
using Fuzn.TestFuzn.Sinks.InfluxDB;
using Microsoft.Extensions.DependencyInjection;

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

        configuration.UseHttp(httpConfig =>
        {
            // Register the default HTTP client
            httpConfig.Services.AddHttpClient<DefaultHttpClient>(client =>
            {
                client.BaseAddress = new Uri("https://localhost:7058");
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            // Register typed HTTP client with usage tracking for testing
            httpConfig.Services.AddHttpClient<TestHttpClient>();

            httpConfig.DefaultHttpClient<DefaultHttpClient>();
        });
        configuration.UseWebSocket(config =>
        {
            config.DefaultConnectionTimeout = TimeSpan.FromSeconds(10);
            config.DefaultKeepAliveInterval = TimeSpan.FromSeconds(30);
            config.LogFailedConnectionsToTestConsole = true;
            // Configure custom serializer for WebSocket JSON messages
            // config.SerializerProvider = new NewtonsoftSerializerProvider();
        });
        configuration.UseInfluxDB();
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
