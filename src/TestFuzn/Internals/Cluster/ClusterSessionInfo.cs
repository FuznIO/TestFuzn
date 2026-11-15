using Fuzn.TestFuzn.Internals.Cluster.LoadChild;

namespace Fuzn.TestFuzn.Internals.Cluster;

internal enum ClusterRole
{
    None,
    Controller,
    LoadChild
}

internal class ClusterSessionInfo
{
    internal const string ClusterArgument = "cluster";
    internal const string ClusterVariable = "TESTFUZN_CLUSTER";

    public ClusterRole Role { get; set; } = ClusterRole.None;

    public bool RunLoadTestsOnCluster { get; set; }

    private int _remoteRuns;

    public bool TryStartRemoteRun() => Interlocked.Exchange(ref _remoteRuns, 1) == 0;

    public string? RemoteRunUrl { get; set; }

    // The controller, a TestFuzn runner remote client and the remote commands run no suite or class
    // hooks and no context plugins: those belong to the machines that run the iterations.
    public bool SkipSuiteHooks { get; set; }

    // Set for a controller or load child; becomes the session's test run id.
    public string? RunId { get; set; }

    public int WorkerIndex { get; set; }
    public int WorkerCount { get; set; } = 1;
    public int Seed { get; set; }

    // Scenario name to the JSON its BeforeScenarioOnce returned.
    public Dictionary<string, string> OnceData { get; } = new();

    public LoadChildCoordinator? LoadChild { get; set; }

    // Parsed command-line arguments, for the remote client's own options (--run-name, ...).
    public Dictionary<string, string> Args { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool SinksEnabled => Role != ClusterRole.LoadChild && !RunLoadTestsOnCluster;

    public bool ReportsEnabled => Role != ClusterRole.LoadChild;
}
