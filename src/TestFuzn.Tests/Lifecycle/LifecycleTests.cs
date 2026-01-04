namespace Fuzn.TestFuzn.Tests.Lifecycle;

[TestClass]
public class LifecycleTests : Test, IBeforeTest, IAfterTest
{
    private bool _beforeTestCalled = false;
    private bool _afterTestCalled = false;

    public Task BeforeTest(Context context)
    {
        _beforeTestCalled = true;
        return Task.CompletedTask;
    }

    public Task AfterTest(Context context)
    {
        _afterTestCalled = true;
        return Task.CompletedTask;
    }
    
    [Test]
    public async Task Verify_lifecycle_BeforeSuite()
    {
        Assert.IsTrue(Startup.BeforeSuiteExecuted);
    }

    [Test]
    public async Task Verify_lifecycle_sync_methods_with_context_standard()
    {
        var beforeScenarioCalled = false;
        var beforeIterationCalled = false;
        var afterIterationCalled = false;
        var afterScenarioCalled = false;

        await Scenario()
            .BeforeScenario((context) =>
            {
                beforeScenarioCalled = true;
            })
            .BeforeIteration((context) =>
            {
                beforeIterationCalled = true;
            })
            .Step("Step 1", (context) => { })
            .AfterIteration((context) =>
            {
                afterIterationCalled = true;
            })
            .AfterScenario((context) =>
            {
                afterScenarioCalled = true;
            })
            .Run();

        Assert.IsTrue(beforeScenarioCalled);
        Assert.IsTrue(beforeIterationCalled);
        Assert.IsTrue(afterIterationCalled);
        Assert.IsTrue(afterScenarioCalled);
        Assert.IsTrue(_beforeTestCalled);
        Assert.IsTrue(_afterTestCalled);
    }

    [Test]
    public async Task Verify_lifecycle_sync_methods_with_context_load()
    {
        var beforeScenarioCalled = false;
        var beforeIterationCalled = 0;
        var afterIterationCalled = 0;
        var afterScenarioCalled = false;

        await Scenario()
            .BeforeScenario((context) =>
            {
                beforeScenarioCalled = true;
            })
            .BeforeIteration((context) =>
            {
                Interlocked.Add(ref beforeIterationCalled, 1);
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .AfterIteration((context) =>
            {
                Interlocked.Add(ref afterIterationCalled, 1);
            })
            .AfterScenario((context) =>
            {
                afterScenarioCalled = true;
            })
            .Run();

        Assert.IsTrue(beforeScenarioCalled);
        Assert.AreEqual(3, beforeIterationCalled);
        Assert.AreEqual(3, afterIterationCalled);
        Assert.IsTrue(afterScenarioCalled);
        Assert.IsTrue(_beforeTestCalled);
        Assert.IsTrue(_afterTestCalled);
    }

    [Test]
    public async Task Verify_lifecycle_async_methods_with_context_standard()
    {
        var beforeScenarioCalled = false;
        var beforeIterationCalled = false;
        var afterIterationCalled = false;
        var afterScenarioCalled = false;

        await Scenario()
            .BeforeScenario(async (context) =>
            {
                beforeScenarioCalled = true;
                await Task.CompletedTask;
            })
            .BeforeIteration(async (context) =>
            {
                beforeIterationCalled = true;
            })
            .Step("Step 1", (context) => { })
            .AfterIteration(async (context) =>
            {
                afterIterationCalled = true;
                await Task.CompletedTask;
            })
            .AfterScenario(async (context) =>
            {
                afterScenarioCalled = true;
                await Task.CompletedTask;
            })
            .Run();

        Assert.IsTrue(beforeScenarioCalled);
        Assert.IsTrue(beforeIterationCalled);
        Assert.IsTrue(afterIterationCalled);
        Assert.IsTrue(afterScenarioCalled);
    }

    [Test]
    public async Task Verify_lifecycle_async_methods_with_context_load()
    {
        var beforeScenarioCalled = false;
        var beforeIterationCalled = 0;
        var afterIterationCalled = 0;
        var afterScenarioCalled = false;

        await Scenario()
            .BeforeScenario(async (context) =>
            {
                beforeScenarioCalled = true;
                await Task.CompletedTask;
            })
            .BeforeIteration(async (context) =>
            {
                Interlocked.Add(ref beforeIterationCalled, 1);
            })
            .Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .AfterIteration(async (context) =>
            {
                Interlocked.Add(ref afterIterationCalled, 1);
                await Task.CompletedTask;
            })
            .AfterScenario(async (context) =>
            {
                afterScenarioCalled = true;
                await Task.CompletedTask;
            })
            .Run();

        Assert.IsTrue(beforeScenarioCalled);
        Assert.AreEqual(3, beforeIterationCalled);
        Assert.AreEqual(3, afterIterationCalled);
        Assert.IsTrue(afterScenarioCalled);
    }
}
