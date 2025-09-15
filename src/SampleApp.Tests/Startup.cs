using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Plugins.Playwright;

namespace SampleApp.Tests;

[TestClass]
public class Startup : BaseStartup
{
    public override TestFusionConfiguration Configuration()
    {
        var configuration = new TestFusionConfiguration();
        configuration.TestSuite.Name = "SampleApp.Tests";

        configuration.UseHttp();
        configuration.UsePlaywright();

        return configuration;
    }
}
