using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Plugins.Playwright;

namespace SampleApp.Tests;

[TestClass]
public class Startup : BaseStartup
{
    public override TestFuznConfiguration Configuration()
    {
        var configuration = new TestFuznConfiguration();
        configuration.TestSuite.Name = "SampleApp.Tests";

        configuration.UseHttp();
        configuration.UsePlaywright();

        return configuration;
    }
}
