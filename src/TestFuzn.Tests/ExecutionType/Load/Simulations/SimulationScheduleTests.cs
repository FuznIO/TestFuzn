using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Simulations;

[TestClass]
[Tags("unit")]
public class SimulationScheduleTests : Test
{
    [Test]
    public async Task Verify_start_schedules()
    {
        await Scenario()
            .Step("A fixed load spreads its rate evenly over each interval", context =>
            {
                var starts = Within(FixedLoadHandler.StartTimes(10, TimeSpan.FromSeconds(1)), TimeSpan.FromSeconds(2));

                Assert.HasCount(20, starts);
                Assert.AreEqual(TimeSpan.Zero, starts[0]);
                Assert.AreEqual(TimeSpan.FromMilliseconds(100), starts[1]);
                Assert.AreEqual(TimeSpan.FromMilliseconds(1900), starts[^1]);
            })
            .Step("A fixed load stops at its duration, also partway through an interval", context =>
            {
                Assert.HasCount(150, Within(FixedLoadHandler.StartTimes(100, TimeSpan.FromSeconds(2)), TimeSpan.FromSeconds(3)));
            })
            .Step("A fixed load with rate 0 starts nothing", context =>
            {
                Assert.IsEmpty(Within(FixedLoadHandler.StartTimes(0, TimeSpan.FromSeconds(1)), TimeSpan.FromSeconds(5)));
            })
            .Step("A gradual load starts the average of its rates times the duration", context =>
            {
                var starts = Within(GradualLoadIncreaseHandler.StartTimes(5, 50, TimeSpan.FromSeconds(5)), TimeSpan.FromSeconds(5));

                Assert.HasCount(138, starts);
                AssertRising(starts);
            })
            .Step("A gradual load starts slowly and ends fast", context =>
            {
                var starts = Within(GradualLoadIncreaseHandler.StartTimes(5, 50, TimeSpan.FromSeconds(5)), TimeSpan.FromSeconds(5));

                Assert.HasCount(10, starts.Where(start => start < TimeSpan.FromSeconds(1)).ToList());
                Assert.IsGreaterThan(44, starts.Count(start => start >= TimeSpan.FromSeconds(4)));
            })
            .Step("A gradual load ramps down when the end rate is lower", context =>
            {
                var starts = Within(GradualLoadIncreaseHandler.StartTimes(50, 5, TimeSpan.FromSeconds(5)), TimeSpan.FromSeconds(5));

                Assert.HasCount(138, starts);
                Assert.IsGreaterThan(44, starts.Count(start => start < TimeSpan.FromSeconds(1)));
                AssertRising(starts);
            })
            .Step("A gradual load whose total is a whole number starts exactly that many", context =>
            {
                Assert.HasCount(45, Within(GradualLoadIncreaseHandler.StartTimes(8, 1, TimeSpan.FromSeconds(10)), TimeSpan.FromSeconds(10)));
                Assert.HasCount(465, Within(GradualLoadIncreaseHandler.StartTimes(0, 31, TimeSpan.FromSeconds(30)), TimeSpan.FromSeconds(30)));
                Assert.HasCount(203, Within(GradualLoadIncreaseHandler.StartTimes(0, 58, TimeSpan.FromSeconds(7)), TimeSpan.FromSeconds(7)));
            })
            .Step("A gradual load with equal rates is a fixed load", context =>
            {
                Assert.HasCount(30, Within(GradualLoadIncreaseHandler.StartTimes(10, 10, TimeSpan.FromSeconds(3)), TimeSpan.FromSeconds(3)));
            })
            .Step("A gradual load from 0 to 0 starts nothing", context =>
            {
                Assert.IsEmpty(Within(GradualLoadIncreaseHandler.StartTimes(0, 0, TimeSpan.FromSeconds(3)), TimeSpan.FromSeconds(3)));
            })
            .Step("A random load starts the drawn number each second, spread over the second", context =>
            {
                var configuration = new RandomLoadPerSecondConfiguration(4, 4, TimeSpan.FromSeconds(2));
                var starts = Within(RandomLoadPerSecondHandler.StartTimes(configuration, new Random(1)), TimeSpan.FromSeconds(2));

                Assert.HasCount(8, starts);
                Assert.AreEqual(TimeSpan.FromMilliseconds(250), starts[1]);
                Assert.AreEqual(TimeSpan.FromMilliseconds(1750), starts[^1]);
            })
            .Step("A random load stays between its minimum and maximum each second", context =>
            {
                var configuration = new RandomLoadPerSecondConfiguration(3, 7, TimeSpan.FromSeconds(20));
                var starts = Within(RandomLoadPerSecondHandler.StartTimes(configuration, new Random(7)), TimeSpan.FromSeconds(20));

                foreach (var second in starts.GroupBy(start => (int) start.TotalSeconds))
                {
                    Assert.IsGreaterThan(2, second.Count());
                    Assert.IsLessThan(8, second.Count());
                }
            })
            .Step("A random load with maximum 0 starts nothing", context =>
            {
                var configuration = new RandomLoadPerSecondConfiguration(0, 0, TimeSpan.FromSeconds(2));

                Assert.IsEmpty(Within(RandomLoadPerSecondHandler.StartTimes(configuration, new Random(1)), TimeSpan.FromSeconds(2)));
            });
    }

    [Test]
    public async Task Invalid_values_are_rejected_when_the_simulation_is_built()
    {
        var second = TimeSpan.FromSeconds(1);

        await Scenario()
            .Step("Counts and rates below 0", context =>
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OneTimeLoadConfiguration(-1));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FixedLoadConfiguration(-1, second, second));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GradualLoadIncreaseConfiguration(-1, 5, second));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GradualLoadIncreaseConfiguration(5, -1, second));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FixedConcurrentLoadConfiguration(-1, second));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RandomLoadPerSecondConfiguration(-1, 5, second));
            })
            .Step("Durations and intervals of 0 or less", context =>
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FixedLoadConfiguration(5, TimeSpan.Zero, second));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FixedLoadConfiguration(5, second, TimeSpan.Zero));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GradualLoadIncreaseConfiguration(1, 5, TimeSpan.Zero));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FixedConcurrentLoadConfiguration(5, TimeSpan.Zero));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RandomLoadPerSecondConfiguration(1, 5, TimeSpan.Zero));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PauseLoadConfiguration(TimeSpan.FromSeconds(-1)));
            })
            .Step("A minimum above the maximum", context =>
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RandomLoadPerSecondConfiguration(6, 5, second));
            })
            .Step("A pause of 0 and a rate of 0 are allowed", context =>
            {
                Assert.AreEqual(TimeSpan.Zero, new PauseLoadConfiguration(TimeSpan.Zero).Duration);
                Assert.AreEqual(0, new FixedLoadConfiguration(0, second, second).Rate);
            });
    }

    private static List<TimeSpan> Within(IEnumerable<TimeSpan> starts, TimeSpan duration) =>
        starts.TakeWhile(start => start < duration).ToList();

    private static void AssertRising(List<TimeSpan> starts)
    {
        for (var index = 1; index < starts.Count; index++)
            Assert.IsGreaterThan(starts[index - 1], starts[index]);
    }
}
