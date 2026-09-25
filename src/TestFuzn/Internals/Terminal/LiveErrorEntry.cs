namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class LiveErrorEntry
{
    public string StepName { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public int Count { get; init; }

    public DateTime LastSeen { get; init; }
}
