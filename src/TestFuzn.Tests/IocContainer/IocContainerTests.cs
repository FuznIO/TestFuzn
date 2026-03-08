using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Tests.IocContainer;

[TestClass]
public class IocContainerTests : Test
{
    [Test]
    public async Task Verify_singleton_returns_same_instance_across_steps()
    {
        Guid? step1InstanceId = null;
        Guid? step2InstanceId = null;

        await Scenario()
            .Step("Resolve singleton in step 1", (context) =>
            {
                var marker = context.ServicesProvider.GetRequiredService<SingletonMarker>();
                step1InstanceId = marker.InstanceId;
            })
            .Step("Resolve singleton in step 2", (context) =>
            {
                var marker = context.ServicesProvider.GetRequiredService<SingletonMarker>();
                step2InstanceId = marker.InstanceId;
            })
            .Run();

        Assert.AreEqual(step1InstanceId, step2InstanceId);
    }

    [Test]
    public async Task Verify_singleton_returns_same_instance_across_iterations()
    {
        var instanceIds = new ConcurrentBag<Guid>();

        await Scenario()
            .Step("Resolve singleton", (context) =>
            {
                var marker = context.ServicesProvider.GetRequiredService<SingletonMarker>();
                instanceIds.Add(marker.InstanceId);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Run();

        Assert.AreEqual(1, instanceIds.Distinct().Count());
    }

    [Test]
    public async Task Verify_scoped_returns_same_instance_within_iteration()
    {
        Guid? step1InstanceId = null;
        Guid? step2InstanceId = null;

        await Scenario()
            .Step("Resolve scoped in step 1", (context) =>
            {
                var marker = context.ServicesProvider.GetRequiredService<ScopedMarker>();
                step1InstanceId = marker.InstanceId;
            })
            .Step("Resolve scoped in step 2", (context) =>
            {
                var marker = context.ServicesProvider.GetRequiredService<ScopedMarker>();
                step2InstanceId = marker.InstanceId;
            })
            .Run();

        Assert.AreEqual(step1InstanceId, step2InstanceId);
    }

    [Test]
    public async Task Verify_scoped_returns_different_instance_per_iteration()
    {
        var instanceIds = new ConcurrentBag<Guid>();

        await Scenario()
            .Step("Resolve scoped", (context) =>
            {
                var marker = context.ServicesProvider.GetRequiredService<ScopedMarker>();
                instanceIds.Add(marker.InstanceId);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Run();

        Assert.AreEqual(5, instanceIds.Distinct().Count());
    }

    [Test]
    public async Task Verify_scoped_in_test_scope_differs_from_iteration_scope()
    {
        Guid? testScopeInstanceId = null;
        Guid? iterationScopeInstanceId = null;

        await Scenario()
            .BeforeScenario((context) =>
            {
                var marker = context.ServicesProvider.GetRequiredService<ScopedMarker>();
                testScopeInstanceId = marker.InstanceId;
            })
            .Step("Resolve scoped in step", (context) =>
            {
                var marker = context.ServicesProvider.GetRequiredService<ScopedMarker>();
                iterationScopeInstanceId = marker.InstanceId;
            })
            .Run();

        Assert.IsNotNull(testScopeInstanceId);
        Assert.IsNotNull(iterationScopeInstanceId);
        Assert.AreNotEqual(testScopeInstanceId, iterationScopeInstanceId);
    }

    [Test]
    public async Task Verify_transient_returns_different_instance_per_resolution()
    {
        Guid? firstInstanceId = null;
        Guid? secondInstanceId = null;

        await Scenario()
            .Step("Resolve transient twice", (context) =>
            {
                var first = context.ServicesProvider.GetRequiredService<TransientMarker>();
                var second = context.ServicesProvider.GetRequiredService<TransientMarker>();
                firstInstanceId = first.InstanceId;
                secondInstanceId = second.InstanceId;
            })
            .Run();

        Assert.AreNotEqual(firstInstanceId, secondInstanceId);
    }
}
