using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Sinks.InfluxDB;

namespace Fuzn.TestFuzn.Tests;

public class Startup : BaseStartup
{
    public override TestFuznConfiguration Configuration()
    {
        var configuration = new TestFuznConfiguration();
        configuration.TestSuite = new TestSuiteInfo
        {
            Id = "TestFuzn.Tests.Failing",
            Name = "TestFuzn Tests Failing",
        };
        //configuration.UsePlaywright();
        configuration.UseHttp();
        configuration.UseInfluxDb();
        // Only one serializer can be used, last one set wins, have these 2 lines just to show both options.
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
