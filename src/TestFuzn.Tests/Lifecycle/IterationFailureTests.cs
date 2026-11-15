namespace Fuzn.TestFuzn.Tests.Lifecycle;

/// <summary>
/// Regression tests for a producer/consumer deadlock: actions outside the steps
/// (BeforeIteration, AfterIteration, cleanup actions) are not guarded by ExecuteStepHandler.
/// When such an action threw, the consumer never removed the message from the queues, so the
/// producers kept waiting for the queue to drain and the test run hung forever.
/// The timeouts make a regression fail instead of hanging the suite.
/// </summary>
[TestClass]
[Tags("integration")]
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
                .Step("Step 1", (context) => { });
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
                .AfterIteration((context) => throw new InvalidOperationException("AfterIteration failure"));
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
                .Step("Step 1", (context) => { });
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
                .AfterIteration((context) => throw new InvalidOperationException("AfterIteration failure"));
        });
    }

    [Test]
    [Tags("load")]
    [Timeout(60000)]
    public async Task ShouldFail_AfterIteration_failure_during_load_warmup_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .Step("Step 1", (context) => { })
                .AfterIteration((context) => throw new InvalidOperationException("AfterIteration failure"))
                .Load().Warmup((context, simulations) => simulations.OneTimeLoad(3))
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3));
        });
    }

    [Test]
    [Timeout(60000)]
    public async Task ShouldFail_BeforeIteration_timeout_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
        {
            await Scenario()
                .BeforeIteration((context) => throw new TaskCanceledException("BeforeIteration timeout"))
                .Step("Step 1", (context) => { });
        });
    }

    [Test]
    [Timeout(60000)]
    public async Task ShouldFail_BeforeIteration_timeout_with_input_data_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
        {
            await Scenario()
                .InputData(new IterationUser("user1"), new IterationUser("user2"), new IterationUser("user3"))
                .BeforeIteration((context) => throw new TaskCanceledException("BeforeIteration timeout"))
                .Step("Step 1", (context) => { });
        });
    }

    [Test]
    [Timeout(60000)]
    public async Task ShouldFail_AfterIteration_timeout_with_input_data_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
        {
            await Scenario()
                .InputData(new IterationUser("user1"), new IterationUser("user2"), new IterationUser("user3"))
                .Step("Step 1", (context) => { })
                .AfterIteration((context) => throw new TaskCanceledException("AfterIteration timeout"));
        });
    }

    [Test]
    [Tags("load")]
    [Timeout(60000)]
    public async Task ShouldFail_BeforeIteration_failure_in_load_test_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .BeforeIteration((context) => throw new InvalidOperationException("BeforeIteration failure"))
                .Step("Step 1", (context) => { })
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3));
        });
    }

    [Test]
    [Tags("load")]
    [Timeout(60000)]
    public async Task ShouldFail_BeforeIteration_failure_in_load_test_counts_as_a_failed_iteration()
    {
        var iterations = 0;
        var stepRuns = 0;
        var ok = -1;
        var failed = -1;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .BeforeIteration((context) =>
                {
                    if (Interlocked.Increment(ref iterations) % 2 == 0)
                        throw new InvalidOperationException("BeforeIteration failure");
                })
                .Step("Step 1", (context) => { Interlocked.Increment(ref stepRuns); })
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
                .Load().AssertWhenDone((context, stats) =>
                {
                    ok = stats.Ok.Count;
                    failed = stats.Failed.Count;
                });
        });

        Assert.AreEqual(5, ok);
        Assert.AreEqual(5, failed);
        Assert.AreEqual(5, stepRuns);
    }

    [Test]
    [Tags("load")]
    [Timeout(60000)]
    public async Task ShouldFail_BeforeIteration_timeout_in_load_test_propagates_exception()
    {
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
        {
            await Scenario()
                .BeforeIteration((context) => throw new TaskCanceledException("BeforeIteration timeout"))
                .Step("Step 1", (context) => { })
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3));
        });
    }
}
