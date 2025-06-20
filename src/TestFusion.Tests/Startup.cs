using TestFusion.BrowserTesting;
using TestFusion.HttpTesting;

namespace TestFusion.Tests;

public class Startup : BaseStartup
{
    public override TestFusionConfiguration Configuration()
    {
        var configuration = ConfigurationManager.LoadConfiguration();
        configuration.EnvironmentName = "development";
        configuration.UseBrowserTesting();
        configuration.UseHttpTesting();
        configuration.UseInfluxDbSink();
        configuration.UseDefaultReports();

        return configuration;
    }
}
