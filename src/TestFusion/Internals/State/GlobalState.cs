
namespace TestFusion.Internals.State;

internal static class GlobalState
{
    public static bool IsInitializeGlobalExecuted { get; set; } = false;
    public static string TestsOutputDirectory { get; set; }
    public static TestFusionConfiguration Configuration { get; set; }
    public static bool LoadTestWasExecuted { get; set; } = false;
    public static ILogger Logger { get; set; }
    public static bool CustomTestRunner { get; set; } = false;
    public static string AssemblyWithTestsName { get; set; }
    public static string TestRunId { get; set; }
    public static DateTime TestRunStartTime { get; set; }
    public static DateTime TestRunEndTime { get; set; }
    public static TimeSpan SinkWriteFrequency { get; set; } = TimeSpan.FromSeconds(3);
    public static string NodeName { get; set; } = Environment.MachineName;

    internal static void Init()
    {
        TestRunStartTime = DateTime.UtcNow;
        DateTimeOffset test = DateTimeOffset.UtcNow;

        TestRunId = $"{DateTime.Now:yyyy-MM-dd_HH-mm}__{Guid.NewGuid().ToString("N").Substring(0, 6)}";
    }
}