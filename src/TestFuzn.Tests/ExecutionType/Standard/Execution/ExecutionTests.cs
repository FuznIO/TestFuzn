namespace Fuzn.TestFuzn.Tests.ExecutionType.Standard.Execution;

[TestClass]
public class ExecutionTests : Test
{
    [Test]
    public async Task Verify_Run_Once()
    {
        var executionCount = 0;

        await Scenario("Verify that a standard test scenario is run only once")
            .Step("Step 1", (context) => 
            {
                Interlocked.Increment(ref executionCount);
            })
            .Run();

        Assert.AreEqual(1, executionCount);
    }
}
