
namespace Fuzn.TestFuzn.Tests.InitCleanup;

[FeatureTest]
public class InitAndCleanupTests : BaseFeatureTest
{
    public override string FeatureName => "Feature-Init-Cleanup";
    private bool _beforeEachScenarioTestCalled = false;
    private bool _afterEachScenarioTestCalled = false;

    public override Task InitTestMethod(Context context)
    {
        _beforeEachScenarioTestCalled = true;
        return Task.CompletedTask;
    }

    public override Task CleanupTestMethod(Context context)
    {
        _afterEachScenarioTestCalled = true;
        return Task.CompletedTask;
    }

    [ScenarioTest]
    public async Task Verify_sync_with_context()
    {
        var initCalled = false;
        var cleanupIterationCalled = 0;
        var cleanupCalled = false;

        await Scenario()
            .InitScenario((context) =>
            {
                initCalled = true;
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .CleanupIteration((context) =>
            {
                Interlocked.Add(ref cleanupIterationCalled, 1);
            })
            .CleanupScenario((context) =>
            {
                cleanupCalled = true;
            })
            .Run();

        Assert.IsTrue(initCalled);
        Assert.AreEqual(3, cleanupIterationCalled);
        Assert.IsTrue(cleanupCalled);
        Assert.IsTrue(_beforeEachScenarioTestCalled);
        Assert.IsTrue(_afterEachScenarioTestCalled);
    }

    [ScenarioTest]
    public async Task Verify_async_with_context()
    {
        var initCalled = false;
        var cleanupIterationCalled = 0;
        var cleanupCalled = false;

        await Scenario()
            .InitScenario(async (context) =>
            {
                initCalled = true;
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .CleanupIteration(async (context) =>
            {
                Interlocked.Add(ref cleanupIterationCalled, 1);
            })
            .CleanupScenario(async (context) =>
            {
                cleanupCalled = true;
            })
            .Run();

        Assert.IsTrue(initCalled);
        Assert.AreEqual(3, cleanupIterationCalled);
        Assert.IsTrue(cleanupCalled);
    }
}
