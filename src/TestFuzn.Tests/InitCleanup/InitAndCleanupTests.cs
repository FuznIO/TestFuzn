
namespace Fuzn.TestFuzn.Tests.InitCleanup;

[FeatureTest]
public class InitAndCleanupTests : BaseFeatureTest, ITestMethodInit, ITestMethod
{
    public override string FeatureName => "Feature-Init-Cleanup";
    private bool _beforeEachScenarioTestCalled = false;
    private bool _afterEachScenarioTestCalled = false;

    public Task InitTestMethod(Context context)
    {
        _beforeEachScenarioTestCalled = true;
        return Task.CompletedTask;
    }

    public Task CleanupTestMethod(Context context)
    {
        _afterEachScenarioTestCalled = true;
        return Task.CompletedTask;
    }

    [ScenarioTest]
    public async Task Verify_lifecycle_sync_methods_with_context()
    {
        var initScenarioCalled = false;
        var initIterationCalled = 0;
        var cleanupIterationCalled = 0;
        var cleanupScenarioCalled = false;

        await Scenario()
            .InitScenario((context) =>
            {
                initScenarioCalled = true;
            })
            .InitIteration((context) =>
            {
                Interlocked.Add(ref initIterationCalled, 1);
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .CleanupIteration((context) =>
            {
                Interlocked.Add(ref cleanupIterationCalled, 1);
            })
            .CleanupScenario((context) =>
            {
                cleanupScenarioCalled = true;
            })
            .Run();

        Assert.IsTrue(initScenarioCalled);
        Assert.AreEqual(3, initIterationCalled);
        Assert.AreEqual(3, cleanupIterationCalled);
        Assert.IsTrue(cleanupScenarioCalled);
        Assert.IsTrue(_beforeEachScenarioTestCalled);
        Assert.IsTrue(_afterEachScenarioTestCalled);
    }

    [ScenarioTest]
    public async Task Verify_lifecycle_async_methods_with_context()
    {
        var initScenarioCalled = false;
        var initIterationCalled = 0;
        var cleanupIterationCalled = 0;
        var cleanupScenarioCalled = false;

        await Scenario()
            .InitScenario(async (context) =>
            {
                initScenarioCalled = true;
                await Task.CompletedTask;
            })
            .InitIteration(async (context) =>
            {
                Interlocked.Add(ref initIterationCalled, 1);
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .CleanupIteration(async (context) =>
            {
                Interlocked.Add(ref cleanupIterationCalled, 1);
                await Task.CompletedTask;
            })
            .CleanupScenario(async (context) =>
            {
                cleanupScenarioCalled = true;
                await Task.CompletedTask;
            })
            .Run();

        Assert.IsTrue(initScenarioCalled);
        Assert.AreEqual(3, initIterationCalled);
        Assert.AreEqual(3, cleanupIterationCalled);
        Assert.IsTrue(cleanupScenarioCalled);
    }
}
