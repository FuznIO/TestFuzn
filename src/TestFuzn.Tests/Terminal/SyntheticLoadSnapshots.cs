using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Tests.Terminal;

internal static class SyntheticLoadSnapshots
{
    public static readonly DateTime BaseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public static DateTime At(double seconds)
    {
        return BaseTime + TimeSpan.FromSeconds(seconds);
    }

    public static ScenarioLoadResult Snapshot(int ok = 0, int failed = 0, int warmupOk = 0, int warmupFailed = 0, double okPercentile95Ms = 0)
    {
        var result = new ScenarioLoadResult();
        result.ScenarioName = "Checkout flow";
        result.Ok = BuildStats(ok, okPercentile95Ms);
        result.Failed = BuildStats(failed, 0);
        result.WarmupRequestCountOk = warmupOk;
        result.WarmupRequestCountFailed = warmupFailed;
        result.RequestCount = ok + failed;
        result.Steps = new Dictionary<string, StepLoadResult>();
        return result;
    }

    public static Stats BuildStats(int requestCount, double percentile95Ms)
    {
        var stats = new Stats();
        stats.RequestCount = requestCount;
        stats.ResponseTimePercentile95 = TimeSpan.FromMilliseconds(percentile95Ms);
        return stats;
    }

    public static StepLoadResult Step(string name, int ok = 0, int failed = 0)
    {
        var step = new StepLoadResult();
        step.Name = name;
        step.Ok = BuildStats(ok, 0);
        step.Failed = BuildStats(failed, 0);
        step.Steps = new List<StepLoadResult>();
        return step;
    }

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

    public static void AddStep(ScenarioLoadResult snapshot, StepLoadResult step)
    {
        snapshot.Steps[step.Name] = step;
    }
}
