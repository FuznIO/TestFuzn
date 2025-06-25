using TestFusion.Plugins.Playwright;
using TestFusion.Plugins.Http;
using TestFusion.Sinks.InfluxDB;

namespace TestFusion.Tests;

public class Startup : BaseStartup
{
    public override TestFusionConfiguration Configuration()
    {
        var configuration = ConfigurationManager.LoadConfiguration();
        configuration.EnvironmentName = "development";
        configuration.UsePlaywright();
        configuration.UseHttp();
        configuration.UseInfluxDb();
        return configuration;
    }
}
