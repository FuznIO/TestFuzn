using System.Collections.Concurrent;
using Fuzn.TestFuzn.Tests.Cancellation;

namespace Fuzn.TestFuzn.Tests.ContextPlugins;

[TestClass]
[Tags("integration")]
public class ContextPluginTests : Test
{
    [Test]
    [Tags("load")]
    public async Task InitIteration_is_called_per_iteration()
    {
        var capturedStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();

        await Scenario()
            .Step("Capture state", (context) =>
            {
                var state = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
                capturedStates.Add(state);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3));

        Assert.HasCount(3, capturedStates);
    }

    [Test]
    [Tags("load")]
    public async Task Each_iteration_gets_its_own_state()
    {
        var capturedStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();

        await Scenario()
            .Step("Capture state", (context) =>
            {
                var state = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
                capturedStates.Add(state);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5));

        var distinctCount = capturedStates.Distinct().Count();
        Assert.AreEqual(5, distinctCount, "Each iteration should receive a unique state instance");
    }

    [Test]
    [Tags("load")]
    public async Task CleanupIteration_is_called_after_each_iteration()
    {
        var capturedStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();

        await Scenario()
            .Step("Capture state", (context) =>
            {
                var state = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
                capturedStates.Add(state);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5));

        Assert.IsTrue(capturedStates.All(s => s.CleanedUp),
            "CleanupIteration should be called for every iteration");
    }

    [Test]
    public async Task ShouldFail_CleanupIteration_runs_when_step_throws()
    {
        var capturedStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();

        try
        {
            await Scenario()
                .Step("Capture and throw", (context) =>
                {
                    var state = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
                    capturedStates.Add(state);
                    throw new InvalidOperationException("Step failure");
                });
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        Assert.HasCount(1, capturedStates);
        Assert.IsTrue(capturedStates.First().CleanedUp);
    }

    [Test]
    public async Task State_is_accessible_across_multiple_steps()
    {
        TrackingContextPlugin.TrackingState? stateFromStep1 = null;
        TrackingContextPlugin.TrackingState? stateFromStep2 = null;

        await Scenario()
            .Step("Step 1", (context) =>
            {
                stateFromStep1 = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
            })
            .Step("Step 2", (context) =>
            {
                stateFromStep2 = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
            });

        Assert.IsNotNull(stateFromStep1);
        Assert.AreSame(stateFromStep1, stateFromStep2,
            "The same state instance should be shared across steps within an iteration");
    }

    [Test]
    public async Task CleanupIteration_runs_for_standard_test()
    {
        var capturedStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();

        await Scenario()
            .Step("Capture state", (context) =>
            {
                var state = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
                capturedStates.Add(state);
            });

        Assert.HasCount(1, capturedStates);
        Assert.IsTrue(capturedStates.First().CleanedUp);
    }

    [Test]
    [Tags("load")]
    public async Task CleanupIteration_runs_for_warmup_iterations()
    {
        var warmupStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();
        var measurementStates = new ConcurrentBag<TrackingContextPlugin.TrackingState>();

        await Scenario()
            .Step("Capture state", (context) =>
            {
                var state = context.Internals.Plugins.GetState<TrackingContextPlugin.TrackingState>(typeof(TrackingContextPlugin));
                measurementStates.Add(state);
            })
            .Load().Warmup((context, simulations) => simulations.OneTimeLoad(2))
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3));

        // All states (warmup + measurement) should be cleaned up
        var allStates = measurementStates.ToList();
        Assert.IsTrue(allStates.All(s => s.CleanedUp),
            "CleanupIteration should run for all iterations including warmup");
    }
}
