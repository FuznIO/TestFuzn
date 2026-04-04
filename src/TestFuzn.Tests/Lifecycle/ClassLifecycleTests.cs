namespace Fuzn.TestFuzn.Tests.Lifecycle;

[TestClass]
public class ClassLifecycleTests : Test, IBeforeClass, IAfterClass, IBeforeTest
{
    internal static int BeforeClassCallCount = 0;
    internal static bool AfterClassCalled = false;
    internal static bool BeforeClassCalledBeforeBeforeTest = false;
    private bool _beforeTestCalled = false;

    public Task BeforeClass(Context context)
    {
        Interlocked.Increment(ref BeforeClassCallCount);
        return Task.CompletedTask;
    }

    public Task AfterClass(Context context)
    {
        AfterClassCalled = true;
        return Task.CompletedTask;
    }

    public Task BeforeTest(Context context)
    {
        _beforeTestCalled = true;

        if (BeforeClassCallCount > 0)
            BeforeClassCalledBeforeBeforeTest = true;

        return Task.CompletedTask;
    }

    [Test]
    public async Task Verify_BeforeClass_is_called()
    {
        await Scenario()
            .Step("Verify BeforeClass was called", (context) =>
            {
                Assert.IsGreaterThan(0, BeforeClassCallCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_BeforeClass_is_called_before_BeforeTest()
    {
        await Scenario()
            .Step("Verify ordering", (context) =>
            {
                Assert.IsTrue(BeforeClassCalledBeforeBeforeTest);
                Assert.IsTrue(_beforeTestCalled);
            })
            .Run();
    }
}
