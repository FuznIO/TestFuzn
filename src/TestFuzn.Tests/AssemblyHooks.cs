using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Tests.Lifecycle;

namespace Fuzn.TestFuzn.Tests;

/// <summary>
/// Checks that need all tests to be done. The host cleans up the default session later, when the test session ends.
/// </summary>
[TestClass]
public class AssemblyHooks
{
    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        if (ClassLifecycleTests.AnyTestExecuted)
        {
            Assert.AreEqual(1, ClassLifecycleTests.BeforeClassCallCount, "IBeforeClass.BeforeClass should be called exactly once.");
            Assert.IsTrue(ClassLifecycleTests.AfterClassCalled, "IAfterClass.AfterClass was not called.");
        }

        // Some tests start their own sessions. The host cleans up only the default one.
        var testFramework = new MsTestRunnerAdapter(testContext);
        foreach (var session in TestSessionRegistry.TestSessions)
        {
            await session.Value.Cleanup(testFramework);
        }
    }
}
