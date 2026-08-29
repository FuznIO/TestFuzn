namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// How a <see cref="StatTile"/>'s value reads against whatever judges it — a threshold, an
/// error budget — and so which palette colour its value, its gauge fill and its trend take
/// (<see cref="TerminalPalette.StateStyle"/>): Neutral is a plain reading in the default
/// foreground, the other three the dashboard's Ok / Warning / Failed vocabulary. A layout maps
/// a <see cref="Fuzn.TestFuzn.Internals.Thresholds.ThresholdState"/> reading onto it — Ok,
/// Warning and Breached to Ok, Warning and Critical.
/// </summary>
internal enum StatTileState
{
    /// <summary>Not judged: the value renders in the default foreground.</summary>
    Neutral,

    /// <summary>Comfortably inside its limit: <see cref="TerminalPalette.OkStyle"/>.</summary>
    Ok,

    /// <summary>Inside its limit but within the warning band next to it: <see cref="TerminalPalette.WarningStyle"/>.</summary>
    Warning,

    /// <summary>Past its limit: <see cref="TerminalPalette.FailedStyle"/>.</summary>
    Critical
}
