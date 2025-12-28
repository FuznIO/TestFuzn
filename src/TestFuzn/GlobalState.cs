using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Providers;

namespace Fuzn.TestFuzn;

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
    public static string EnvironmentName { get; set; }
    public static List<string> TagsFilterInclude { get; set; } = new();
    public static List<string> TagsFilterExclude { get; set; } = new();
    
    internal static void EnsureInitialized(ITestFrameworkAdapter testFramework)
    {
        if (testFramework == null)
            throw new ArgumentNullException(nameof(testFramework), "Test framework adapter cannot be null.");

        if (!IsInitializeGlobalExecuted)
            testFramework.ThrowTestFuznIsNotInitializedException();
    }
}