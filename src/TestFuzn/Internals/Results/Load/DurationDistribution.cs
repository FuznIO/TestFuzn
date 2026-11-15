using Fuzn.TestFuzn.Internals.Cluster;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal class DurationBucket
{
    public double FromMs { get; set; }
    public double ToMs { get; set; }
    public long OkCount { get; set; }
    public long FailedCount { get; set; }
}

internal static class DurationDistribution
{
    public const int TargetBuckets = 30;

    private static readonly double[] NiceSteps = [1, 2, 2.5, 5, 10];

    public static IReadOnlyList<DurationBucket> From(StatsState? ok, StatsState? failed, int targetBuckets = TargetBuckets)
    {
        var histograms = new List<(Histogram Histogram, bool Ok)>();
        var minTicks = long.MaxValue;
        var maxTicks = long.MinValue;
        foreach (var (state, isOk) in new[] { (ok, true), (failed, false) })
        {
            if (state == null || state.Count == 0 || state.Histogram == null)
                continue;

            histograms.Add((Histogram.Decode(state.Histogram), isOk));
            minTicks = Math.Min(minTicks, state.MinTicks);
            maxTicks = Math.Max(maxTicks, state.MaxTicks);
        }

        if (histograms.Count == 0)
            return [];

        var minMs = ToMs(minTicks);
        var maxMs = ToMs(maxTicks);
        var width = NiceWidth((maxMs - minMs) / Math.Max(1, targetBuckets));
        var start = Math.Floor(minMs / width) * width;
        var count = Math.Max(1, (int) Math.Floor((maxMs - start) / width) + 1);

        var buckets = new DurationBucket[count];
        for (var i = 0; i < count; i++)
        {
            buckets[i] = new DurationBucket();
            buckets[i].FromMs = Round(start + i * width);
            buckets[i].ToMs = Round(start + (i + 1) * width);
        }

        foreach (var (histogram, isOk) in histograms)
        {
            foreach (var (_, high, total) in histogram.Ranges())
            {
                var valueMs = Math.Clamp(ToMs(high), minMs, maxMs);
                var index = Math.Clamp((int) Math.Floor((valueMs - start) / width), 0, count - 1);
                if (isOk)
                    buckets[index].OkCount += total;
                else
                    buckets[index].FailedCount += total;
            }
        }

        return buckets;
    }

    internal static double NiceWidth(double raw)
    {
        if (raw <= 0)
            return 0.001;

        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (var step in NiceSteps)
        {
            if (step * power >= raw)
                return Math.Max(0.001, step * power);
        }

        return 10 * power;
    }

    private static double ToMs(double ticks)
    {
        return ticks / TimeSpan.TicksPerMillisecond;
    }

    private static double Round(double ms)
    {
        return Math.Round(ms, 3);
    }
}
