namespace Fuzn.TestFuzn.Tests.Assertions;

[TestClass]
public class AssertionTests : TestBase
{
    [Test]
    public async Task Verify_assert_while_running_should_fail()
    {
        var stepExecutionCount = 0;
        var assertWhileRunningExecuted = false;
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
                    assertWhileRunningExecuted = true;
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
            Assert.IsTrue(assertWhileRunningExecuted);
            Assert.IsTrue(catchExecuted);
        }
    }

    [Test]
    public async Task Verify_assert_sub_steps_all_ok()
    {
        await Scenario()
            .Step("Step 1", (context) =>
            {

            })
            .Step("Step 2", (context) =>
            {
                context.Step("Step 2.1", (subContext) =>
                {
                });
                context.Step("Step 2.2", (subContext) =>
                {
                    //Assert.Fail();
                });
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(5, stats.RequestCount);
                Assert.AreEqual(0, stats.Failed.RequestCount);
                Assert.AreEqual(5, stats.Ok.RequestCount);

                Assert.AreEqual(5, stats.GetStep("Step 1").Ok.RequestCount);
                Assert.AreEqual(0, stats.GetStep("Step 1").Failed.RequestCount);

                Assert.AreEqual(0, stats.GetStep("Step 2").Failed.RequestCount);
                Assert.AreEqual(5, stats.GetStep("Step 2").Ok.RequestCount);
                Assert.AreEqual(5, stats.GetStep("Step 2.1").Ok.RequestCount);
                Assert.AreEqual(0, stats.GetStep("Step 2.1").Failed.RequestCount);
                Assert.AreEqual(5, stats.GetStep("Step 2.2").Ok.RequestCount);
                Assert.AreEqual(0, stats.GetStep("Step 2.2").Failed.RequestCount);
            })
            .Run();
    }

[Test]
    public async Task Verify_assert_sub_steps_fails()
    {
        await Scenario()
            .Step("Step 1", (context) =>
            {

            })
            .Step("Step 2", (context) =>
            {
                context.Step("Step 2.1", (subContext) =>
                {
                });
                context.Step("Step 2.2", (subContext) =>
                {
                    Assert.Fail();
                });
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(5, stats.RequestCount);
                Assert.AreEqual(5, stats.Failed.RequestCount);
                Assert.AreEqual(0, stats.Ok.RequestCount);
                Assert.AreEqual(5, stats.GetStep("Step 1").Ok.RequestCount);
                Assert.AreEqual(0, stats.GetStep("Step 1").Failed.RequestCount);
                Assert.AreEqual(5, stats.GetStep("Step 2").Failed.RequestCount);
                Assert.AreEqual(0, stats.GetStep("Step 2").Ok.RequestCount);
                Assert.AreEqual(5, stats.GetStep("Step 2.1").Ok.RequestCount);
                Assert.AreEqual(0, stats.GetStep("Step 2.1").Failed.RequestCount);
                Assert.AreEqual(0, stats.GetStep("Step 2.2").Ok.RequestCount);
                Assert.AreEqual(5, stats.GetStep("Step 2.2").Failed.RequestCount);
            })
            .Run();
    }
}
