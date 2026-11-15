using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Tests.Reports;

[TestClass]
[Tags("unit")]
public class DurationDistributionTests : Test
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Verify_duration_distribution_buckets()
    {
        await Scenario()
            .Step("Ok and failed iterations share the same bucket edges and every iteration is counted once", context =>
            {
                var ok = Collector((100, 100), (300, 50));
                var failed = Collector((1000, 10));

                var buckets = DurationDistribution.From(ok.GetState(), failed.GetState());

                Assert.IsLessThanOrEqualTo(DurationDistribution.TargetBuckets + 2, buckets.Count);
                Assert.AreEqual(150L, buckets.Sum(b => b.OkCount));
                Assert.AreEqual(10L, buckets.Sum(b => b.FailedCount));
                for (var i = 1; i < buckets.Count; i++)
                    Assert.AreEqual(buckets[i - 1].ToMs, buckets[i].FromMs);

                Assert.AreEqual(100L, Holding(buckets, 100).OkCount);
                Assert.AreEqual(50L, Holding(buckets, 300).OkCount);
                Assert.AreEqual(10L, Holding(buckets, 1000).FailedCount);
            })
            .Step("The edges are round numbers that cover the fastest and the slowest iteration", context =>
            {
                var buckets = DurationDistribution.From(Collector((12, 1), (873, 1)).GetState(), null);

                var width = buckets[0].ToMs - buckets[0].FromMs;
                Assert.AreEqual(50.0, width, 0.0001);
                Assert.IsLessThanOrEqualTo(12.0, buckets[0].FromMs);
                Assert.IsGreaterThan(873.0, buckets[^1].ToMs);
            })
            .Step("Iterations that all take the same time land in one bucket", context =>
            {
                var buckets = DurationDistribution.From(Collector((40, 25)).GetState(), null);

                Assert.HasCount(1, buckets);
                Assert.AreEqual(25L, buckets[0].OkCount);
            })
            .Step("No iterations give no buckets", context =>
            {
                var buckets = DurationDistribution.From(new StatsCollector().GetState(), new StatsCollector().GetState());

                Assert.HasCount(0, buckets);
            })
            .Step("Bucket widths are 1, 2, 2.5 or 5 times a power of ten", context =>
            {
                Assert.AreEqual(10.0, DurationDistribution.NiceWidth(7.3), 0.0001);
                Assert.AreEqual(0.5, DurationDistribution.NiceWidth(0.3), 0.0001);
                Assert.AreEqual(25.0, DurationDistribution.NiceWidth(21), 0.0001);
                Assert.AreEqual(2000.0, DurationDistribution.NiceWidth(1500), 0.0001);
            });
    }

    private static StatsCollector Collector(params (int Ms, int Count)[] durations)
    {
        var collector = new StatsCollector();
        foreach (var (ms, count) in durations)
        {
            for (var i = 0; i < count; i++)
                collector.Record(TimeSpan.FromMilliseconds(ms), Start, Start.AddSeconds(10));
        }

        return collector;
    }

    private static DurationBucket Holding(IReadOnlyList<DurationBucket> buckets, double ms)
    {
        return buckets.Single(b => b.FromMs <= ms && ms < b.ToMs);
    }
}
