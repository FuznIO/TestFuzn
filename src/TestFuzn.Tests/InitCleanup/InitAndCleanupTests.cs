namespace Fuzn.TestFuzn.Tests.InitCleanup;

[TestClass]
public class InitAndCleanupTests : TestBase, ISetupTest, ITeardownTest
{
    public override GroupInfo Group => new() { Name = "Feature-Init-Cleanup" };
    private bool _initScenarioTestMethodCalled = false;
    private bool _cleanupScenarioTestMethodCalled = false;

    public Task SetupTest(Context context)
    {
        _initScenarioTestMethodCalled = true;
        return Task.CompletedTask;
    }

    public Task TeardownTest(Context context)
    {
        _cleanupScenarioTestMethodCalled = true;
        return Task.CompletedTask;
    }


    [Test]
    public async Task Verify_lifecycle_sync_methods_with_context_feature()
    {
        var initScenarioCalled = false;
        var initIterationCalled = false;
        var cleanupIterationCalled = false;
        var cleanupScenarioCalled = false;

        await Scenario()
            .SetupScenario((context) =>
            {
                initScenarioCalled = true;
            })
            .SetupIteration((context) =>
            {
                initIterationCalled = true;
            })
            .Step("Step 1", (context) => { })
            .TeardownIteration((context) =>
            {
                cleanupIterationCalled = true;
            })
            .TeardownScenario((context) =>
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

    [Test]
    public async Task Verify_lifecycle_sync_methods_with_context_load()
    {
        var initScenarioCalled = false;
        var initIterationCalled = 0;
        var cleanupIterationCalled = 0;
        var cleanupScenarioCalled = false;

        await Scenario()
            .SetupScenario((context) =>
            {
                initScenarioCalled = true;
            })
            .SetupIteration((context) =>
            {
                Interlocked.Add(ref initIterationCalled, 1);
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .TeardownIteration((context) =>
            {
                Interlocked.Add(ref cleanupIterationCalled, 1);
            })
            .TeardownScenario((context) =>
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

    [Test]
    public async Task Verify_lifecycle_async_methods_with_context_feature()
    {
        var initScenarioCalled = false;
        var initIterationCalled = false;
        var cleanupIterationCalled = false;
        var cleanupScenarioCalled = false;

        await Scenario()
            .SetupScenario(async (context) =>
            {
                initScenarioCalled = true;
                await Task.CompletedTask;
            })
            .SetupIteration(async (context) =>
            {
                initIterationCalled = true;
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .TeardownIteration(async (context) =>
            {
                cleanupIterationCalled = true;
                await Task.CompletedTask;
            })
            .TeardownScenario(async (context) =>
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

    [Test]
    public async Task Verify_lifecycle_async_methods_with_context_load()
    {
        var initScenarioCalled = false;
        var initIterationCalled = 0;
        var cleanupIterationCalled = 0;
        var cleanupScenarioCalled = false;

        await Scenario()
            .SetupScenario(async (context) =>
            {
                initScenarioCalled = true;
                await Task.CompletedTask;
            })
            .SetupIteration(async (context) =>
            {
                Interlocked.Add(ref initIterationCalled, 1);
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .TeardownIteration(async (context) =>
            {
                Interlocked.Add(ref cleanupIterationCalled, 1);
                await Task.CompletedTask;
            })
            .TeardownScenario(async (context) =>
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
