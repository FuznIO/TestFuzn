using Fuzn.TestFuzn.Internals.Cluster.Protocol;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

// An attached client: waits for the run to end and keeps the controller's latest merged stats for the
// summary. Used by --cluster. It asks Cockpit every second;
// the stats are cumulative, so only the latest one matters.
internal sealed class RunAttachment
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(30);

    private readonly CockpitConnection _cockpit;
    private readonly string _runId;
    private readonly TimeSpan _connectionTimeout;
    private readonly string? _liveUrl;
    private readonly object _lock = new();
    private TestExecutionState? _state;
    private long _liveSeq;

    public RunAttachment(CockpitConnection cockpit, string runId, TimeSpan connectionTimeout, string? liveUrl)
    {
        _cockpit = cockpit;
        _runId = runId;
        _connectionTimeout = connectionTimeout;
        _liveUrl = liveUrl;
    }

    private string ResultHint => _liveUrl != null ? $"See the result in Cockpit: {_liveUrl}" : "See the result on the TestFuzn cluster page in Cockpit.";

    public LiveMessage? Latest { get; private set; }

    public void Attach(TestExecutionState state)
    {
        lock (_lock)
        {
            _state = state;
            if (Latest != null)
                Apply(state, Latest, DateTime.UtcNow);
        }
    }

    // The latest merged stats, or null when the controller has sent none yet.
    public async Task<LiveMessage?> ReadLive(CancellationToken cancellationToken)
    {
        var reply = await _cockpit.Get<LiveReply>(CockpitPaths.Live(_runId, _liveSeq), cancellationToken);
        if (reply == null)
            return Latest;

        lock (_lock)
        {
            _liveSeq = reply.Seq;
            Latest = reply.Live;
            if (_state != null)
                Apply(_state, reply.Live, DateTime.UtcNow - TimeSpan.FromMilliseconds(reply.AgeMs));
        }

        return reply.Live;
    }

    // Ctrl+C, the quit key or a cancelled test stop the run on all workers; the client still waits for its end.
    public async Task<RunEntry> WaitForEnd(CancellationToken stopToken, CancellationToken abortToken, Action<TimeSpan>? progress = null)
    {
        var stopSent = false;
        var lastHeardUtc = DateTime.UtcNow;
        var startedUtc = DateTime.UtcNow;
        var nextProgressUtc = startedUtc + ProgressInterval;

        while (true)
        {
            abortToken.ThrowIfCancellationRequested();

            try
            {
                if (stopToken.IsCancellationRequested && !stopSent)
                {
                    await _cockpit.Post<RunControlReply>(CockpitPaths.Stop(_runId), new RunControlRequest { RequestedBy = Environment.UserName + "@" + Environment.MachineName }, abortToken);
                    stopSent = true;
                }

                await ReadLive(abortToken);
                var entry = await _cockpit.Get<RunEntry>(CockpitPaths.Run(_runId), abortToken);
                if (entry == null)
                    throw new RemoteRunException($"Cockpit has no run {_runId} any more.");

                lastHeardUtc = DateTime.UtcNow;
                if (entry.IsEnded)
                {
                    // The final stats come with the end, so the summary shows the whole run.
                    await ReadLive(abortToken);
                    return entry;
                }

                if (entry.IsLost)
                    throw new RemoteRunException($"Run {_runId} is lost: its controller has not reached Cockpit for {_connectionTimeout.TotalSeconds:0} s. Its workers stop by themselves. {ResultHint}");
            }
            catch (CockpitException ex) when (ex.StatusCode == null || ex.StatusCode >= 500)
            {
                // Cockpit is restarting or the network is down; the run goes on without us.
                if (DateTime.UtcNow - lastHeardUtc > _connectionTimeout)
                    throw new RemoteRunException($"Lost the connection to Cockpit for {_connectionTimeout.TotalSeconds:0} s ({ex.Message}). The run continues on the workers. {ResultHint}");
            }

            if (progress != null && DateTime.UtcNow >= nextProgressUtc)
            {
                progress(DateTime.UtcNow - startedUtc);
                nextProgressUtc = DateTime.UtcNow + ProgressInterval;
            }

            await Task.Delay(PollInterval, abortToken);
        }
    }

    public static string FormatElapsed(TimeSpan elapsed)
        => elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"m\:ss");

    // Puts the controller's merged stats into this process's collectors, so the live view, the summary
    // and the asserts read them as if the load ran here.
    public static void Apply(TestExecutionState state, LiveMessage message, DateTime receivedUtc)
    {
        var clockOffset = receivedUtc - message.ControllerUtc;
        foreach (var live in message.Scenarios)
        {
            var scenario = state.Scenarios.FirstOrDefault(s => s.Name == live.Stats.Name);
            if (scenario == null || !state.LoadCollectors.TryGetValue(scenario.Name, out var collector))
                continue;

            if (scenario.SimulationsInternal.Count == 0 && live.Stats.Simulations.Count > 0)
            {
                foreach (var simulation in live.Stats.Simulations)
                    scenario.SimulationsInternal.Add(new DescribedLoadConfiguration(simulation.Description, simulation.IsWarmup));
            }

            collector.LoadMerged([live.Stats], TimeSpan.FromTicks(live.Stats.MeasurementDurationTicks));
            collector.ApplyTimes(live.Times, clockOffset);
        }

        state.Workers = message.Workers;
        lock (state.Warnings)
        {
            state.Warnings.Clear();
            state.Warnings.AddRange(message.Warnings);
        }
    }
}
