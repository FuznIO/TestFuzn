using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Tests.Reports;

[TestClass]
[Tags("unit")]
public class SnapshotTimelineTests : Test
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly long FastTicks = TimeSpan.FromMilliseconds(100).Ticks;
    private static readonly long SlowTicks = TimeSpan.FromMilliseconds(2000).Ticks;

    [Test]
    public async Task Verify_snapshots_stay_evenly_spread_over_a_long_run()
    {
        await Scenario()
            .Step("An 8-hour run with a snapshot every 3 s keeps at most 100, evenly spread, with the first and the last", context =>
            {
                var snapshots = new EvenlySpreadSnapshots(100);
                var count = (int) (TimeSpan.FromHours(8).TotalSeconds / 3);
                for (var index = 0; index <= count; index++)
                    snapshots.AddSnapshot(Snapshot(TimeSpan.FromSeconds(index * 3), new Histogram()));

                var kept = snapshots.GetSnapshots();
                Assert.IsLessThanOrEqualTo(100, kept.Count);
                Assert.IsGreaterThanOrEqualTo(50, kept.Count);
                Assert.AreEqual(Start, kept[0].Time);
                Assert.AreEqual(Start + TimeSpan.FromSeconds(count * 3), kept[^1].Time);

                // All gaps but the last one are the same.
                var gaps = kept.Zip(kept.Skip(1), (a, b) => b.Time - a.Time).ToList();
                Assert.AreEqual(1, gaps.Take(gaps.Count - 1).Distinct().Count());
                Assert.IsLessThanOrEqualTo(gaps[0], gaps[^1]);
            })
            .Step("A short run keeps every snapshot", context =>
            {
                var snapshots = new EvenlySpreadSnapshots(100);
                for (var index = 0; index < 4; index++)
                    snapshots.AddSnapshot(Snapshot(TimeSpan.FromSeconds(index * 3), new Histogram()));

                Assert.HasCount(4, snapshots.GetSnapshots());
            })
            .Step("A timeline point covers at least a second; shorter ones count in the next point", context =>
            {
                // The first snapshot comes with the first request; the final one 0.2 s after a regular one.
                var histogram = new Histogram();
                var snapshots = new List<LoadSnapshot>();
                histogram.Record(FastTicks);
                snapshots.Add(Snapshot(TimeSpan.FromMilliseconds(5), histogram, FastTicks));
                histogram.Record(FastTicks, 29);
                snapshots.Add(Snapshot(TimeSpan.FromSeconds(3), histogram, 30 * FastTicks));
                histogram.Record(FastTicks, 2);
                snapshots.Add(Snapshot(TimeSpan.FromSeconds(3.2), histogram, 32 * FastTicks));

                var timeline = EvenlySpreadSnapshots.Timeline(snapshots);
                Assert.HasCount(1, timeline);
                Assert.AreEqual(Start + TimeSpan.FromSeconds(3.2), timeline[0].Time);
                Assert.AreEqual(10.0, timeline[0].OkPerSecond);
            });
    }

    [Test]
    public async Task Verify_timeline_shows_a_late_spike()
    {
        await Scenario()
            .Step("A 10-minute spike at hour 4 of an 8-hour run shows as a 2000 ms p99", context =>
            {
                // 10 fast iterations a second, all run long; from 4:00 to 4:10 one in ten takes 2 s.
                var snapshots = new EvenlySpreadSnapshots(100);
                var histogram = new Histogram();
                var totalTicks = 0L;
                var spikeStart = TimeSpan.FromHours(4);
                var spikeEnd = spikeStart + TimeSpan.FromMinutes(10);
                var count = (int) (TimeSpan.FromHours(8).TotalSeconds / 3);
                for (var index = 0; index <= count; index++)
                {
                    var at = TimeSpan.FromSeconds(index * 3);
                    if (index > 0)
                    {
                        var inSpike = at > spikeStart && at <= spikeEnd;
                        var fast = inSpike ? 27 : 30;
                        var slow = inSpike ? 3 : 0;
                        histogram.Record(FastTicks, fast);
                        histogram.Record(SlowTicks, slow);
                        totalTicks += fast * FastTicks + slow * SlowTicks;
                    }

                    snapshots.AddSnapshot(Snapshot(at, histogram, totalTicks));
                }

                var timeline = EvenlySpreadSnapshots.Timeline(snapshots.GetSnapshots());
                var spike = timeline.Where(point => point.Time > Start + spikeStart && point.Time <= Start + spikeEnd + TimeSpan.FromMinutes(5)).ToList();
                Assert.IsNotEmpty(spike);
                Assert.IsGreaterThan(1900.0, spike.Max(point => point.P99Ms!.Value));

                // Outside the spike every point is fast, and the rate is 10 a second.
                var calm = timeline.Skip(1).Where(point => point.Time < Start + spikeStart).ToList();
                Assert.IsTrue(calm.All(point => point.P99Ms < 101));
                Assert.IsTrue(calm.All(point => Math.Abs(point.OkPerSecond - 10) < 0.01));
                Assert.IsTrue(calm.All(point => Math.Abs(point.MeanMs!.Value - 100) < 0.01));

                Assert.IsTrue(calm.All(point => Math.Abs(point.VirtualUsers!.Value - 1.0) < 0.01));
                Assert.IsGreaterThan(2.5, spike.Max(point => point.VirtualUsers!.Value));
            });
    }

    private static LoadSnapshot Snapshot(TimeSpan at, Histogram histogram, long totalTicks = 0)
    {
        var count = (int) histogram.TotalCount;
        var result = new ScenarioLoadResult { ScenarioName = "S", MeasurementStartTime = Start, Created = Start + at };
        var ok = new StatsState { Count = count, TotalTicks = totalTicks, Histogram = count > 0 ? histogram.Encode() : null };
        return new LoadSnapshot(Start + at, result, ok);
    }
}
