namespace Fuzn.TestFuzn.Tests.ExecutionType.Feature.Execution;

[TestClass]
public class ExecutionTests : TestBase
{
    public override GroupInfo Group => new() { Name = "Feature-Execution" };

    [Test]
    public async Task Verify_Run_Once()
    {
        var executionCount = 0;

        await Scenario("Verify that a feature scenario without simulations is run only once")
            .Step("Step 1", (context) => 
            {
                Interlocked.Increment(ref executionCount);
            })
            .Run();

        Assert.AreEqual(1, executionCount);
    }
}
