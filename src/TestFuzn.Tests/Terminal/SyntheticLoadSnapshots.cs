using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Builders for the synthetic <see cref="ScenarioLoadResult"/> sequences the live metrics model
/// tests feed into <c>ScenarioLiveMetrics.Record</c>, shaped like what
/// <c>ScenarioLoadCollector.GetCurrentResult</c> produces: cumulative counters, phase
/// timestamps, per-step stats and message-keyed error entries.
/// </summary>
internal static class SyntheticLoadSnapshots
{
    /// <summary>Fixed base time all synthetic timestamps are offsets from.</summary>
    public static readonly DateTime BaseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The synthetic clock at the given number of seconds past <see cref="BaseTime"/>.</summary>
    public static DateTime At(double seconds)
    {
        return BaseTime + TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// A snapshot with the given cumulative counters. <paramref name="okPercentile95Ms"/> is
    /// the cumulative Ok p95, <paramref name="intervalPercentile95Ms"/> the per-interval p95 a
    /// force-refresh would have computed. Phase timestamps stay default; tests set the ones a
    /// scenario has reached directly on the returned instance.
    /// </summary>
    public static ScenarioLoadResult Snapshot(int ok = 0, int failed = 0, int warmupOk = 0, int warmupFailed = 0, double okPercentile95Ms = 0, double intervalPercentile95Ms = 0)
    {
        var result = new ScenarioLoadResult();
        result.ScenarioName = "Checkout flow";
        result.Ok = BuildStats(ok, okPercentile95Ms);
        result.Failed = BuildStats(failed, 0);
        result.WarmupRequestCountOk = warmupOk;
        result.WarmupRequestCountFailed = warmupFailed;
        result.RequestCount = ok + failed;
        result.IntervalResponseTimePercentile95 = TimeSpan.FromMilliseconds(intervalPercentile95Ms);
        result.Steps = new Dictionary<string, StepLoadResult>();
        return result;
    }

    /// <summary>Cumulative stats with the given request count and Ok p95.</summary>
    public static Stats BuildStats(int requestCount, double percentile95Ms)
    {
        var stats = new Stats();
        stats.RequestCount = requestCount;
        stats.ResponseTimePercentile95 = TimeSpan.FromMilliseconds(percentile95Ms);
        return stats;
    }

    /// <summary>A step result with the given cumulative ok/failed counts and no errors.</summary>
    public static StepLoadResult Step(string name, int ok = 0, int failed = 0)
    {
        var step = new StepLoadResult();
        step.Name = name;
        step.Ok = BuildStats(ok, 0);
        step.Failed = BuildStats(failed, 0);
        step.Steps = new List<StepLoadResult>();
        return step;
    }

    /// <summary>Adds (or replaces) a message-keyed error entry on a step, like the collector stores them.</summary>
    public static void AddError(StepLoadResult step, string message, int count)
    {
        if (step.Errors == null)
            step.Errors = new Dictionary<string, ErrorEntry>();

        var entry = new ErrorEntry();
        entry.Message = message;
        entry.Details = message;
        entry.Count = count;
        step.Errors[message] = entry;
    }

    /// <summary>Adds a top-level step to a snapshot's step dictionary.</summary>
    public static void AddStep(ScenarioLoadResult snapshot, StepLoadResult step)
    {
        snapshot.Steps[step.Name] = step;
    }
}
