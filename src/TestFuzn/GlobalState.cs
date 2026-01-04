using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Providers;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides global state and configuration for the TestFuzn framework.
/// </summary>
public static class GlobalState
{
    internal static bool IsInitializeGlobalExecuted { get; set; } = false;
    internal static string TestsOutputDirectory { get; set; }
    internal static TestFuznConfiguration Configuration { get; set; }
    internal static bool LoadTestWasExecuted { get; set; } = false;
    internal static ILogger Logger { get; set; }
    internal static string AssemblyWithTestsName { get; set; }
    internal static string TestRunId { get; set; }
    internal static DateTime TestRunStartTime { get; set; }
    internal static DateTime TestRunEndTime { get; set; }
    internal static TimeSpan SinkWriteFrequency { get; set; } = TimeSpan.FromSeconds(3);
    internal static string NodeName { get; set; }
    internal static ISerializerProvider SerializerProvider => Configuration.SerializerProvider;
    internal static LoggingVerbosity LoggingVerbosity => Configuration.LoggingVerbosity;
    
    /// <summary>
    /// Gets or sets the target environment the tests are executing against (e.g., Dev, Test, Staging, Production).
    /// Set via TESTFUZN_TARGET_ENVIRONMENT environment variable or --target-environment argument.
    /// </summary>
    public static string TargetEnvironment { get; internal set; }
    
    /// <summary>
    /// Gets or sets the execution environment where tests are running (e.g., Local, CI, CloudAgent).
    /// Set via TESTFUZN_EXECUTION_ENVIRONMENT environment variable or --execution-environment argument.
    /// Used for configuration loading, not for test filtering.
    /// </summary>
    public static string ExecutionEnvironment { get; internal set; }
    
    /// <summary>
    /// Gets or sets the list of tags to include when filtering tests.
    /// Only tests with at least one of these tags will be executed.
    /// </summary>
    public static List<string> TagsFilterInclude { get; internal set; } = new();

    /// <summary>
    /// Gets or sets the list of tags to exclude when filtering tests.
    /// Tests with any of these tags will be skipped.
    /// </summary>
    public static List<string> TagsFilterExclude { get; internal set; } = new();
    
    internal static void EnsureInitialized(ITestFrameworkAdapter testFramework)
    {
        if (testFramework == null)
            throw new ArgumentNullException(nameof(testFramework), "Test framework adapter cannot be null.");

        if (!IsInitializeGlobalExecuted)
            testFramework.ThrowTestFuznIsNotInitializedException();
    }
}