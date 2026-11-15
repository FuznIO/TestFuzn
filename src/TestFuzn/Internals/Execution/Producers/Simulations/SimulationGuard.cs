namespace Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

internal static class SimulationGuard
{
    public static void NotNegative(int value, string name)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be 0 or more.");
    }

    public static void NotNegative(TimeSpan value, string name)
    {
        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be 0 or more.");
    }

    public static void Positive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be more than 0.");
    }

    public static void NotAbove(int value, int maximum, string name, string maximumName)
    {
        if (value > maximum)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must not be more than {maximumName} ({maximum}).");
    }
}
