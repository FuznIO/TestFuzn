namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal static class DescriptionHelper
{
    public static string AddWarmupIfWarmup(bool isWarmup)
    {
        return isWarmup ? "(Warmup)" : string.Empty;
    }
}