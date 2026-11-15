using System.Diagnostics;

namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Simulations;

[TestClass]
[Tags("integration", "load")]
public class FixedLoadTests : Test
{
    [Test]
    public async Task Test()
    {
        var stepExecutionCounter = 0;

        await Scenario("Fixed Load Test")
            .Step("Test", context =>
            {
                Interlocked.Increment(ref stepExecutionCounter);
                return Task.CompletedTask;
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(1000, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6)));

        Assert.AreEqual(3000, stepExecutionCounter);
    }

    [Test]
    public async Task Rate_is_per_second_when_no_interval_is_given()
    {
        var count = 0;

        await Scenario()
            .Step("Count", context => { Interlocked.Increment(ref count); })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(10, TimeSpan.FromSeconds(2)));

        Assert.AreEqual(20, count);
    }

    [Test]
    public async Task Load_stops_at_the_duration_partway_through_an_interval()
    {
        var count = 0;
        var stopwatch = Stopwatch.StartNew();

        await Scenario()
            .Step("Count", context => { Interlocked.Increment(ref count); })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(10, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)));

        Assert.AreEqual(15, count);
        Assert.IsGreaterThan(2.9, stopwatch.Elapsed.TotalSeconds);
    }
}
