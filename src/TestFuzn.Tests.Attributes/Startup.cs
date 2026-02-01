namespace Fuzn.TestFuzn.Tests.Attributes;

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
        Assert.IsTrue(AssertStandardReport.IsExecuted, "Standard report was not executed.");
    }

    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.AddStandardReport(new AssertStandardReport());
    }
}
