namespace Fuzn.TestFuzn;

/// <summary>
/// Provides global state and configuration for the TestFuzn framework.
/// </summary>
public static class GlobalState
{
    /// <summary>
    /// Gets the target environment the tests are executing against
    /// Set via TESTFUZN_TARGET_ENVIRONMENT environment variable or --target-environment argument.
    /// </summary>
    public static string TargetEnvironment => GetTestSession().Configuration.TargetEnvironment;

    /// <summary>
    /// Gets the execution environment where tests are running
    /// Set via TESTFUZN_EXECUTION_ENVIRONMENT environment variable or --execution-environment argument.
    /// Used for configuration loading, not for test filtering.
    /// </summary>
    public static string ExecutionEnvironment => GetTestSession().Configuration.ExecutionEnvironment;

    /// <summary>
    /// Gets the list of tags to include when filtering tests.
    /// Only tests with at least one of these tags will be executed.
    /// </summary>
    public static List<string> TagsFilterInclude => GetTestSession().Configuration.TagsFilterInclude;

    /// <summary>
    /// Gets the list of tags to exclude when filtering tests.
    /// Tests with any of these tags will be skipped.
    /// </summary>
    public static List<string> TagsFilterExclude => GetTestSession().Configuration.TagsFilterExclude;

    /// <summary>
    /// Gets the configuration manager that provides access to application configuration settings.
    /// </summary>
    public static AppConfigurationManager AppConfiguration => GetTestSession().Configuration.AppConfiguration;

    /// <summary>
    /// Gets the name of the node (machine) where the tests are running.
    /// </summary>
    public static string NodeName => GetTestSession().NodeName;

    /// <summary>
    /// Gets the service provider for resolving dependencies registered during test initialization.
    /// </summary>
    public static IServiceProvider ServiceProvider => GetTestSession().ServiceProvider;

    /// <summary>
    /// Gets the logger instance used for writing test log output.
    /// </summary>
    public static ILogger Logger => GetTestSession().Logger;

    /// <summary>
    /// Gets the directory where test results and artifacts are written.
    /// </summary>
    public static string TestsResultsDirectory => GetTestSession().TestsResultsDirectory;

    /// <summary>
    /// Gets the unique identifier for the current test run.
    /// </summary>
    public static string TestRunId => GetTestSession().TestRunId;

    private static TestSession GetTestSession() =>
        TestSession.Current ?? throw new InvalidOperationException("TestSession has not been initialized. Ensure TestFuzn is initialized before accessing GlobalState.");
}