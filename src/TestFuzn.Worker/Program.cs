using System.Runtime.InteropServices;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;
using Fuzn.TestFuzn.Worker;

const string Usage = """
    Usage: testfuzn-worker --pool <name> [--work-dir <path>]
    Set TESTFUZN_COCKPIT_URL to Cockpit's address and TESTFUZN_WORKER_KEY to the worker key set in Cockpit.
    """;
const string SettingsHint = "Check TESTFUZN_COCKPIT_URL and TESTFUZN_WORKER_KEY.";

WorkerOptions options;
try
{
    options = WorkerOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(Usage);
    return 1;
}

var url = Environment.GetEnvironmentVariable(CockpitConnection.UrlVariable);
var workerKey = Environment.GetEnvironmentVariable(CockpitConnection.WorkerKeyVariable);
if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(workerKey))
{
    Console.Error.WriteLine("The worker reaches the cluster through Cockpit and needs its address and the worker key.");
    Console.Error.WriteLine(Usage);
    return 1;
}

if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
{
    Console.Error.WriteLine($"TESTFUZN_COCKPIT_URL '{url}' is not a URL.");
    return 1;
}

using var shutdown = new CancellationTokenSource();
void RequestShutdown(PosixSignalContext context)
{
    context.Cancel = true;
    shutdown.Cancel();
}

// SIGTERM comes from e.g. a pod deletion; children get "stop" and a grace period before they are killed.
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, RequestShutdown);
using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, RequestShutdown);

try
{
    using var cockpit = new CockpitConnection(url.Trim(), CockpitConnection.WorkerKeyHeader, workerKey.Trim(), SettingsHint);
    var host = new WorkerHost(options, cockpit);
    await host.Join(shutdown.Token);

    Console.WriteLine($"testfuzn-worker {WorkerHost.AppVersion}: worker {host.WorkerId} joined pool '{options.Pool}' through Cockpit at {cockpit.Url}.");
    Console.WriteLine($"Work folder: {options.WorkDirectory}");

    await host.Run(shutdown.Token);
    return 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
