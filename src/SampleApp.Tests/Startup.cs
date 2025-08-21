using FuznLabs.TestFuzn;
using FuznLabs.TestFuzn.Plugins.Http;
using FuznLabs.TestFuzn.Plugins.Playwright;

namespace SampleApp.Tests;

[TestClass]
public class Startup : BaseStartup
{
    public override TestFusionConfiguration Configuration()
    {
        var configuration = new TestFusionConfiguration();
        configuration.TestSuiteName = "SampleApp.Tests";

        configuration.UseHttp();
        configuration.UsePlaywright();

        return configuration;
    }
}
