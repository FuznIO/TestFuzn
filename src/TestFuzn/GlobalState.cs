using Fuzn.TestFuzn.Contracts.Providers;

namespace Fuzn.TestFuzn;

public static class GlobalState
{
    internal static bool IsInitializeGlobalExecuted { get; set; } = false;
    internal static string TestsOutputDirectory { get; set; }
    internal static TestFuznConfiguration Configuration { get; set; }
    internal static bool LoadTestWasExecuted { get; set; } = false;
    internal static ILogger Logger { get; set; }
    internal static bool CustomTestRunner { get; set; } = false;
    internal static string AssemblyWithTestsName { get; set; }
    internal static string TestRunId { get; set; }
    internal static DateTime TestRunStartTime { get; set; }
    internal static DateTime TestRunEndTime { get; set; }
    internal static TimeSpan SinkWriteFrequency { get; set; } = TimeSpan.FromSeconds(3);
    internal static string NodeName { get; set; } = Environment.MachineName;
    public static ISerializerProvider SerializerProvider => Configuration.SerializerProvider;
    public static string EnvironmentName { get; set; }

    internal static void Init()
    {
        TestRunStartTime = DateTime.UtcNow;
        DateTimeOffset test = DateTimeOffset.UtcNow;

        TestRunId = $"{DateTime.Now:yyyy-MM-dd_HH-mm}__{Guid.NewGuid().ToString("N").Substring(0, 6)}";
    
        EnvironmentName = Environment.GetEnvironmentVariable("TESTFUZN_ENVIRONMENT") ?? "";
    }
}