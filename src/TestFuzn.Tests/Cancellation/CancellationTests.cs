using System.Collections.Concurrent;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Tests.Cancellation;

[TestClass]
public class CancellationTests : Test
{
    [Test]
    public async Task CancellationToken_is_available_on_context()
    {
        await Scenario()
            .Step("Verify token exists", (context) =>
            {
                Assert.AreNotEqual(CancellationToken.None, context.Info.CancellationToken);
                Assert.IsFalse(context.Info.CancellationToken.IsCancellationRequested);
            })
            .Run();
    }

    [Test]
    public async Task ShouldFail_CancellationToken_is_not_cancelled_on_controlled_stop()
    {
        CancellationToken capturedToken = default;
        ExecutionStatus? capturedStatus = null;

        try
        {
            await Scenario()
                .Step("Capture token and fail", (context) =>
                {
                    capturedToken = context.Info.CancellationToken;
                    Assert.Fail("Force stop");
                })
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(1))
                .Load().AssertWhileRunning((context, stats) =>
                {
                    Assert.AreEqual(0, stats.Failed.RequestCount);
                })
                .AssertInternalState(state =>
                {
                    capturedStatus = state.TestExecutionState.ExecutionStatus;
                })
                .Run();
        }
        catch (AssertFailedException)
        {
            // Expected
        }

        Assert.AreEqual(ExecutionStatus.Stopped, capturedStatus,
            "ExecutionStatus should be Stopped to confirm the controlled stop path was taken");
        Assert.IsFalse(capturedToken.IsCancellationRequested,
            "Controlled stop should not cancel the token — only framework cancellation should");
    }

    [Test]
    public async Task ShouldFail_Load_test_stops_producing_when_stopped()
    {
        var executionCount = 0;
        ExecutionStatus? capturedStatus = null;

        try
        {
            await Scenario()
                .Step("Increment", (context) =>
                {
                    Interlocked.Increment(ref executionCount);
                    Assert.Fail("Force failure");
                })
                .Load().Simulations((context, simulations) => simulations.FixedLoad(100, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)))
                .Load().AssertWhileRunning((context, stats) =>
                {
                    Assert.AreEqual(0, stats.Failed.RequestCount);
                })
                .AssertInternalState(state =>
                {
                    capturedStatus = state.TestExecutionState.ExecutionStatus;
                })
                .Run();
        }
        catch (AssertFailedException)
        {
            // Expected
        }

        Assert.AreEqual(ExecutionStatus.Stopped, capturedStatus);
        Assert.IsLessThan(1000, executionCount);
    }

    [Test]
    public async Task ShouldFail_Cleanup_runs_after_controlled_stop()
    {
        var afterScenarioExecuted = false;
        ExecutionStatus? capturedStatus = null;

        try
        {
            await Scenario()
                .AfterScenario(context =>
                {
                    afterScenarioExecuted = true;
                    return Task.CompletedTask;
                })
                .Step("Fail", (context) =>
                {
                    Assert.Fail("Force failure");
                })
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(1))
                .Load().AssertWhileRunning((context, stats) =>
                {
                    Assert.AreEqual(0, stats.Failed.RequestCount);
                })
                .AssertInternalState(state =>
                {
                    capturedStatus = state.TestExecutionState.ExecutionStatus;
                })
                .Run();
        }
        catch (AssertFailedException)
        {
            // Expected
        }

        Assert.AreEqual(ExecutionStatus.Stopped, capturedStatus);
        Assert.IsTrue(afterScenarioExecuted, "AfterScenario should run even after controlled stop");
    }

    [Test]
    public async Task ShouldFail_Cleanup_context_has_uncancelled_token()
    {
        CancellationToken cleanupToken = default;
        ExecutionStatus? capturedStatus = null;

        try
        {
            await Scenario()
                .AfterScenario(context =>
                {
                    cleanupToken = context.Info.CancellationToken;
                    return Task.CompletedTask;
                })
                .Step("Fail", (context) =>
                {
                    Assert.Fail("Force failure");
                })
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(1))
                .Load().AssertWhileRunning((context, stats) =>
                {
                    Assert.AreEqual(0, stats.Failed.RequestCount);
                })
                .AssertInternalState(state =>
                {
                    capturedStatus = state.TestExecutionState.ExecutionStatus;
                })
                .Run();
        }
        catch (AssertFailedException)
        {
            // Expected
        }

        Assert.AreEqual(ExecutionStatus.Stopped, capturedStatus);
        Assert.IsFalse(cleanupToken.IsCancellationRequested,
            "Cleanup context should have an uncancelled token");
    }

    [Test]
    public async Task Context_plugin_cleanup_runs_for_every_iteration()
    {
        var capturedStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();

        await Scenario()
            .Step("Capture state", (context) =>
            {
                var state = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
                capturedStates.Add(state);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(5, stats.Ok.RequestCount);
            })
            .Run();

        Assert.HasCount(5, capturedStates);
        Assert.IsTrue(capturedStates.All(s => s.CleanedUp),
            "All context plugin states should be cleaned up");
    }

    [Test]
    public async Task ShouldFail_Context_plugin_cleanup_runs_on_controlled_stop()
    {
        var capturedStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();

        try
        {
            await Scenario()
                .Step("Capture and fail", (context) =>
                {
                    var state = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
                    capturedStates.Add(state);
                    Assert.Fail("Force failure");
                })
                .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
                .Load().AssertWhileRunning((context, stats) =>
                {
                    Assert.AreEqual(0, stats.Failed.RequestCount);
                })
                .Run();
        }
        catch (AssertFailedException)
        {
            // Expected
        }

        Assert.IsGreaterThan(0, capturedStates.Count);
        Assert.IsTrue(capturedStates.All(s => s.CleanedUp),
            "All context plugin states should be cleaned up even after controlled stop");
    }

    [Test]
    public async Task ShouldFail_Context_plugin_cleanup_runs_on_step_exception()
    {
        var capturedStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();

        try
        {
            await Scenario()
                .Step("Capture and throw", (context) =>
                {
                    var state = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
                    capturedStates.Add(state);
                    throw new InvalidOperationException("Unexpected error");
                })
                .Run();
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        Assert.HasCount(1, capturedStates);
        Assert.IsTrue(capturedStates.All(s => s.CleanedUp),
            "Context plugin should be cleaned up even when a step throws");
    }
}
