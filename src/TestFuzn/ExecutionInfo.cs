namespace Fuzn.TestFuzn;

/// <summary>
/// Contains information about the test execution environment and context.
/// </summary>
public class ExecutionInfo
{
    internal TestSession TestSession { get; set; }

    /// <summary>
    /// Gets the target environment the tests are executing against (e.g., Dev, Test, Staging, Production).
    /// </summary>
    public string TargetEnvironment => TestSession.Configuration?.TargetEnvironment;

    /// <summary>
    /// Gets the execution environment where tests are running (e.g., Local, CI, CloudAgent).
    /// </summary>
    public string ExecutionEnvironment => TestSession.Configuration?.ExecutionEnvironment;

    /// <summary>
    /// Gets the name of the node executing the tests in distributed scenarios.
    /// </summary>
    public string NodeName => TestSession.NodeName;

    /// <summary>
    /// Gets the unique identifier for the current test run.
    /// </summary>
    public string TestRunId => TestSession.TestRunId;

    /// <summary>
    /// Gets the zero-based index of the load worker running this iteration. Always 0 when the test runs on one machine.
    /// </summary>
    public int WorkerIndex => TestSession.Cluster.WorkerIndex;

    /// <summary>
    /// Gets the number of load workers sharing the run. Always 1 when the test runs on one machine.
    /// </summary>
    public int WorkerCount => TestSession.Cluster.WorkerCount;

    /// <summary>
    /// Gets or sets the correlation ID for tracking across distributed systems.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets the cancellation token for the current test execution. Use this to cooperatively cancel
    /// long-running operations (e.g. HTTP requests) when the test is stopped.
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}
