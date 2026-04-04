namespace Fuzn.TestFuzn.Tests.Lifecycle;

[TestClass]
public class ClassLifecycleBeforeOnlyTests : Test, IBeforeClass
{
    internal static bool BeforeClassCalled = false;

    public Task BeforeClass(Context context)
    {
        BeforeClassCalled = true;
        return Task.CompletedTask;
    }

    [Test]
    public async Task Verify_BeforeClass_only_works_without_AfterClass()
    {
        await Scenario()
            .Step("Verify BeforeClass was called", (context) =>
            {
                Assert.IsTrue(BeforeClassCalled);
            })
            .Run();
    }
}
