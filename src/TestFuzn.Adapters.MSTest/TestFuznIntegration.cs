using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

public static class TestFuznIntegration
{
    public static async Task Init(TestContext testContext)
    {
        var testFramework = new MsTestRunnerAdapter(testContext);

        await TestFuznIntegrationCore.Init(testFramework);
    }

    public static async Task Cleanup(TestContext testContext)
    {
        var testFramework = new MsTestRunnerAdapter(testContext);

        await TestFuznIntegrationCore.Cleanup(testFramework);
    }
}
