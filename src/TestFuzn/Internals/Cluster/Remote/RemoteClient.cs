using System.Reflection;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Logger;
using Fuzn.TestFuzn.Internals.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

// The laptop or pipeline side of --cluster: upload the build to Cockpit once, hand the run to the pool, then
// wait for its end and print the summary. Everything else about the run is in Cockpit.
internal sealed class RemoteClient
{
    private const string RunNameArgument = "run-name";
    private static readonly TimeSpan ControllerJobPoll = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan QueuePoll = TimeSpan.FromSeconds(5);

    private readonly TestExecutionState _state;
    private readonly IServiceProvider _serviceProvider;
    private readonly TestSession _session;
    private readonly ClusterOptions _options;
    private string _cockpitUrl = "";

    public RemoteClient(TestExecutionState state, IServiceProvider serviceProvider)
    {
        _state = state;
        _serviceProvider = serviceProvider;
        _session = state.TestSession;
        _options = ClusterOptions.Load(_session);
    }

    public async Task<bool> Run()
    {
        var cancellationToken = _state.CancellationToken;
        if (Cockpit.CockpitSettings.Load(_session.Configuration.AppConfiguration, _session.Cluster.Args).Source == null)
            throw new InvalidOperationException($"A cluster run needs a source, so Cockpit knows which runs to compare it with. Pass --{Cockpit.CockpitSettings.SourceArgument}=<name> or set {Cockpit.CockpitSettings.SourceVariable}, for example nightly-load.");
        using var cockpit = CockpitClientSettings.Connect(_session);
        _cockpitUrl = cockpit.Url;

        var compatible = CheckPool(await PoolWorkers(cockpit, cancellationToken));
        var builds = await UploadBuilds(cockpit, compatible, cancellationToken);

        var runId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var job = CreateJob(runId, builds);
        var liveUrl = await SubmitJob(cockpit, job, cancellationToken);

        Write($"Run [bold]{runId}[/] started on pool \"{_options.Pool}\" ({_options.Workers} worker{(_options.Workers == 1 ? "" : "s")}).");

        var attachment = new RunAttachment(cockpit, runId, _options.ConnectionTimeout, liveUrl);
        attachment.Attach(_state);

        var entry = await attachment.WaitForEnd(cancellationToken, CancellationToken.None, elapsed => Write($"Running… {RunAttachment.FormatElapsed(elapsed)}"));
        Complete(_state, _session, attachment, entry);
        return true;
    }

    private async Task<List<WorkerInfo>> PoolWorkers(CockpitConnection cockpit, CancellationToken cancellationToken)
    {
        var workers = await cockpit.Get<List<WorkerInfo>>(CockpitPaths.PoolWorkers(_options.Pool), cancellationToken);
        if (workers == null)
            return new List<WorkerInfo>();
        return workers;
    }

    private List<WorkerInfo> CheckPool(List<WorkerInfo> workers)
    {
        if (workers.Count == 0)
            throw new InvalidOperationException($"No worker in pool \"{_options.Pool}\" is connected to Cockpit at {_cockpitUrl}. Start one with: testfuzn-worker --pool {_options.Pool}");

        var compatible = workers.Where(worker => worker.Protocol == ClusterProtocol.Version).ToList();
        if (compatible.Count == 0)
            throw new InvalidOperationException($"The workers in pool \"{_options.Pool}\" use another protocol version than this TestFuzn version ({ClusterProtocol.Version}). Update the worker app.");

        if (compatible.Count < _options.Workers)
            throw new InvalidOperationException($"Pool \"{_options.Pool}\" has {compatible.Count} worker{(compatible.Count == 1 ? "" : "s")}; the run needs {_options.Workers}. Start more workers, or set TestFuzn:Cluster:Workers lower.");

        return compatible;
    }

    // One build per OS family in the pool (usually one); the controller may land on any worker of the pool.
    private async Task<Dictionary<string, string>> UploadBuilds(CockpitConnection cockpit, List<WorkerInfo> workers, CancellationToken cancellationToken)
    {
        var source = BuildDirectory();
        var builds = new Dictionary<string, string>();
        foreach (var osFamily in workers.Select(worker => worker.Os).Distinct())
        {
            var files = BuildPackager.Files(source, osFamily);
            var hash = BuildPackager.Hash(source, files);
            var size = files.Sum(file => new FileInfo(Path.Combine(source, file)).Length);
            Write($"Build for {osFamily} workers: {files.Count} files, {size / (1024.0 * 1024.0):0.#} MB, hash {hash.Substring(0, 12)}.");

            if (await BuildPackager.UploadIfMissing(cockpit, source, files, hash, cancellationToken))
                Write("Uploaded the build to Cockpit.");
            else
                Write("Cockpit already has this build; nothing to upload.");

            builds[osFamily] = hash;
        }

        return builds;
    }

    private string BuildDirectory()
    {
        var ownDirectory = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(_options.PackagePath))
            return ownDirectory;

        var packagePath = Path.GetFullPath(_options.PackagePath);
        if (!Directory.Exists(packagePath))
            throw new InvalidOperationException($"TestFuzn:Cluster:PackagePath '{packagePath}' does not exist.");

        var entryAssembly = EntryAssemblyFileName();
        var packaged = Path.Combine(packagePath, entryAssembly);
        if (!File.Exists(packaged))
            throw new InvalidOperationException($"TestFuzn:Cluster:PackagePath '{packagePath}' has no {entryAssembly}.");

        if (File.GetLastWriteTimeUtc(packaged) < File.GetLastWriteTimeUtc(Path.Combine(ownDirectory, entryAssembly)))
            Write("[yellow]PackagePath is older than your build. Did you forget to publish?[/]");

        return packagePath;
    }

    private static string EntryAssemblyFileName()
    {
        var entry = Assembly.GetEntryAssembly();
        if (entry == null)
            throw new InvalidOperationException("No entry assembly to upload.");
        return Path.GetFileName(entry.Location);
    }

    private ControllerJob CreateJob(string runId, Dictionary<string, string> builds)
    {
        var now = DateTime.UtcNow;
        var testName = _state.TestClassInstance.TestInfo.FullName;
        var entry = new RunEntry
        {
            RunId = runId,
            Name = _session.Cluster.Args.TryGetValue(RunNameArgument, out var name) && name != ArgumentsParser.FlagValue ? name : null,
            TestName = testName,
            StartedBy = Environment.UserName,
            FromMachine = Environment.MachineName,
            Pool = _options.Pool,
            Workers = _options.Workers,
            TargetEnvironment = _session.Configuration.TargetEnvironment,
            StartedUtc = now,
            ConnectionTimeoutSeconds = (int) _options.ConnectionTimeout.TotalSeconds
        };

        var cockpit = Cockpit.CockpitSettings.Load(_session.Configuration.AppConfiguration, _session.Cluster.Args);

        return new ControllerJob
        {
            RunId = runId,
            TestName = testName,
            EntryAssembly = EntryAssemblyFileName(),
            Builds = builds,
            Pool = _options.Pool,
            Workers = _options.Workers,
            ConnectionTimeoutSeconds = entry.ConnectionTimeoutSeconds,
            TargetEnvironment = _session.Configuration.TargetEnvironment,
            Seed = Random.Shared.Next(),
            Entry = entry,
            Suite = _session.Configuration.Suite.Name,
            Source = cockpit.Source,
            Branch = cockpit.Branch,
            Commit = cockpit.Commit,
            BuildNumber = cockpit.BuildNumber,
            LogUrl = cockpit.LogUrl
        };
    }

    // The run waits in the pool's queue until enough workers are free; Cockpit then gives it to a worker of the
    // pool, which starts the controller. Ctrl+C takes it out of the queue.
    private async Task<string?> SubmitJob(CockpitConnection cockpit, ControllerJob job, CancellationToken cancellationToken)
    {
        var reply = await cockpit.Post<SubmitRunReply>(CockpitPaths.Runs, job, cancellationToken);
        string? liveUrl = null;
        if (reply != null)
            liveUrl = reply.LiveUrl;
        if (liveUrl != null)
            Write($"Live: {liveUrl}");
        if (LivePageOpener.ShouldOpen(_options.OpenBrowser, liveUrl, new EnvironmentWrapper()))
        {
            var error = LivePageOpener.TryOpen(liveUrl!);
            Write(error == null ? "Opened the live page in your browser." : $"[yellow]Could not open the browser: {Fuzn.TestFuzn.Internals.Terminal.MarkupParser.Escape(error)}[/]");
        }

        int? shownPosition = null;
        try
        {
            while (true)
            {
                var entry = await cockpit.Get<RunEntry>(CockpitPaths.Run(job.RunId), cancellationToken);
                if (entry != null && entry.IsEnded)
                    throw new InvalidOperationException(entry.Error ?? $"Run {job.RunId} ended before it started ({entry.Result}).");
                if (entry != null && entry.ControllerWorkerId != null)
                    return liveUrl;

                var position = entry?.QueuePosition;
                if (position != null && position != shownPosition)
                {
                    Write($"Queued: {QueuePlace(position.Value)} in line for {_options.Workers} worker{(_options.Workers == 1 ? "" : "s")} of pool \"{_options.Pool}\".");
                    shownPosition = position;
                }

                await Task.Delay(position > 1 ? QueuePoll : ControllerJobPoll, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            await cockpit.Post<RunControlReply>(CockpitPaths.Stop(job.RunId), new RunControlRequest { RequestedBy = Environment.UserName + "@" + Environment.MachineName }, CancellationToken.None);
            throw;
        }
    }

    private static string QueuePlace(int position)
    {
        if (position == 1)
            return "next";
        var suffix = (position % 100) is 11 or 12 or 13 ? "th" : (position % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return position + suffix;
    }

    internal static void Complete(TestExecutionState state, TestSession session, RunAttachment attachment, RunEntry entry)
    {
        if (attachment.Latest != null)
            RunAttachment.Apply(state, attachment.Latest, DateTime.UtcNow);
        session.Cluster.RemoteRunUrl = entry.RunUrl;

        var now = DateTime.UtcNow;
        state.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Init, now);
        state.TestResult.MarkPhaseAsStarted(StandardTestPhase.Execute, now);
        state.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Execute, now);
        state.TestResult.MarkPhaseAsStarted(StandardTestPhase.Cleanup, now);
        state.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Cleanup, now);

        if (entry.Result == RunResults.Passed)
        {
            state.TestResult.Status = TestStatus.Passed;
            return;
        }

        state.TestResult.Status = TestStatus.Failed;

        if (entry.Result == RunResults.Stopped && state.CancellationToken.IsCancellationRequested)
            return;

        var message = entry.Error;
        if (message == null)
            message = entry.Result == RunResults.Stopped ? $"Run {entry.RunId} was stopped." : $"Run {entry.RunId} failed.";
        if (state.FirstException == null)
            state.FirstException = new RemoteRunException(message);
    }

    private void Write(string markup)
    {
        _state.TestFramework.WriteMarkup(markup);
    }
}
