namespace Fuzn.TestFuzn.Tests;

[TestClass]
public class TestFuznMsTestIntegration
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
    {
        var testFramework = new MsTestAdapter(testContext);

        await TestFuznIntegration.InitGlobal(testFramework);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        var testFramework = new MsTestAdapter(testContext);

        await TestFuznIntegration.CleanupGlobal(testFramework);
    }
}
