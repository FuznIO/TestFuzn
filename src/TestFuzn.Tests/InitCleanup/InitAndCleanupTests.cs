
namespace Fuzn.TestFuzn.Tests.InitCleanup;

[FeatureTest]
public class InitAndCleanupTests : BaseFeatureTest, IInitScenarioTestMethod, ICleanupScenarioTestMethod
{
    public override string FeatureName => "Feature-Init-Cleanup";
    private bool _initScenarioTestMethodCalled = false;
    private bool _cleanupScenarioTestMethodCalled = false;

    public Task InitScenarioTestMethod(Context context)
    {
        _initScenarioTestMethodCalled = true;
        return Task.CompletedTask;
    }

    public Task CleanupScenarioTestMethod(Context context)
    {
        _cleanupScenarioTestMethodCalled = true;
        return Task.CompletedTask;
    }


    [ScenarioTest]
    public async Task Verify_lifecycle_sync_methods_with_context_feature()
    {
        var initScenarioCalled = false;
        var initIterationCalled = false;
        var cleanupIterationCalled = false;
        var cleanupScenarioCalled = false;

        await Scenario()
            .InitScenario((context) =>
            {
                initScenarioCalled = true;
            })
            .InitIteration((context) =>
            {
                initIterationCalled = true;
            })
            .Step("Step 1", (context) => { })
            .CleanupIteration((context) =>
            {
                cleanupIterationCalled = true;
            })
            .CleanupScenario((context) =>
            {
                cleanupScenarioCalled = true;
            })
            .Run();

        Assert.IsTrue(initScenarioCalled);
        Assert.IsTrue(initIterationCalled);
        Assert.IsTrue(cleanupIterationCalled);
        Assert.IsTrue(cleanupScenarioCalled);
        Assert.IsTrue(_initScenarioTestMethodCalled);
        Assert.IsTrue(_cleanupScenarioTestMethodCalled);
    }

    [ScenarioTest]
    public async Task Verify_lifecycle_sync_methods_with_context_load()
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
        Assert.IsTrue(_initScenarioTestMethodCalled);
        Assert.IsTrue(_cleanupScenarioTestMethodCalled);
    }

    [ScenarioTest]
    public async Task Verify_lifecycle_async_methods_with_context_feature()
    {
        var initScenarioCalled = false;
        var initIterationCalled = false;
        var cleanupIterationCalled = false;
        var cleanupScenarioCalled = false;

        await Scenario()
            .InitScenario(async (context) =>
            {
                initScenarioCalled = true;
                await Task.CompletedTask;
            })
            .InitIteration(async (context) =>
            {
                initIterationCalled = true;
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .CleanupIteration(async (context) =>
            {
                cleanupIterationCalled = true;
                await Task.CompletedTask;
            })
            .CleanupScenario(async (context) =>
            {
                cleanupScenarioCalled = true;
                await Task.CompletedTask;
            })
            .Run();

        Assert.IsTrue(initScenarioCalled);
        Assert.IsTrue(initIterationCalled);
        Assert.IsTrue(cleanupIterationCalled);
        Assert.IsTrue(cleanupScenarioCalled);
    }

    [ScenarioTest]
    public async Task Verify_lifecycle_async_methods_with_context_load()
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
