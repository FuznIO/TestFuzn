using FuznLabs.TestFuzn.Plugins.Playwright;
using FuznLabs.TestFuzn.Plugins.Http;
using FuznLabs.TestFuzn.Plugins.Newtonsoft;
using FuznLabs.TestFuzn.Sinks.InfluxDB;

namespace FuznLabs.TestFuzn.Tests;

public class Startup : BaseStartup
{
    public override TestFusionConfiguration Configuration()
    {
        var configuration = ConfigurationManager.LoadConfiguration();
        configuration.EnvironmentName = "development";
        configuration.UsePlaywright();
        configuration.UseHttp();
        configuration.UseInfluxDb();
        configuration.UseSystemTextJsonSerializer(options =>
        {
            options.Priority = 0;
        });
        configuration.UseNewtonsoftSerializer(pluginConfiguration =>
        {
            pluginConfiguration.Priority = 1;
        });
        return configuration;
    }
}
