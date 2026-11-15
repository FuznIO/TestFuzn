using Fuzn.TestFuzn.Internals.Cluster.Protocol;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

// What a user reads when a pool has too few free workers: which runs hold them and what to do about it.
internal static class PoolMessages
{
    // How long a run waits for workers that are busy only with a run that has ended.
    public static readonly TimeSpan FinishingWait = TimeSpan.FromSeconds(15);

    public static string NotEnoughWorkers(string pool, int free, int needed, IEnumerable<WorkerInfo> busyWorkers, IEnumerable<string> otherReasons)
    {
        var lines = new List<string>
        {
            $"pool \"{pool}\" has {free} free worker{(free == 1 ? "" : "s")}; the run needs {needed}."
        };

        foreach (var run in busyWorkers.GroupBy(worker => worker.BusyRunId))
        {
            var first = run.First();
            var since = "";
            if (first.BusySince != null)
                since = $", since {first.BusySince.Value.ToLocalTime():HH:mm}";
            var count = run.Count();
            lines.Add($"Busy with run {run.Key} ({ShortTestName(first.BusyTest)}, {first.BusyBy}{since}) on {count} worker{(count == 1 ? "" : "s")}.");
        }

        lines.AddRange(otherReasons);
        lines.Add("See and stop the runs on the TestFuzn cluster page in Cockpit.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string ShortTestName(string? testName)
    {
        if (string.IsNullOrEmpty(testName))
            return "unknown test";

        var dot = testName.LastIndexOf('.');
        return dot >= 0 ? testName.Substring(dot + 1) : testName;
    }
}
