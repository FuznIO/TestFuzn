namespace Fuzn.TestFuzn;

/// <summary>
/// Contains information about the test execution environment and context.
/// </summary>
public class ExecutionInfo
{
    /// <summary>
    /// Gets the target environment the tests are executing against (e.g., Dev, Test, Staging, Production).
    /// </summary>
    public string TargetEnvironment { get; internal set; }

    /// <summary>
    /// Gets the execution environment where tests are running (e.g., Local, CI, CloudAgent).
    /// </summary>
    public string ExecutionEnvironment { get; internal set; }

    /// <summary>
    /// Gets the name of the node executing the tests in distributed scenarios.
    /// </summary>
    public string NodeName { get; internal set; }

    /// <summary>
    /// Gets the unique identifier for the current test run.
    /// </summary>
    public string TestRunId { get; internal set; }

    /// <summary>
    /// Gets or sets the correlation ID for tracking across distributed systems.
    /// </summary>
    public string CorrelationId { get; set; }
}
