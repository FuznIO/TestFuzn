using Fuzn.TestFuzn.Plugins.Playwright;
using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Plugins.Newtonsoft;
using Fuzn.TestFuzn.Sinks.InfluxDB;

namespace Fuzn.TestFuzn.Tests;

public class Startup : BaseStartup
{
    public override TestFusionConfiguration Configuration()
    {
        var configuration = ConfigurationManager.LoadConfiguration();
        configuration.TestSuite = new TestSuiteInfo
        {
            Id = "TestFuzn.Tests",
            Name = "TestFuzion Tests",
            Metadata = new Dictionary<string, string>
            {
                { "Owner", "Fuzn" },
                { "Numeric-Id", "123" },
            }
        };
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

    public override Task InitGlobal(Context context)
    {
        return base.InitGlobal(context);
    }

    public override Task CleanupGlobal(Context context)
    {
        return base.CleanupGlobal(context);
    }
}
