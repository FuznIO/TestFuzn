using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

public static class TestFuznIntegration
{
    [AssemblyInitialize]
    public static async Task Init(TestContext testContext)
    {
        var testFramework = new MsTestRunnerAdapter(testContext);

        await TestFuznIntegrationCore.Init(testFramework);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        var testFramework = new MsTestRunnerAdapter(testContext);

        await TestFuznIntegrationCore.Cleanup(testFramework);
    }
}
