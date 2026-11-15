using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Simulations;

[TestClass]
[Tags("integration", "load")]
public class RandomLoadPerSecondTests : Test
{
    [Test]
    public async Task Test()
    {
        var stepExecutionCount = 0;

        await Scenario("Verify random load")
            .Step("Test", (context) =>
            {
                Interlocked.Increment(ref stepExecutionCount);
                return Task.CompletedTask;
            })
            .Load().Simulations((context, simulations) => simulations.RandomLoadPerSecond(1, 10, TimeSpan.FromSeconds(3)));

        Assert.IsGreaterThan(2, stepExecutionCount);
        Assert.IsLessThan(31, stepExecutionCount);
    }

    [Test]
    public async Task Starts_are_spread_over_each_second()
    {
        var starts = new ConcurrentBag<DateTime>();

        await Scenario()
            .Step("Record", context => { starts.Add(DateTime.UtcNow); })
            .Load().Simulations((context, simulations) => simulations.RandomLoadPerSecond(5, 5, TimeSpan.FromSeconds(3)));

        var firstSecond = starts.OrderBy(start => start).Take(5).ToList();
        Assert.HasCount(15, starts);
        Assert.IsGreaterThan(0.5, (firstSecond[^1] - firstSecond[0]).TotalSeconds);
    }
}
