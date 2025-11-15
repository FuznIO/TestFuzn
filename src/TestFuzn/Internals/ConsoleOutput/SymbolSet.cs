namespace Fuzn.TestFuzn.Internals.ConsoleOutput;

internal static class SymbolSet
{
    public static bool UseAscii { get; } = false;
    public static string Scenario => UseAscii ? "=== Scenario:" : "🧪";
    public static string Step => UseAscii ? ">> Step" : "⚙️ Step";
    public static string Metrics => UseAscii ? "[Metrics]" : "📊";
    public static string Sub => UseAscii ? "->" : "↳";
    public  static string Success => UseAscii ? "[OK]" : "✅";
    public static string Failure => UseAscii ? "[FAIL]" : "❌";
    public static string Skipped => UseAscii ? "[SKIP]" : "⏭️";
    public static string Warning => UseAscii ? "[WARN]" : "⚠️";

    internal static string MapStepStatus(StepStatus status)
    {
        switch (status)
        {
            case StepStatus.Passed:
                return Success;
            case StepStatus.Failed:
                return Failure;
            case StepStatus.Skipped:
                return Skipped;
            default:
                throw new NotImplementedException($"Step status '{status}' is not implemented.");
        }
    }
}
