using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides global state and configuration for the TestFuzn framework.
/// </summary>
public static class GlobalState
{
    internal static bool IsInitializeGlobalExecuted
    {
        get => TestSession.Current.IsInitializeGlobalExecuted;
        set => TestSession.Current.IsInitializeGlobalExecuted = value;
    }

    internal static string TestsOutputDirectory
    {
        get => TestSession.Current.TestsOutputDirectory;
        set => TestSession.Current.TestsOutputDirectory = value;
    }

    internal static TestFuznConfiguration Configuration
    {
        get => TestSession.Current.Configuration;
        set => TestSession.Current.Configuration = value;
    }

    internal static bool LoadTestWasExecuted
    {
        get => TestSession.Current.LoadTestWasExecuted;
        set => TestSession.Current.LoadTestWasExecuted = value;
    }

    internal static ILogger Logger
    {
        get => TestSession.Current.Logger;
        set => TestSession.Current.Logger = value;
    }

    internal static string AssemblyWithTestsName
    {
        get => TestSession.Current.AssemblyWithTestsName;
        set => TestSession.Current.AssemblyWithTestsName = value;
    }

    internal static string TestRunId
    {
        get => TestSession.Current.TestRunId;
        set => TestSession.Current.TestRunId = value;
    }

    internal static DateTime TestRunStartTime
    {
        get => TestSession.Current.TestRunStartTime;
        set => TestSession.Current.TestRunStartTime = value;
    }

    internal static DateTime TestRunEndTime
    {
        get => TestSession.Current.TestRunEndTime;
        set => TestSession.Current.TestRunEndTime = value;
    }

    internal static TimeSpan SinkWriteFrequency
    {
        get => TestSession.Current.SinkWriteFrequency;
        set => TestSession.Current.SinkWriteFrequency = value;
    }

    internal static string NodeName
    {
        get => TestSession.Current.NodeName;
        set => TestSession.Current.NodeName = value;
    }

    internal static LoggingVerbosity LoggingVerbosity => TestSession.Current.LoggingVerbosity;

    /// <summary>
    /// Gets or sets the target environment the tests are executing against (e.g., Dev, Test, Staging, Production).
    /// Set via TESTFUZN_TARGET_ENVIRONMENT environment variable or --target-environment argument.
    /// </summary>
    public static string TargetEnvironment
    {
        get => TestSession.Current.TargetEnvironment;
        internal set => TestSession.Current.TargetEnvironment = value;
    }

    /// <summary>
    /// Gets or sets the execution environment where tests are running (e.g., Local, CI, CloudAgent).
    /// Set via TESTFUZN_EXECUTION_ENVIRONMENT environment variable or --execution-environment argument.
    /// Used for configuration loading, not for test filtering.
    /// </summary>
    public static string ExecutionEnvironment
    {
        get => TestSession.Current.ExecutionEnvironment;
        internal set => TestSession.Current.ExecutionEnvironment = value;
    }

    /// <summary>
    /// Gets or sets the list of tags to include when filtering tests.
    /// Only tests with at least one of these tags will be executed.
    /// </summary>
    public static List<string> TagsFilterInclude
    {
        get => TestSession.Current.TagsFilterInclude;
        internal set => TestSession.Current.TagsFilterInclude = value;
    }

    /// <summary>
    /// Gets or sets the list of tags to exclude when filtering tests.
    /// Tests with any of these tags will be skipped.
    /// </summary>
    public static List<string> TagsFilterExclude
    {
        get => TestSession.Current.TagsFilterExclude;
        internal set => TestSession.Current.TagsFilterExclude = value;
    }

    internal static void EnsureInitialized(ITestFrameworkAdapter testFramework)
    {
        TestSession.Current.EnsureInitialized(testFramework);
    }
}