using System.Text.Json;

namespace Fuzn.TestFuzn.Internals.Cluster.LoadChild;

// run.json, written by the worker app before it starts the load child.
internal class LoadChildRunSettings
{
    public int Protocol { get; set; }
    public string RunId { get; set; } = "";
    public string TestName { get; set; } = "";
    public int WorkerIndex { get; set; }
    public int WorkerCount { get; set; } = 1;
    public int Seed { get; set; }
    public string? TargetEnvironment { get; set; }

    // Scenario name to what its BeforeScenarioOnce returned.
    public Dictionary<string, JsonElement>? OnceData { get; set; }
}

// status.json, written by the load child and published by the worker app. The envelope is all the
// worker app reads; Stats passes through to the controller unchanged.
internal class LoadChildStatus
{
    public int Protocol { get; set; } = LoadChildProtocol.Version;
    public string RunId { get; set; } = "";
    public string State { get; set; } = LoadChildStates.Preparing;
    public string? Error { get; set; }
    public string? ResultsDirectory { get; set; }
    public LoadStatsState? Stats { get; set; }
}

internal static class LoadChildProtocol
{
    public const int Version = 1;
    public const string RunArgument = "cluster-run";
    public const string ControllerArgument = "cluster-controller";
    public const string StatusFileName = "status.json";

    public const string StartCommand = "start";
    public const string StartMeasurementCommand = "start-measurement";
    public const string StopCommand = "stop";
}

internal static class LoadChildStates
{
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string Warmup = "Warmup";
    public const string WarmupDone = "WarmupDone";
    public const string Measuring = "Measuring";
    public const string Stopping = "Stopping";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Stopped = "Stopped";

    public static bool IsFinal(string state) => state == Completed || state == Failed || state == Stopped;
}

// The part of controller.json the runner needs to find and start the test; the controller reads the rest.
internal class ControllerLaunchSettings
{
    public string RunId { get; set; } = "";
    public string TestName { get; set; } = "";
    public string? TargetEnvironment { get; set; }
    public int Workers { get; set; }
}
