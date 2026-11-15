using System.Text.Json;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;

namespace Fuzn.TestFuzn.Worker;

// One claimed run: download the build, run the load child, give it the controller's commands on stdin at
// the moment Cockpit set, and report its status.json with every poll until the controller releases the worker.
internal sealed class LoadJob
{
    private static readonly TimeSpan WatchInterval = TimeSpan.FromMilliseconds(250);

    private readonly WorkerHost _host;
    private readonly ClaimRequest _request;
    private readonly string _jobDirectory;
    private readonly TimeSpan _connectionTimeout;
    private readonly object _lock = new();
    private readonly HashSet<string> _sentCommands = new();
    private ChildProcess? _child;
    private DateTime _lastHeardUtc = DateTime.UtcNow;
    private DateTime? _stopSentUtc;
    private string? _scheduledCommand;
    private DateTime _scheduledUtc;
    private bool _isStopRequested;
    private bool _isReleased;
    private string _phase = WorkerJobPhases.Downloading;
    private string? _error;

    public LoadJob(WorkerHost host, ClaimRequest request)
    {
        _host = host;
        _request = request;
        _jobDirectory = Path.Combine(host.Options.WorkDirectory, "runs", request.RunId, "load");
        _connectionTimeout = request.ConnectionTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(request.ConnectionTimeoutSeconds)
            : ClusterProtocol.DefaultConnectionTimeout;
        StartedUtc = DateTime.UtcNow;
    }

    public string RunId => _request.RunId;
    public string Owner => _request.Owner;
    public string TestName => _request.TestName;
    public DateTime StartedUtc { get; }

    public async Task Run(CancellationToken shutdown)
    {
        using var jobEnd = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var watchLoop = Task.Run(() => Watch(jobEnd.Token));

        try
        {
            await RunChild(shutdown);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            lock (_lock)
                _error = ex.Message;
        }

        lock (_lock)
            _phase = WorkerJobPhases.Exited;
        _host.Wake();

        // A child that finished during a connection gap leaves its final status; keep reporting it until the
        // controller has it (released) or the controller is gone.
        while (!shutdown.IsCancellationRequested)
        {
            lock (_lock)
            {
                if (_isReleased || DateTime.UtcNow - _lastHeardUtc > _connectionTimeout)
                    break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken.None);
        }

        jobEnd.Cancel();
        try
        {
            await watchLoop;
        }
        catch (OperationCanceledException)
        {
        }
    }

    // After Run: the worker is already free again, so the next run does not wait for the folder to go.
    public void Cleanup()
    {
        var child = _child;
        if (child != null)
            child.Dispose();
        DeleteJobDirectory();
    }

    private async Task RunChild(CancellationToken shutdown)
    {
        if (_request.Protocol != ClusterProtocol.Version)
            throw new InvalidOperationException($"Worker {_host.WorkerId} uses protocol {ClusterProtocol.Version}, the run uses {_request.Protocol}. Update the worker app.");

        var buildHash = WorkerHost.BuildFor(_request.Builds);
        var packageDirectory = Path.Combine(_jobDirectory, "package");
        await _host.Builds.Unpack(buildHash, packageDirectory, shutdown);

        lock (_lock)
        {
            if (_isStopRequested)
            {
                _error = "The run was stopped before the load child started.";
                return;
            }

            _phase = WorkerJobPhases.Starting;
        }

        var runFilePath = Path.Combine(_jobDirectory, ClusterProtocol.RunFileName);
        await File.WriteAllTextAsync(runFilePath, _request.RunFile.GetRawText(), shutdown);

        var resultsDirectory = Path.Combine(_host.Options.WorkDirectory, "results", "load");
        var outputLog = Path.Combine(resultsDirectory, Path.GetFileNameWithoutExtension(_request.EntryAssembly), RunId, ClusterProtocol.OutputLogFileName);
        var environment = new Dictionary<string, string> { ["TESTFUZN_RESULTS_DIRECTORY"] = resultsDirectory };

        var child = ChildProcess.Start(packageDirectory, _request.EntryAssembly, "--cluster-run=" + runFilePath, outputLog, environment);
        lock (_lock)
        {
            _child = child;
            _phase = WorkerJobPhases.Running;
            if (_isStopRequested)
            {
                child.Send(RunCommands.Stop);
                _stopSentUtc = DateTime.UtcNow;
            }
        }

        await child.WaitForExit(shutdown).ContinueWith(_ => { }, CancellationToken.None);
        if (shutdown.IsCancellationRequested)
            await StopChild(child);
    }

    // The controller's wish, from the host's poll. A start or start-measurement waits for the moment Cockpit
    // set, so every worker of the run starts at once; a stop goes at once.
    public void Apply(LoadJobCommand command)
    {
        lock (_lock)
        {
            _lastHeardUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(command.ControllerQuietMs);
            if (command.Released)
                _isReleased = true;

            if (command.Command == null)
                return;

            if (command.Command == RunCommands.Stop)
            {
                _isStopRequested = true;
                if (_child != null && _stopSentUtc == null)
                {
                    _child.Send(RunCommands.Stop);
                    _stopSentUtc = DateTime.UtcNow;
                }

                return;
            }

            if (_scheduledCommand != command.Command && !_sentCommands.Contains(command.Command))
            {
                _scheduledCommand = command.Command;
                _scheduledUtc = DateTime.UtcNow + TimeSpan.FromMilliseconds(Math.Max(0, command.InMs));
            }
        }
    }

    public WorkerStatusMessage Status()
    {
        var message = new WorkerStatusMessage { WorkerId = _host.WorkerId, RunId = RunId };
        ChildProcess? child;
        lock (_lock)
        {
            message.Phase = _phase;
            message.Error = _error;
            child = _child;
            message.ChildAlive = child != null && !child.HasExited;
            if (child != null)
                message.ExitCode = child.ExitCode;
        }

        message.Status = ReadStatusFile();
        if (child != null && child.HasExited && !HasFinalState(message.Status))
            message.OutputTail = child.Tail();

        return message;
    }

    // Sends due commands, guards against a silent controller, and wakes the host when the child's state
    // changes, so the new status goes out at once.
    private async Task Watch(CancellationToken cancellationToken)
    {
        string? lastState = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            Watchdog();
            SendDueCommand();

            var state = ReadState();
            if (state != lastState)
            {
                lastState = state;
                _host.Wake();
            }

            await Task.Delay(WatchInterval, cancellationToken);
        }
    }

    private void SendDueCommand()
    {
        lock (_lock)
        {
            if (_scheduledCommand == null || _child == null || _isStopRequested || DateTime.UtcNow < _scheduledUtc)
                return;

            _child.Send(_scheduledCommand);
            _sentCommands.Add(_scheduledCommand);
            _scheduledCommand = null;
        }
    }

    private string? ReadState()
    {
        string phase;
        lock (_lock)
            phase = _phase;

        var status = ReadStatusFile();
        if (status != null && status.Value.TryGetProperty("state", out var state))
            return phase + "/" + state.GetString();

        return phase;
    }

    // A controller that went quiet never blocks this worker: Cockpit says how long it has been silent, and a
    // worker that cannot reach Cockpit at all stops hearing from it too.
    private void Watchdog()
    {
        lock (_lock)
        {
            if (_child == null || _child.HasExited)
                return;

            if (_stopSentUtc == null && _isStopRequested)
            {
                _child.Send(RunCommands.Stop);
                _stopSentUtc = DateTime.UtcNow;
            }
            else if (_stopSentUtc == null && DateTime.UtcNow - _lastHeardUtc > _connectionTimeout)
            {
                Console.WriteLine($"Run {RunId}: nothing from the controller for {_connectionTimeout.TotalSeconds:0} s; stopping the load child.");
                _child.Send(RunCommands.Stop);
                _stopSentUtc = DateTime.UtcNow;
            }
            else if (_stopSentUtc != null && DateTime.UtcNow - _stopSentUtc > ClusterProtocol.ChildKillGrace)
            {
                _child.Kill();
            }
        }
    }

    private JsonElement? ReadStatusFile()
    {
        var path = Path.Combine(_jobDirectory, "status.json");
        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool HasFinalState(JsonElement? status)
    {
        if (status == null || !status.Value.TryGetProperty("state", out var state))
            return false;

        var value = state.GetString();
        return value == "Completed" || value == "Failed" || value == "Stopped";
    }

    public async Task Shutdown()
    {
        ChildProcess? child;
        lock (_lock)
            child = _child;

        if (child != null)
            await StopChild(child);
    }

    private static async Task StopChild(ChildProcess child)
    {
        child.Send(RunCommands.Stop);
        using var grace = new CancellationTokenSource(ClusterProtocol.ChildKillGrace);
        try
        {
            await child.WaitForExit(grace.Token);
        }
        catch (OperationCanceledException)
        {
            child.Kill();
        }
    }

    private void DeleteJobDirectory()
    {
        // run.json can carry secrets from BeforeScenarioOnce; it goes with the package folder.
        try
        {
            if (Directory.Exists(_jobDirectory))
                Directory.Delete(_jobDirectory, recursive: true);

            WorkerHost.DeleteIfEmpty(Path.GetDirectoryName(_jobDirectory)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Run {RunId}: could not delete {_jobDirectory}: {ex.Message}");
        }
    }
}
