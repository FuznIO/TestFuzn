namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One distinct error for the dashboard's error ticker: a step name + exception message pair
/// with its cumulative occurrence count and the time its count last increased. Counts are
/// copied out of the collector's shared <see cref="Fuzn.TestFuzn.Contracts.Results.Load.ErrorEntry"/>
/// instances at ingest (those keep being incremented on other threads), so a published entry
/// never changes. When the same step name + message pair appears in more than one step of the
/// tree (same-named sub-steps under different parents), their counts are summed into one entry.
/// </summary>
internal sealed class LiveErrorEntry
{
    /// <summary>The name of the step the error occurred in.</summary>
    public string StepName { get; init; } = string.Empty;

    /// <summary>The exception message, as the collector keyed it.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>How many times this error has occurred so far.</summary>
    public int Count { get; init; }

    /// <summary>The ingest time at which <see cref="Count"/> last increased (first seen counts).</summary>
    public DateTime LastSeen { get; init; }
}
