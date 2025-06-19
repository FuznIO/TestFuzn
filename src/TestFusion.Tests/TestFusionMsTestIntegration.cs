namespace TestFusion.Tests;

[TestClass]
public class TestFusionMsTestIntegration
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
    {
        var testFramework = new MsTestProvider(testContext);

        await TestFusionIntegration.InitGlobal(testFramework);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        var testFramework = new MsTestProvider(testContext);

        await TestFusionIntegration.CleanupGlobal(testFramework);
    }
}
