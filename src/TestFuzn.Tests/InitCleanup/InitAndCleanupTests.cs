
namespace Fuzn.TestFuzn.Tests.InitCleanup;

[FeatureTest]
public class InitAndCleanupTests : BaseFeatureTest
{
    public override string FeatureName => "Feature-Init-Cleanup";
    private bool _beforeEachScenarioTestCalled = false;
    private bool _afterEachScenarioTestCalled = false;

    public override Task InitScenarioTest(Context context)
    {
        _beforeEachScenarioTestCalled = true;
        return Task.CompletedTask;
    }

    public override Task CleanupScenarioTest(Context context)
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
            .Init((context) =>
            {
                initCalled = true;
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .CleanupAfterEachIteration((context) =>
            {
                Interlocked.Add(ref cleanupIterationCalled, 1);
            })
            .CleanupAfterScenario((context) =>
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
            .Init(async (context) =>
            {
                initCalled = true;
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .CleanupAfterEachIteration(async (context) =>
            {
                Interlocked.Add(ref cleanupIterationCalled, 1);
            })
            .CleanupAfterScenario(async (context) =>
            {
                cleanupCalled = true;
            })
            .Run();

        Assert.IsTrue(initCalled);
        Assert.AreEqual(3, cleanupIterationCalled);
        Assert.IsTrue(cleanupCalled);
    }
}
