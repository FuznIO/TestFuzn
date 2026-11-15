namespace Fuzn.TestFuzn.Internals.Cluster.Protocol;

// Shared by TestFuzn and the worker app (compiled into it), so both sides use the same names.
internal static class ClusterProtocol
{
    // Bump when a message changes in a way older parts cannot read. Workers with another version are not selected.
    public const int Version = 3;

    // A free worker asks Cockpit for work this often; a worker with a load job asks more often, so a start
    // command reaches it well before the moment it takes effect.
    public static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan JobPollInterval = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan ChildKillGrace = TimeSpan.FromSeconds(30);

    public const string RunFileName = "run.json";
    public const string ControllerFileName = "controller.json";
    public const string RunSummaryFileName = "run-summary.json";
    public const string OutputLogFileName = "output.log";

    public static string OsFamily()
    {
        if (OperatingSystem.IsWindows())
            return OsFamilies.Windows;
        if (OperatingSystem.IsMacOS())
            return OsFamilies.MacOs;

        return OsFamilies.Linux;
    }
}

// Cockpit's routes, by caller: the remote client (a project API key), the worker app (the worker key) and
// a run's controller (the run token Cockpit gave it).
internal static class CockpitPaths
{
    public static string PoolWorkers(string pool) => $"api/testfuzn/remote/workers?pool={Uri.EscapeDataString(pool)}";
    public static string Build(string hash) => $"api/testfuzn/remote/builds/{hash}";
    public const string Runs = "api/testfuzn/remote/runs";
    public static string Run(string runId) => $"api/testfuzn/remote/runs/{runId}";
    public static string Live(string runId, long afterSeq) => $"api/testfuzn/remote/runs/{runId}/live?after={afterSeq}";
    public static string Stop(string runId) => $"api/testfuzn/remote/runs/{runId}/stop";

    public const string WorkerPoll = "api/testfuzn/worker/poll";
    public const string WorkerLeave = "api/testfuzn/worker/leave";
    public static string WorkerBuild(string hash) => $"api/testfuzn/worker/builds/{hash}";

    public static string ControllerPoll(string runId) => $"api/testfuzn/controller/runs/{runId}/poll";
    public static string ControllerClaim(string runId) => $"api/testfuzn/controller/runs/{runId}/claim";
    public static string ControllerReport(string runId) => $"api/testfuzn/controller/runs/{runId}/report";
    public static string ControllerEnd(string runId) => $"api/testfuzn/controller/runs/{runId}/end";
}

internal static class OsFamilies
{
    public const string Windows = "windows";
    public const string Linux = "linux";
    public const string MacOs = "osx";
}

internal static class RunCommands
{
    public const string Start = "start";
    public const string StartMeasurement = "start-measurement";
    public const string Stop = "stop";
}

internal static class RunStates
{
    public const string Starting = "Starting";
    public const string Running = "Running";
    public const string Ended = "Ended";
}

internal static class RunResults
{
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Stopped = "Stopped";
    public const string Lost = "Lost";
}

internal static class WorkerJobPhases
{
    public const string Downloading = "Downloading";
    public const string Starting = "Starting";
    public const string Running = "Running";
    public const string Exited = "Exited";
}
