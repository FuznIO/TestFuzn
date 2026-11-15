using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Results.Load;

// One snapshot of a scenario: the result for the report's table (totals since the start) and the raw
// totals of the ok iterations, so the timeline can show each interval on its own.
internal record LoadSnapshot(DateTime Time, ScenarioLoadResult Result, StatsState Ok);

// One timeline point: the iterations that ended between the snapshot before and this one.
internal class LoadTimelinePoint
{
    public DateTime Time { get; init; }
    public long ElapsedMs { get; init; }
    public double Seconds { get; init; }
    public int OkCount { get; init; }
    public int FailedCount { get; init; }
    public double OkPerSecond { get; init; }
    public double FailedPerSecond { get; init; }

    // Null when no iteration ended in the interval.
    public double? MeanMs { get; init; }
    public double? P50Ms { get; init; }
    public double? P90Ms { get; init; }
    public double? P95Ms { get; init; }
    public double? P99Ms { get; init; }
    public double? VirtualUsers { get; init; }

    // Null when the simulation sets no rate, such as a fixed number of concurrent users.
    public double? TargetPerSecond { get; init; }
}

// Keeps at most a fixed number of snapshots, evenly spread over the run however long it is. While there
// is room, every snapshot is kept. When the list is full, every second one is dropped, and from then on
// only every second new one is kept; then every fourth, and so on. So the snapshots are 3 s apart at
// first, then 6 s, 12 s, ... The first and the latest snapshot are always there.
internal class EvenlySpreadSnapshots
{
    // A timeline point covers at least this, so a few iterations in a few milliseconds do not show as a
    // huge rate. The first snapshot comes with the first request, and the final one often comes right
    // after a regular one.
    private const double MinimumSeconds = 1;

    private readonly int _maxSnapshots;
    private readonly List<(long Number, LoadSnapshot Snapshot)> _kept = new();
    private long _keepEvery = 1;
    private long _nextNumber;
    private (long Number, LoadSnapshot Snapshot)? _latest;

    public EvenlySpreadSnapshots(int maxSnapshots)
    {
        _maxSnapshots = Math.Max(2, maxSnapshots);
    }

    public void AddSnapshot(LoadSnapshot snapshot)
    {
        var number = _nextNumber++;
        _latest = (number, snapshot);
        if (number % _keepEvery != 0)
            return;

        _kept.Add((number, snapshot));

        // Kept snapshots stay below the maximum, so the latest always fits too. The first has number 0
        // and is never dropped.
        if (_kept.Count < _maxSnapshots)
            return;

        _keepEvery *= 2;
        _kept.RemoveAll(kept => kept.Number % _keepEvery != 0);
    }

    public IReadOnlyList<LoadSnapshot> GetSnapshots()
    {
        var result = _kept.Select(kept => kept.Snapshot).ToList();
        if (_latest is { } latest && _kept[^1].Number != latest.Number)
            result.Add(latest.Snapshot);

        return result;
    }

    // Each point covers the time since the point before it, or since the measurement started, and at
    // least a second: a shorter snapshot is left out and its iterations count in the next point. The last
    // snapshot is always a point; if it comes too soon after the point before, it takes that one's place.
    public static IReadOnlyList<LoadTimelinePoint> Timeline(IReadOnlyList<LoadSnapshot> snapshots, IReadOnlyList<ILoadConfiguration>? simulations = null)
    {
        var chosen = new List<LoadSnapshot>();
        foreach (var snapshot in snapshots)
        {
            if (Seconds(chosen.LastOrDefault(), snapshot) >= MinimumSeconds)
                chosen.Add(snapshot);
        }

        if (snapshots.Count > 0 && (chosen.Count == 0 || chosen[^1] != snapshots[^1]))
        {
            if (chosen.Count > 0 && Seconds(chosen.Count > 1 ? chosen[^2] : null, snapshots[^1]) >= MinimumSeconds)
                chosen.RemoveAt(chosen.Count - 1);
            chosen.Add(snapshots[^1]);
        }

        var points = new List<LoadTimelinePoint>(chosen.Count);
        LoadSnapshot? previous = null;
        foreach (var snapshot in chosen)
        {
            var seconds = Seconds(previous, snapshot);
            var interval = IntervalStats.Point("", "", 0, snapshot.Ok, previous?.Ok, Empty, null);
            var failed = Math.Max(0, (snapshot.Result.Failed?.Count ?? 0) - (previous?.Result.Failed?.Count ?? 0));
            var elapsed = Elapsed(snapshots[0], snapshot);
            var busyTicks = BusyTicks(snapshot);
            if (previous != null)
                busyTicks -= BusyTicks(previous);
            double? virtualUsers = null;
            if (seconds > 0)
                virtualUsers = Math.Round(Math.Max(0, busyTicks) / (double) TimeSpan.TicksPerSecond / seconds, 1);
            points.Add(new LoadTimelinePoint
            {
                Time = snapshot.Time,
                ElapsedMs = (long)Math.Round(elapsed * 1000),
                Seconds = Math.Round(seconds, 3),
                OkCount = interval.OkCount,
                FailedCount = failed,
                OkPerSecond = seconds > 0 ? Math.Round(interval.OkCount / seconds, 1) : 0,
                FailedPerSecond = seconds > 0 ? Math.Round(failed / seconds, 1) : 0,
                MeanMs = interval.MeanMs,
                P50Ms = interval.P50Ms,
                P90Ms = interval.P90Ms,
                P95Ms = interval.P95Ms,
                P99Ms = interval.P99Ms,
                VirtualUsers = virtualUsers,
                TargetPerSecond = LoadTarget.PerSecond(simulations, elapsed - seconds, elapsed)
            });
            previous = snapshot;
        }

        return points;
    }

    private static long BusyTicks(LoadSnapshot snapshot)
    {
        var ticks = snapshot.Ok.TotalTicks;
        if (snapshot.Result.Failed != null)
            ticks += snapshot.Result.Failed.TotalExecutionDuration.Ticks;
        return ticks;
    }

    // The length of the interval that ends at the snapshot; 0 before the measurement started.
    private static double Seconds(LoadSnapshot? previous, LoadSnapshot snapshot)
    {
        var start = previous?.Time ?? DateTime.MinValue;
        var measurementStart = snapshot.Result.MeasurementStartTime;
        if (measurementStart != default && measurementStart > start)
            start = measurementStart;

        return start == DateTime.MinValue ? 0 : Math.Max(0, (snapshot.Time - start).TotalSeconds);
    }

    private static double Elapsed(LoadSnapshot first, LoadSnapshot snapshot)
    {
        var start = snapshot.Result.MeasurementStartTime != default ? snapshot.Result.MeasurementStartTime : first.Time;
        return Math.Max(0, (snapshot.Time - start).TotalSeconds);
    }

    private static readonly StatsState Empty = new();
}
