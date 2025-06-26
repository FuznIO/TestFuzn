namespace TestFusion.Tests.Assertions;

[FeatureTest]
public class AssertionTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task Verify_assertions_while_running_should_fail()
    {
        var stepExecutionCount = 0;
        var assertExecuted = false;
        var catchExecuted = false;

        try
        {
            await Scenario()
                .Step("Test", (context) =>
                {
                    Interlocked.Increment(ref stepExecutionCount);
                    Assert.Fail();
                })
                .Load().Simulations((context, simulations) => simulations.FixedLoad(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)))
                .Load().AssertWhileRunning((context, stats) =>
                {
                    assertExecuted = true;
                    Assert.AreEqual(0, stats.Failed.RequestCount);
                })
                .Run();
        }
        catch (AssertFailedException)
        {
            catchExecuted = true;
        }
        finally
        {
            // Expected failure due to assertion in AssertWhileRunning
            Assert.IsTrue(assertExecuted);
            Assert.IsTrue(catchExecuted);
        }
    }
}
