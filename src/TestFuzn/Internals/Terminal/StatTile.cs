namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One stat tile for <see cref="StatTileWidget"/> and <see cref="TileRowWidget"/>: a label, the
/// big value as the caller formatted it (never reformatted, never truncated), its unit, the
/// <see cref="State"/> that colours the value, and the optional parts — a <see cref="Delta"/>
/// beside the label, a <see cref="Gauge"/> under the value and a <see cref="Trend"/> sparkline
/// at the bottom. The texts are user text to the widgets: escaped, control-sanitized and
/// measured, never parsed as markup, so brackets render literally. The trend is read once per
/// render and never held; a value that is not finite is a gap. Immutable once constructed.
/// </summary>
internal sealed class StatTile
{
    /// <summary>The tile's name, e.g. "requests" or "p95"; rendered dim above the value.</summary>
    public string Label { get; }

    /// <summary>The formatted big value, e.g. "12 345" or "412".</summary>
    public string Value { get; }

    /// <summary>The value's unit, e.g. "ms"; rendered dim after the value, nothing when empty.</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>The change shown beside the label, or null for none.</summary>
    public StatTileDelta? Delta { get; init; }

    /// <summary>The gauge shown under the value, or null for none.</summary>
    public StatTileGauge? Gauge { get; init; }

    /// <summary>The samples of the trend sparkline, oldest first and newest last, or null for none; empty draws a blank trend row.</summary>
    public IReadOnlyList<double>? Trend { get; init; }

    /// <summary>How the value reads; Neutral unless set.</summary>
    public StatTileState State { get; init; }

    public StatTile(string label, string value)
    {
        if (label == null)
            throw new ArgumentNullException(nameof(label), "Label cannot be null.");
        if (value == null)
            throw new ArgumentNullException(nameof(value), "Value cannot be null.");

        Label = label;
        Value = value;
    }
}
