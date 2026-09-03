namespace Fuzn.TestFuzn.Tests.Lifecycle;

/// <summary>
/// Regression tests for a producer/consumer deadlock: actions outside the steps
/// (BeforeIteration, AfterIteration, cleanup actions) are not guarded by ExecuteStepHandler.
/// When such an action threw, the consumer never removed the message from the queues, so the
/// producers kept waiting for the queue to drain and the test run hung forever.
/// The timeouts make a regression fail instead of hanging the suite.
/// </summary>
[TestClass]
public class IterationFailureTests : Test
{
    private record IterationUser(string Name);

    [Test]
    [Timeout(60000)]
    public async Task ShouldFail_BeforeIteration_failure_with_input_data_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .InputData(new IterationUser("user1"), new IterationUser("user2"), new IterationUser("user3"))
                .BeforeIteration((context) => throw new InvalidOperationException("BeforeIteration failure"))
                .Step("Step 1", (context) => { })
                .Run();
        });
    }

    [Test]
    [Timeout(60000)]
    public async Task ShouldFail_AfterIteration_failure_with_input_data_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .InputData(new IterationUser("user1"), new IterationUser("user2"), new IterationUser("user3"))
                .Step("Step 1", (context) => { })
                .AfterIteration((context) => throw new InvalidOperationException("AfterIteration failure"))
                .Run();
        });
    }

    [Test]
    [Timeout(60000)]
    public async Task ShouldFail_BeforeIteration_failure_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .BeforeIteration((context) => throw new InvalidOperationException("BeforeIteration failure"))
                .Step("Step 1", (context) => { })
                .Run();
        });
    }

    [Test]
    [Timeout(60000)]
    public async Task ShouldFail_AfterIteration_failure_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .Step("Step 1", (context) => { })
                .AfterIteration((context) => throw new InvalidOperationException("AfterIteration failure"))
                .Run();
        });
    }

    [Test]
    [Timeout(60000)]
    public async Task ShouldFail_AfterIteration_failure_during_load_warmup_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .Step("Step 1", (context) => { })
                .AfterIteration((context) => throw new InvalidOperationException("AfterIteration failure"))
                .Load().Warmup((context, simulations) => simulations.OneTimeLoad(3))
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
                .Run();
        });
    }
}
