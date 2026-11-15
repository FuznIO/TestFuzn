using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fuzn.TestFuzn.Internals.Cluster.Protocol;

// What a worker is and whether it is free. The worker sends it with every poll; Cockpit fills in the busy
// fields from its own claims, so a worker claimed a moment ago already shows as busy.
internal class WorkerInfo
{
    public string WorkerId { get; set; } = "";
    public string Pool { get; set; } = "";
    public int Protocol { get; set; }
    public string Version { get; set; } = "";
    public string Os { get; set; } = "";
    public string Architecture { get; set; } = "";
    public int CpuCores { get; set; }
    public long MemoryMb { get; set; }
    public bool Busy { get; set; }
    public string? BusyRunId { get; set; }
    public string? BusyBy { get; set; }
    public string? BusyTest { get; set; }
    public DateTime? BusySince { get; set; }

    // Busy with a run that has ended: the worker is free within seconds. Set by Cockpit.
    public bool BusyRunEnded { get; set; }

    // The runs this worker is the controller of; a controller makes no load, so it does not make a worker busy.
    public List<string> ControllerRunIds { get; set; } = new();

    // When Cockpit last heard from the worker.
    public DateTime UpdatedUtc { get; set; }
}

// Client to Cockpit: start a run on a pool. Cockpit gives it to one worker of the pool, which starts the
// controller child with it.
internal class ControllerJob
{
    public int Protocol { get; set; } = ClusterProtocol.Version;
    public string RunId { get; set; } = "";
    public string TestName { get; set; } = "";
    public string EntryAssembly { get; set; } = "";
    public Dictionary<string, string> Builds { get; set; } = new();
    public string Pool { get; set; } = "";
    public int Workers { get; set; }
    public int ConnectionTimeoutSeconds { get; set; }
    public string? TargetEnvironment { get; set; }
    public int Seed { get; set; }

    // Who started the run and from where, for the run list and the busy message.
    public RunEntry Entry { get; set; } = new();

    public string? Suite { get; set; }
    public string? Source { get; set; }
    public string? Branch { get; set; }
    public string? Commit { get; set; }
    public string? BuildNumber { get; set; }
    public string? LogUrl { get; set; }

    public string? ControllerWorkerId { get; set; }

    // Set by the worker app: where the controller reaches Cockpit (the worker's own address for it).
    public string? CockpitUrl { get; set; }
}

// Cockpit to one claimed worker: run a load child for this run.
internal class ClaimRequest
{
    public int Protocol { get; set; } = ClusterProtocol.Version;
    public string RunId { get; set; } = "";
    public string Owner { get; set; } = "";
    public string TestName { get; set; } = "";
    public string EntryAssembly { get; set; } = "";
    public Dictionary<string, string> Builds { get; set; } = new();
    public int ConnectionTimeoutSeconds { get; set; }

    // Written to run.json as is; the worker app does not read it.
    public JsonElement RunFile { get; set; }
}

// What a worker app knows about its load job: the load child's status.json plus what only the worker app knows.
internal class WorkerStatusMessage
{
    public string WorkerId { get; set; } = "";
    public string RunId { get; set; } = "";
    public string Phase { get; set; } = WorkerJobPhases.Downloading;
    public bool ChildAlive { get; set; }
    public int? ExitCode { get; set; }

    // The last lines of the child's output when it died without writing a final status.
    public string? OutputTail { get; set; }
    public string? Error { get; set; }

    // The load child's status.json, passed through unchanged.
    public JsonElement? Status { get; set; }
}

// Worker app to Cockpit, every few seconds: here I am, this is my load job, these controllers ended.
// The answer is the work for this worker.
internal class WorkerPollRequest
{
    public WorkerInfo Worker { get; set; } = new();
    public WorkerStatusMessage? LoadJob { get; set; }

    // Load jobs that have ended, so the worker is free again. Sent until Cockpit has answered once.
    public List<string> DoneLoadRunIds { get; set; } = new();

    // Controller children that exited; Cockpit ends their run if the controller did not.
    public List<EndedController> EndedControllers { get; set; } = new();
}

internal class EndedController
{
    public string RunId { get; set; } = "";
    public string? Error { get; set; }
}

internal class WorkerPollReply
{
    // A run of this worker's pool to be the controller of.
    public ControllerJob? ControllerJob { get; set; }

    // A load job this worker was claimed for.
    public ClaimRequest? LoadJob { get; set; }

    // What the controller wants from the current load job.
    public LoadJobCommand? LoadCommand { get; set; }
}

internal class LoadJobCommand
{
    public string RunId { get; set; } = "";

    // start, start-measurement or stop; null before the controller sent one.
    public string? Command { get; set; }

    // How long until the command takes effect. A start is set a few seconds ahead, so every worker has it
    // in time and they all start at once.
    public int InMs { get; set; }

    // The controller has every final status (or the run ended); the worker can clean up.
    public bool Released { get; set; }

    // How long ago the controller last called Cockpit. Longer than the connection timeout means it is gone.
    public long ControllerQuietMs { get; set; }
}

// Controller to Cockpit, once per tick: what happened since the previous tick, as plain numbers, for
// dashboards that should not need TestFuzn's histogram format.
internal class LivePointsMessage
{
    public string RunId { get; set; } = "";
    public string TestName { get; set; } = "";
    public DateTime ControllerUtc { get; set; }
    public List<LivePoint> Points { get; set; } = new();
    public List<LiveGenerator> Generators { get; set; } = new();
    public List<LiveError> Errors { get; set; } = new();
    public string? StoppedReason { get; set; }
}

internal class LivePoint
{
    // "Scenario" or "Scenario > Step".
    public string Path { get; set; } = "";
    public string Phase { get; set; } = "";
    public long TMs { get; set; }
    public int OkCount { get; set; }
    public int FailedCount { get; set; }
    public double? DurationMeanMs { get; set; }
    public double? DurationP50Ms { get; set; }
    public double? DurationP90Ms { get; set; }
    public double? DurationP95Ms { get; set; }
    public double? DurationP99Ms { get; set; }
    public double? VirtualUsers { get; set; }
    public Dictionary<int, long>? StatusCodes { get; set; }
}

internal class LiveError
{
    public string Path { get; set; } = "";
    public string Message { get; set; } = "";
    public int Count { get; set; }
    public bool IsOther { get; set; }
}

internal class LiveGenerator
{
    public string GeneratorName { get; set; } = "";
    public string State { get; set; } = "";
    public long TMs { get; set; }
    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }
    public double? StartLagMs { get; set; }
}

internal class RunControlRequest
{
    public string? RequestedBy { get; set; }
}

internal class RunControlReply
{
    public string? Message { get; set; }
}

// A run as Cockpit keeps it: created when the client submits it, then kept up to date by the controller.
internal class RunEntry
{
    public string RunId { get; set; } = "";
    public string? Name { get; set; }
    public string TestName { get; set; } = "";
    public string StartedBy { get; set; } = "";
    public string FromMachine { get; set; } = "";
    public string Pool { get; set; } = "";
    public int Workers { get; set; }
    public string? TargetEnvironment { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? EndedUtc { get; set; }
    public string State { get; set; } = RunStates.Starting;
    public string? Result { get; set; }
    public string? Error { get; set; }
    public string? ControllerWorkerId { get; set; }
    public int ConnectionTimeoutSeconds { get; set; }

    // Set by Cockpit: the controller stopped calling for longer than the connection timeout. Cockpit decides
    // it with its own clock, so a client with a wrong clock still sees the right state.
    public bool IsLost { get; set; }

    public int? QueuePosition { get; set; }

    public string? LiveUrl { get; set; }
    public string? RunUrl { get; set; }

    [JsonIgnore]
    public bool IsEnded => State == RunStates.Ended;

    public string DisplayState() => !IsEnded && IsLost ? RunResults.Lost : State;
}

internal static class ProtocolJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json)
    {
        var value = JsonSerializer.Deserialize<T>(json, Options);
        if (value == null)
            throw new InvalidDataException($"Expected a {typeof(T).Name} but the message was empty.");

        return value;
    }
}
