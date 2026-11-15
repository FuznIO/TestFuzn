using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;

namespace Fuzn.TestFuzn.Worker;

// The worker app's one loop: ask Cockpit for work every few seconds (every second while a load job runs),
// and send along what this worker is doing. Cockpit's answer is the work: a run to be the controller of,
// a load job this worker was claimed for, and the controller's command for the current load job.
internal partial class WorkerHost
{
    private static readonly TimeSpan LeaveTimeout = TimeSpan.FromSeconds(5);

    private readonly object _lock = new();
    private readonly List<Task> _controllerJobs = new();
    private readonly HashSet<string> _controllerRunIds = new();
    private readonly HashSet<string> _takenControllerRunIds = new();
    private readonly List<EndedController> _endedControllers = new();
    private readonly List<string> _doneLoadRunIds = new();
    private readonly HashSet<string> _finishedLoadRunIds = new();
    private TaskCompletionSource _wakeSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private LoadJob? _loadJob;
    private DateTime? _unreachableSinceUtc;

    public WorkerHost(WorkerOptions options, CockpitConnection cockpit)
    {
        Options = options;
        Cockpit = cockpit;
        Builds = new BuildCache(cockpit, options.WorkDirectory);
        WorkerId = CreateWorkerId();
    }

    public static string AppVersion { get; } = ReadAppVersion();

    public WorkerOptions Options { get; }
    public CockpitConnection Cockpit { get; }
    public BuildCache Builds { get; }

    // Machine name plus a random suffix, so two workers never share an id.
    public string WorkerId { get; }

    public static string BuildFor(Dictionary<string, string> builds)
    {
        var os = ClusterProtocol.OsFamily();
        if (!builds.TryGetValue(os, out var hash))
            throw new InvalidOperationException($"The run has no build for {os} workers.");

        return hash;
    }

    private static string ReadAppVersion()
    {
        var attribute = typeof(WorkerHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute == null)
            return "0.0.0";

        return attribute.InformationalVersion.Split('+')[0];
    }

    // A load job's state changed: send its status now, so the start and the sync point do not wait.
    public void Wake()
    {
        Interlocked.Exchange(ref _wakeSignal, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }

    // The first poll must succeed, so a wrong address or key stops the worker at once with the reason.
    public async Task Join(CancellationToken shutdown)
    {
        await Poll(shutdown);
    }

    public async Task Run(CancellationToken shutdown)
    {
        while (!shutdown.IsCancellationRequested)
        {
            var wake = _wakeSignal.Task;
            try
            {
                await Poll(shutdown);
                if (_unreachableSinceUtc != null)
                {
                    Console.WriteLine($"Reached Cockpit again after {(DateTime.UtcNow - _unreachableSinceUtc.Value).TotalSeconds:0} s.");
                    _unreachableSinceUtc = null;
                }
            }
            catch (CockpitException ex)
            {
                // A running load job keeps going; it stops by itself if the controller stays silent too long.
                if (_unreachableSinceUtc == null)
                {
                    _unreachableSinceUtc = DateTime.UtcNow;
                    Console.WriteLine($"{ex.Message} Trying again.");
                }
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
                break;
            }

            var interval = HasLoadJob() ? ClusterProtocol.JobPollInterval : ClusterProtocol.IdlePollInterval;
            try
            {
                await Task.WhenAny(Task.Delay(interval, shutdown), wake);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await Shutdown();
    }

    private async Task Shutdown()
    {
        Console.WriteLine("Shutting down: stopping children.");
        LoadJob? loadJob;
        Task[] controllerJobs;
        lock (_lock)
        {
            loadJob = _loadJob;
            controllerJobs = _controllerJobs.ToArray();
        }

        if (loadJob != null)
            await loadJob.Shutdown();

        await Task.WhenAll(controllerJobs).WaitAsync(ClusterProtocol.ChildKillGrace + TimeSpan.FromSeconds(5)).ContinueWith(_ => { });

        // Tells Cockpit the worker is gone and which of its jobs ended; without it the worker leaves the list
        // once it has been silent long enough.
        try
        {
            using var timeout = new CancellationTokenSource(LeaveTimeout);
            await Cockpit.Post(CockpitPaths.WorkerLeave, BuildRequest(), timeout.Token);
        }
        catch (Exception)
        {
        }
    }

    private bool HasLoadJob()
    {
        lock (_lock)
            return _loadJob != null;
    }

    private async Task Poll(CancellationToken shutdown)
    {
        var request = BuildRequest();
        var reply = await Cockpit.Post<WorkerPollReply>(CockpitPaths.WorkerPoll, request, shutdown);

        LoadJob? loadJob;
        lock (_lock)
        {
            // Sent and answered: Cockpit has them now.
            _doneLoadRunIds.RemoveAll(request.DoneLoadRunIds.Contains);
            _endedControllers.RemoveAll(ended => request.EndedControllers.Any(sent => sent.RunId == ended.RunId));
            loadJob = _loadJob;
        }

        if (reply.LoadCommand != null && loadJob != null && reply.LoadCommand.RunId == loadJob.RunId)
            loadJob.Apply(reply.LoadCommand);

        if (reply.ControllerJob != null)
            StartController(reply.ControllerJob, shutdown);

        if (reply.LoadJob != null)
            StartLoad(reply.LoadJob, shutdown);
    }

    private WorkerPollRequest BuildRequest()
    {
        var request = new WorkerPollRequest { Worker = Describe() };
        LoadJob? loadJob;
        lock (_lock)
        {
            request.DoneLoadRunIds = new List<string>(_doneLoadRunIds);
            request.EndedControllers = _endedControllers.Select(ended => new EndedController { RunId = ended.RunId, Error = ended.Error }).ToList();
            loadJob = _loadJob;
        }

        if (loadJob != null)
            request.LoadJob = loadJob.Status();

        return request;
    }

    private WorkerInfo Describe()
    {
        var info = new WorkerInfo
        {
            WorkerId = WorkerId,
            Pool = Options.Pool,
            Protocol = ClusterProtocol.Version,
            Version = AppVersion,
            Os = ClusterProtocol.OsFamily(),
            Architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            CpuCores = Environment.ProcessorCount,
            MemoryMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024),
            UpdatedUtc = DateTime.UtcNow
        };

        lock (_lock)
        {
            info.ControllerRunIds = _controllerRunIds.ToList();
            if (_loadJob != null)
            {
                info.Busy = true;
                info.BusyRunId = _loadJob.RunId;
                info.BusyBy = _loadJob.Owner;
                info.BusyTest = _loadJob.TestName;
                info.BusySince = _loadJob.StartedUtc;
            }
        }

        return info;
    }

    // Cockpit offers a job again until the worker reports it, in case an answer was lost; a job this worker
    // already took is not started twice.
    private void StartController(ControllerJob job, CancellationToken shutdown)
    {
        lock (_lock)
        {
            if (!_takenControllerRunIds.Add(job.RunId))
                return;
        }

        if (job.Protocol != ClusterProtocol.Version)
        {
            EndController(job.RunId, $"Worker {WorkerId} uses protocol {ClusterProtocol.Version}, the run uses {job.Protocol}. Update the worker app.");
            return;
        }

        job.CockpitUrl = Cockpit.Url;
        Console.WriteLine($"Run {job.RunId}: controller for {job.TestName} ({job.Entry.StartedBy}).");
        lock (_lock)
        {
            _controllerJobs.RemoveAll(task => task.IsCompleted);
            _controllerRunIds.Add(job.RunId);
            _controllerJobs.Add(Task.Run(async () =>
            {
                string? failure = null;
                try
                {
                    failure = await ControllerJobRunner.Run(this, job, shutdown);
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                }
                finally
                {
                    lock (_lock)
                        _controllerRunIds.Remove(job.RunId);
                    EndController(job.RunId, failure);
                }
            }));
        }
    }

    // Cockpit ends the run with this error if the controller did not end it itself.
    private void EndController(string runId, string? error)
    {
        lock (_lock)
            _endedControllers.Add(new EndedController { RunId = runId, Error = error });
        Wake();
    }

    private void StartLoad(ClaimRequest request, CancellationToken shutdown)
    {
        LoadJob job;
        lock (_lock)
        {
            // At most one load child at a time. Cockpit never claims a busy worker; an offer of the job this
            // worker runs or just finished is a repeat.
            if (_loadJob != null || _finishedLoadRunIds.Contains(request.RunId))
                return;

            job = new LoadJob(this, request);
            _loadJob = job;
        }

        Console.WriteLine($"Run {job.RunId}: load for {job.TestName} ({job.Owner}).");
        _ = Task.Run(async () =>
        {
            try
            {
                await job.Run(shutdown);
            }
            finally
            {
                lock (_lock)
                {
                    if (_loadJob == job)
                        _loadJob = null;
                    _finishedLoadRunIds.Add(job.RunId);
                    _doneLoadRunIds.Add(job.RunId);
                }

                Wake();
                Console.WriteLine($"Run {job.RunId}: load done; worker is free.");
                job.Cleanup();
            }
        });
    }

    // The run folder holds a load and a controller folder; whichever job ends last removes it.
    public static void DeleteIfEmpty(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        catch (IOException)
        {
        }
    }

    private static string CreateWorkerId()
    {
        var machine = InvalidIdCharacters().Replace(Environment.MachineName.ToLowerInvariant(), "-");
        return machine + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
    }

    [GeneratedRegex("[^a-z0-9_-]")]
    private static partial Regex InvalidIdCharacters();
}
