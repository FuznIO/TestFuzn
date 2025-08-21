namespace FuznLabs.TestFuzn.Tests;

[TestClass]
public class TestFusionMsTestIntegration
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
    {
        var testFramework = new MsTestAdapter(testContext);

        await TestFusionIntegration.InitGlobal(testFramework);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        var testFramework = new MsTestAdapter(testContext);

        await TestFusionIntegration.CleanupGlobal(testFramework);
    }
}
