using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Plugins.Newtonsoft;
using Fuzn.TestFuzn.Plugins.Playwright;
using Fuzn.TestFuzn.Plugins.WebSocket;
using Fuzn.TestFuzn.Sinks.InfluxDB;

namespace Fuzn.TestFuzn.Tests;

public class Startup : BaseStartup
{
    public override TestFuznConfiguration Configuration()
    {
        var configuration = ConfigurationManager.LoadConfiguration();
        configuration.TestSuite = new TestSuiteInfo
        {
            Id = "TestFuzn.Tests",
            Name = "TestFuzn Tests",
            Metadata = new Dictionary<string, string>
            {
                { "Owner", "Fuzn" },
                { "OwnerID", "123" },
            }
        };
        configuration.EnvironmentName = "development";
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
        configuration.UseHttp();
        configuration.UseWebSocket(config =>
        {
            config.DefaultConnectionTimeout = TimeSpan.FromSeconds(10);
            config.DefaultKeepAliveInterval = TimeSpan.FromSeconds(30);
            config.LogFailedConnectionsToTestConsole = true;
        });
        configuration.UseInfluxDb();
        // Only one serializer can be used, last one set wins, have these 2 lines just to show both options.
        configuration.SerializerProvider = new NewtonsoftSerializerProvider();
        configuration.SerializerProvider = new SystemTextJsonSerializerProvider();
        return configuration;
    }

    public override Task InitGlobal(Context context)
    {
        return base.InitGlobal(context);
    }

    public override Task CleanupGlobal(Context context)
    {
        return base.CleanupGlobal(context);
    }
}
