using TestFusion;

namespace SampleApp.Tests;

[TestClass]
public class Startup : BaseStartup
{
    public override TestFusionConfiguration Configuration()
    {
        var configuration = new TestFusionConfiguration();
        configuration.TestSuiteName = "SampleApp.Tests";

        return configuration;
    }
}
