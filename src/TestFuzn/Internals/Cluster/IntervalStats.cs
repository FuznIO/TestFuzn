using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Internals.Cluster;

// Send cumulative, show per interval: the previous state is subtracted from the current one, and the
// percentiles come from the difference of the histograms. Two cumulative percentiles cannot give this.
internal static class IntervalStats
{
    public static List<IntervalPoint> Points(ScenarioStatsState current, ScenarioStatsState? previous, long timeMs)
    {
        var points = new List<IntervalPoint>();
        if (previous == null)
        {
            points.Add(WithUsers(Point(current.Name, current.Phase, timeMs, current.Ok, null, current.Failed, null), current, null));
            AddSteps(points, current.Name, current.Phase, timeMs, current.Steps, null);
        }
        else
        {
            points.Add(WithUsers(Point(current.Name, current.Phase, timeMs, current.Ok, previous.Ok, current.Failed, previous.Failed), current, previous));
            AddSteps(points, current.Name, current.Phase, timeMs, current.Steps, previous.Steps);
        }
        return points;
    }

    private static IntervalPoint WithUsers(IntervalPoint point, ScenarioStatsState current, ScenarioStatsState? previous)
    {
        var busyTicks = current.Ok.TotalTicks + current.Failed.TotalTicks;
        var intervalTicks = current.MeasurementDurationTicks;
        if (previous != null)
        {
            busyTicks -= previous.Ok.TotalTicks + previous.Failed.TotalTicks;
            intervalTicks -= previous.MeasurementDurationTicks;
        }

        if (intervalTicks <= 0)
            return point;

        return new IntervalPoint
        {
            Path = point.Path,
            Phase = point.Phase,
            TimeMs = point.TimeMs,
            OkCount = point.OkCount,
            FailedCount = point.FailedCount,
            MeanMs = point.MeanMs,
            P50Ms = point.P50Ms,
            P90Ms = point.P90Ms,
            P95Ms = point.P95Ms,
            P99Ms = point.P99Ms,
            VirtualUsers = Math.Round(Math.Max(0, busyTicks) / (double) intervalTicks, 1)
        };
    }

    private static void AddSteps(List<IntervalPoint> points, string parentPath, string phase, long timeMs, List<StepStatsState> steps, List<StepStatsState>? previousSteps)
    {
        foreach (var step in steps)
        {
            StepStatsState? previous = null;
            if (previousSteps != null)
                previous = previousSteps.FirstOrDefault(s => s.Name == step.Name);
            var path = parentPath + " > " + step.Name;
            if (previous == null)
            {
                var point = Point(path, phase, timeMs, step.Ok, null, step.Failed, null);
                point.StatusCodes = StatusCodesSince(step, null);
                points.Add(point);
                AddSteps(points, path, phase, timeMs, step.Steps, null);
            }
            else
            {
                var point = Point(path, phase, timeMs, step.Ok, previous.Ok, step.Failed, previous.Failed);
                point.StatusCodes = StatusCodesSince(step, previous);
                points.Add(point);
                AddSteps(points, path, phase, timeMs, step.Steps, previous.Steps);
            }
        }
    }

    private static Dictionary<int, long>? StatusCodesSince(StepStatsState step, StepStatsState? previous)
    {
        if (step.StatusCodes == null || step.StatusCodes.Count == 0)
            return null;

        var codes = new Dictionary<int, long>();
        foreach (var code in step.StatusCodes)
        {
            var count = code.Value;
            if (previous != null && previous.StatusCodes != null && previous.StatusCodes.TryGetValue(code.Key, out var before))
                count -= before;
            if (count > 0)
                codes[code.Key] = count;
        }
        if (codes.Count == 0)
            return null;
        return codes;
    }

    internal static IntervalPoint Point(string path, string phase, long timeMs, StatsState ok, StatsState? previousOk, StatsState failed, StatsState? previousFailed)
    {
        var okCount = ok.Count;
        if (previousOk != null)
            okCount -= previousOk.Count;
        var failedCount = failed.Count;
        if (previousFailed != null)
            failedCount -= previousFailed.Count;

        double? mean = null, p50 = null, p90 = null, p95 = null, p99 = null;
        if (okCount > 0 && ok.Histogram != null)
        {
            var current = Histogram.Decode(ok.Histogram);
            var before = previousOk != null && previousOk.Histogram != null ? Histogram.Decode(previousOk.Histogram) : null;
            var interval = Histogram.Subtract(current, before);
            if (interval.TotalCount > 0)
            {
                var totalTicks = ok.TotalTicks;
                if (previousOk != null)
                    totalTicks -= previousOk.TotalTicks;
                mean = Milliseconds(totalTicks / okCount);
                p50 = Milliseconds(interval.ValueAtPercentile(50));
                p90 = Milliseconds(interval.ValueAtPercentile(90));
                p95 = Milliseconds(interval.ValueAtPercentile(95));
                p99 = Milliseconds(interval.ValueAtPercentile(99));
            }
        }

        return new IntervalPoint
        {
            Path = path,
            Phase = phase,
            TimeMs = timeMs,
            OkCount = Math.Max(0, okCount),
            FailedCount = Math.Max(0, failedCount),
            MeanMs = mean,
            P50Ms = p50,
            P90Ms = p90,
            P95Ms = p95,
            P99Ms = p99
        };
    }

    private static double Milliseconds(long ticks) => Math.Round(TimeSpan.FromTicks(ticks).TotalMilliseconds, 3);
}
