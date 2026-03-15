using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides MSTest integration entry points for initializing and cleaning up a TestFuzn test suite.
/// </summary>
public static class TestFuznIntegration
{
    /// <summary>
    /// Initializes the TestFuzn test suite using the specified <typeparamref name="TStartup"/> class.
    /// Call from an <c>[AssemblyInitialize]</c> method.
    /// </summary>
    /// <typeparam name="TStartup">The <see cref="IStartup"/> implementation to configure the suite.</typeparam>
    /// <param name="testContext">The MSTest <see cref="TestContext"/> for the current test run.</param>
    public static async Task Init<TStartup>(TestContext testContext)
        where TStartup : IStartup, new()
    {
        var testFramework = new MsTestRunnerAdapter(testContext);

        var testSession = new TestSession("default");
        TestSession.Default = testSession;
        await testSession.Init<TStartup>(testFramework);
    }

    /// <summary>
    /// Cleans up the TestFuzn test suite and writes final reports. Call from an <c>[AssemblyCleanup]</c> method.
    /// </summary>
    /// <param name="testContext">The MSTest <see cref="TestContext"/> for the current test run.</param>
    public static async Task Cleanup(TestContext testContext)
    {
        var testSession = TestSession.Default;
        if (testSession == null)
            return;

        var testFramework = new MsTestRunnerAdapter(testContext);

        await testSession.Cleanup(testFramework);
    }
}
