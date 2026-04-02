namespace Fuzn.TestFuzn.Tests.Steps;

[TestClass]
public class CleanupTests : Test
{
    [Test]
    public async Task Cleanup_action_executes_after_steps_complete()
    {
        var cleanupExecuted = false;
        var stepCompleted = false;

        await Scenario()
            .Step("Step 1", (context) =>
            {
                context.Cleanup(() =>
                {
                    Assert.IsTrue(stepCompleted);
                    cleanupExecuted = true;
                });
                stepCompleted = true;
            })
            .Run();

        Assert.IsTrue(cleanupExecuted);
    }

    [Test]
    public async Task Cleanup_actions_execute_in_reverse_order()
    {
        var order = new List<int>();

        await Scenario()
            .Step("Step 1", (context) =>
            {
                context.Cleanup(() => order.Add(1));
                context.Cleanup(() => order.Add(2));
            })
            .Step("Step 2", (context) =>
            {
                context.Cleanup(() => order.Add(3));
            })
            .Run();

        Assert.HasCount(3, order);
        Assert.AreEqual(3, order[0]);
        Assert.AreEqual(2, order[1]);
        Assert.AreEqual(1, order[2]);
    }

    [Test]
    public async Task ShouldFail_Cleanup_action_failure_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .Step("Step 1", (context) =>
                {
                    context.Cleanup(() => throw new InvalidOperationException("Cleanup failure"));
                })
                .Run();
        });
    }

    [Test]
    public async Task Cleanup_works_from_sub_steps()
    {
        var cleanupExecuted = false;

        await Scenario()
            .Step("Step 1", (context) =>
            {
                context.Step("Sub step 1.1", (subContext) =>
                {
                    subContext.Cleanup(() => cleanupExecuted = true);
                });
            })
            .Run();

        Assert.IsTrue(cleanupExecuted);
    }

    [Test]
    public async Task Cleanup_async_action_executes()
    {
        var cleanupExecuted = false;

        await Scenario()
            .Step("Step 1", (context) =>
            {
                context.Cleanup(async () =>
                {
                    await Task.Delay(1);
                    cleanupExecuted = true;
                });
            })
            .Run();

        Assert.IsTrue(cleanupExecuted);
    }

    [Test]
    public async Task ShouldFail_Cleanup_executes_even_when_step_fails()
    {
        var cleanupExecuted = false;

        try
        {
            await Scenario()
                .Step("Step 1", (context) =>
                {
                    context.Cleanup(() => cleanupExecuted = true);
                })
                .Step("Step 2 - Fails", (context) =>
                {
                    throw new InvalidOperationException("Step failure");
                })
                .Run();
        }
        catch
        {
            // Expected
        }

        Assert.IsTrue(cleanupExecuted);
    }

    [Test]
    public async Task Cleanup_no_actions_registered()
    {
        var stepExecuted = false;

        await Scenario()
            .Step("Step 1", (context) =>
            {
                stepExecuted = true;
            })
            .Run();

        Assert.IsTrue(stepExecuted);
    }

    [Test]
    public async Task Cleanup_executes_before_AfterIteration()
    {
        var order = new List<string>();

        await Scenario()
            .Step("Step 1", (context) =>
            {
                context.Cleanup(() => order.Add("cleanup"));
            })
            .AfterIteration((context) =>
            {
                order.Add("afterIteration");
            })
            .Run();

        Assert.HasCount(2, order);
        Assert.AreEqual("cleanup", order[0]);
        Assert.AreEqual("afterIteration", order[1]);
    }
}
