namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Simulations;

[TestClass]
[Tags("integration", "load")]
public class FixedConcurrentLoadTests : Test
{
    [Test]
    public async Task Keeps_the_count_running_at_the_same_time()
    {
        var running = 0;
        var mostRunning = 0;
        var count = 0;

        await Scenario()
            .Step("Work", async context =>
            {
                Interlocked.Increment(ref count);
                Track(Interlocked.Increment(ref running), ref mostRunning);
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                Interlocked.Decrement(ref running);
            })
            .Load().Simulations((context, simulations) => simulations.FixedConcurrentLoad(10, TimeSpan.FromSeconds(2)));

        Assert.AreEqual(10, mostRunning);
        Assert.IsGreaterThan(79, count);
        Assert.IsLessThan(121, count);
    }

    [Test]
    public async Task Starts_the_next_iteration_as_soon_as_one_finishes()
    {
        var running = 0;
        var mostRunning = 0;
        var count = 0;

        await Scenario()
            .Step("Work", async context =>
            {
                Interlocked.Increment(ref count);
                Track(Interlocked.Increment(ref running), ref mostRunning);
                await Task.Yield();
                Interlocked.Decrement(ref running);
            })
            .Load().Simulations((context, simulations) => simulations.FixedConcurrentLoad(5, TimeSpan.FromSeconds(1)));

        Assert.IsLessThan(6, mostRunning);
        Assert.IsGreaterThan(1000, count);
    }

    private static void Track(int now, ref int most)
    {
        var seen = Volatile.Read(ref most);
        while (now > seen)
        {
            var previous = Interlocked.CompareExchange(ref most, now, seen);
            if (previous == seen)
                return;
            seen = previous;
        }
    }
}
