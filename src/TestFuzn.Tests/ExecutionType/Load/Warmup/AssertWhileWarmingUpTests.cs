namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Warmup;

[TestClass]
public class AssertWhileWarmingUpTests : Test
{
    [Test]
    public async Task ShouldFail_Verify_stops_when_assertion_throws()
    {
        var warmupExecutionCount = 0;
        var measurementExecutionCount = 0;
        var catchExecuted = false;

        try
        {
            await Scenario()
                .Step("Step 1", (context) =>
                {
                    Assert.Fail("Simulated failure");
                })
                .Load().Warmup((context, simulations) =>
                {
                    simulations.OneTimeLoad(20);
                })
                .Load().AssertWhileWarmingUp((context, warmup) =>
                {
                    Interlocked.Increment(ref warmupExecutionCount);

                    if (warmup.TotalCount >= 5 && warmup.FailedRate > 0.5)
                        throw new Exception("Too many warmup failures");
                })
                .Load().Simulations((context, simulations) =>
                {
                    simulations.OneTimeLoad(10);
                })
                .Load().AssertWhenDone((context, stats) =>
                {
                    Interlocked.Increment(ref measurementExecutionCount);
                })
                .Run();
        }
        catch
        {
            catchExecuted = true;
        }

        Assert.IsTrue(catchExecuted, "Test should have failed due to warmup assertion");
        Assert.IsGreaterThanOrEqualTo(5, warmupExecutionCount, "Assertion should have been evaluated at least 5 times");
        Assert.AreEqual(0, measurementExecutionCount, "Measurement phase should not have been reached");
    }

    [Test]
    public async Task Verify_does_not_stop_when_assertion_passes()
    {
        var executionCount = 0;

        await Scenario()
            .Step("Step 1", (context) =>
            {
                Interlocked.Increment(ref executionCount);
            })
            .Load().Warmup((context, simulations) =>
            {
                simulations.OneTimeLoad(10);
            })
            .Load().AssertWhileWarmingUp((context, warmup) =>
            {
                if (warmup.TotalCount >= 5 && warmup.FailedRate > 0.5)
                    throw new Exception("Too many warmup failures");
            })
            .Load().Simulations((context, simulations) =>
            {
                simulations.OneTimeLoad(20);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(30, executionCount);
                Assert.AreEqual(20, stats.RequestCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_warmup_stats_properties()
    {
        var capturedStats = new List<WarmupStats>();

        await Scenario()
            .Step("Step 1", (context) =>
            {
                // All iterations pass
            })
            .Load().Warmup((context, simulations) =>
            {
                simulations.OneTimeLoad(5);
            })
            .Load().AssertWhileWarmingUp((context, warmup) =>
            {
                lock (capturedStats)
                {
                    capturedStats.Add(warmup);
                }
            })
            .Load().Simulations((context, simulations) =>
            {
                simulations.OneTimeLoad(1);
            })
            .Run();

        Assert.IsNotEmpty(capturedStats);

        var lastStats = capturedStats.Last();
        Assert.AreEqual(5, lastStats.TotalCount);
        Assert.AreEqual(5, lastStats.OkCount);
        Assert.AreEqual(0, lastStats.FailedCount);
        Assert.AreEqual(1.0, lastStats.OkRate);
        Assert.AreEqual(0.0, lastStats.FailedRate);
        Assert.IsGreaterThan(TimeSpan.Zero, lastStats.Duration);
    }

    [Test]
    public async Task Verify_without_assert_while_warming_up_works_as_before()
    {
        var executionCount = 0;

        await Scenario()
            .Step("Step 1", (context) =>
            {
                Interlocked.Increment(ref executionCount);
            })
            .Load().Warmup((context, simulations) =>
            {
                simulations.OneTimeLoad(10);
            })
            .Load().Simulations((context, simulations) =>
            {
                simulations.OneTimeLoad(20);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(30, executionCount);
                Assert.AreEqual(20, stats.RequestCount);
            })
            .Run();
    }
}
