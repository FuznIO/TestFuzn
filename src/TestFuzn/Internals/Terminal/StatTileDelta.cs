namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The change a <see cref="StatTile"/> shows beside its label. The signed <see cref="Change"/>
/// gives the arrow — ▲ at zero or above, ▼ below — and, with <see cref="UpIsGood"/>, the
/// colour: a change in the good direction renders in <see cref="TerminalPalette.OkStyle"/>, one
/// in the bad direction in <see cref="TerminalPalette.FailedStyle"/>, and zero unstyled.
/// <see cref="Text"/> is written after the arrow as the caller formatted it ("12.5 %"), escaped
/// and sanitized by the widget like any user text; empty leaves the arrow alone. A NaN change
/// has no direction and draws nothing at all; ±Infinity draws its arrow.
/// </summary>
internal readonly struct StatTileDelta
{
    /// <summary>The signed change; only its sign is drawn, the magnitude is <see cref="Text"/>.</summary>
    public double Change { get; }

    /// <summary>The formatted change written after the arrow, e.g. "12.5 %".</summary>
    public string Text { get; }

    /// <summary>Whether an increase is the good direction — requests: yes; latency and errors: no.</summary>
    public bool UpIsGood { get; }

    public StatTileDelta(double change, string text, bool upIsGood)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");

        Change = change;
        Text = text;
        UpIsGood = upIsGood;
    }
}
