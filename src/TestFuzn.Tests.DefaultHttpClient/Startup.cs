using Fuzn.TestFuzn.Plugins.Http;

namespace Fuzn.TestFuzn.Tests.DefaultHttpClient;

public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.Suite = new SuiteInfo
        {
            Id = "TestFuzn.Tests.DefaultHttpClient",
            Name = "TestFuzn Default HTTP Client Tests",
        };

        configuration.UseHttp();
    }
}
