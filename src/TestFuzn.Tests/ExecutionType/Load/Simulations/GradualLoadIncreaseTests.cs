using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Simulations;

[TestClass]
[Tags("integration", "load")]
public class GradualLoadIncreaseTests : Test
{
    [Test]
    public async Task Rate_rises_evenly_per_second_over_the_duration()
    {
        var starts = new ConcurrentBag<DateTime>();

        await Scenario()
            .Step("Record", context => { starts.Add(DateTime.UtcNow); })
            .Load().Simulations((context, simulations) => simulations.GradualLoadIncrease(2, 18, TimeSpan.FromSeconds(4)));

        var (firstHalf, secondHalf) = Halves(starts, TimeSpan.FromSeconds(4));
        Assert.HasCount(40, starts);
        Assert.IsGreaterThan(firstHalf + 8, secondHalf);
    }

    [Test]
    public async Task Rate_falls_when_the_end_rate_is_below_the_start_rate()
    {
        var starts = new ConcurrentBag<DateTime>();

        await Scenario()
            .Step("Record", context => { starts.Add(DateTime.UtcNow); })
            .Load().Simulations((context, simulations) => simulations.GradualLoadIncrease(18, 2, TimeSpan.FromSeconds(4)));

        var (firstHalf, secondHalf) = Halves(starts, TimeSpan.FromSeconds(4));
        Assert.HasCount(40, starts);
        Assert.IsGreaterThan(secondHalf + 8, firstHalf);
    }

    private static (int FirstHalf, int SecondHalf) Halves(IEnumerable<DateTime> starts, TimeSpan duration)
    {
        var sorted = starts.OrderBy(start => start).ToList();
        var middle = sorted[0] + duration / 2;
        var firstHalf = sorted.Count(start => start < middle);
        return (firstHalf, sorted.Count - firstHalf);
    }
}
