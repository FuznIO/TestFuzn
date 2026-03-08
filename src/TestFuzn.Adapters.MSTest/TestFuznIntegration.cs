using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides MSTest integration entry points for initializing and cleaning up a TestFuzn test suite.
/// </summary>
public static class TestFuznIntegration
{
    /// <summary>
    /// Initializes the TestFuzn test suite. Call from an <c>[AssemblyInitialize]</c> method.
    /// </summary>
    /// <param name="testContext">The MSTest <see cref="TestContext"/> for the current test run.</param>
    public static async Task Init(TestContext testContext)
    {
        var testFramework = new MsTestRunnerAdapter(testContext);

        await TestFuznIntegrationCore.Init(testFramework);
    }

    /// <summary>
    /// Cleans up the TestFuzn test suite and writes final reports. Call from an <c>[AssemblyCleanup]</c> method.
    /// </summary>
    /// <param name="testContext">The MSTest <see cref="TestContext"/> for the current test run.</param>
    public static async Task Cleanup(TestContext testContext)
    {
        var testFramework = new MsTestRunnerAdapter(testContext);

        await TestFuznIntegrationCore.Cleanup(testFramework);
    }
}
