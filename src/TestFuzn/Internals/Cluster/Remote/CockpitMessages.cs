using System.Text.Json;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

// The messages only the remote client and the controller send; the worker app's are in Protocol/Messages.cs.

internal class SubmitRunReply
{
    public string RunId { get; set; } = "";
    public string? LiveUrl { get; set; }
}

// The latest merged stats of a run, for attached clients. Seq grows with every new one.
internal class LiveReply
{
    public long Seq { get; set; }

    // How long ago Cockpit got it, by Cockpit's clock; the live view's times are shifted by it.
    public long AgeMs { get; set; }
    public LiveMessage Live { get; set; } = new();
}

// Controller to Cockpit, twice a tick: the command for the load workers, and after the merge the merged
// stats and the interval points. Every call also tells Cockpit the controller is still there.
internal class ControllerPollRequest
{
    public string? LoadCommand { get; set; }
    public bool Release { get; set; }
    public LiveMessage? Live { get; set; }
    public LivePointsMessage? Points { get; set; }
}

internal class ControllerPollReply
{
    public List<ClaimedWorker> Workers { get; set; } = new();

    // Who asked to stop the run (Ctrl+C on the client, Cockpit's Stop button); null if nobody did.
    public string? StopRequestedBy { get; set; }
}

internal class ClaimedWorker
{
    public int Index { get; set; }
    public WorkerInfo Info { get; set; } = new();
    public WorkerStatusMessage? Status { get; set; }

    // Grows with every status the worker sends; a new value means a fresh status.
    public long StatusSeq { get; set; }

    // How long ago Cockpit got that status, by Cockpit's clock.
    public long? StatusAgeMs { get; set; }
}

// Controller to Cockpit: claim the run's load workers, all or none. One run file per worker, by index.
internal class ClaimWorkersRequest
{
    public List<JsonElement> RunFiles { get; set; } = new();
}

internal class ClaimWorkersReply
{
    public bool Accepted { get; set; }
    public List<WorkerInfo> Workers { get; set; } = new();
    public int Free { get; set; }
    public List<WorkerInfo> Busy { get; set; } = new();

    // Why other workers of the pool could not be used, e.g. another protocol version.
    public List<string> Refusals { get; set; } = new();
}

internal class EndRunRequest
{
    public string Result { get; set; } = RunResults.Failed;
    public string? Error { get; set; }
    public LiveMessage? Live { get; set; }
}
