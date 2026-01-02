using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Sinks.InfluxDB;

namespace Fuzn.TestFuzn.Tests;

[TestClass]
public class Startup : IStartup
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
    {
        await TestFuznIntegration.Init(testContext);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        await TestFuznIntegration.Cleanup(testContext);
    }

    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.Suite = new SuiteInfo
        {
            Id = "TestFuzn.Tests.Failing",
            Name = "TestFuzn Tests Failing",
        };
        //configuration.UsePlaywright();
        configuration.UseHttp();
        configuration.UseInfluxDB();
        // Only one serializer can be used, last one set wins, have these 2 lines just to show both options.
        configuration.SerializerProvider = new SystemTextJsonSerializerProvider();
    }

    public Task InitGlobal(Context context)
    {
        return Task.CompletedTask;
    }

    public Task CleanupGlobal(Context context)
    {
        return Task.CompletedTask;
    }
}
