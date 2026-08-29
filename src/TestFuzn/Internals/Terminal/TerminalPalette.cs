namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The terminal engine's palette as markup style constants — the one place to retheme the
/// standalone runner's live dashboard, its final summaries, the exception renderer, the
/// runner's own messages and the widgets that share their look. The warm accents stay inside
/// the logo gradient family (<see cref="LogoGradientStartStyle"/> → <see cref="LogoGradientEndStyle"/>),
/// and the status colours keep one vocabulary across every surface: <see cref="OkStyle"/> for
/// a passed run and an ok request, <see cref="WarningStyle"/> for a caution, <see cref="FailedStyle"/>
/// for a failure. A <see cref="StatTileState"/> maps onto that same vocabulary through
/// <see cref="StateStyle"/> — Ok to <see cref="OkStyle"/>, Warning to <see cref="WarningStyle"/>,
/// Critical to <see cref="FailedStyle"/>, Neutral to no style at all (the default foreground) —
/// so a tile in the critical state is the same red as a failed request row. Every constant is
/// markup tag words for the [style]...[/] dialect; the logo endpoints, which the logo
/// interpolates per column, are also carried as <see cref="TerminalColor"/> values parsed from
/// the same hex strings, so retheming them is still one edit.
/// </summary>
internal static class TerminalPalette
{
    /// <summary>Panel headers on the dashboard and in the summaries: bold, in the warm accent.</summary>
    public const string PanelHeaderStyle = "bold #ff9d3d";

    /// <summary>The filled cells of the dashboard's progress bar.</summary>
    public const string ProgressBarStyle = "#ff9d3d";

    /// <summary>The dashboard's requests-per-second sparkline.</summary>
    public const string RequestsSparklineStyle = "#ff9d3d";

    /// <summary>The dashboard's response-time sparkline.</summary>
    public const string ResponseTimeSparklineStyle = "#ffcf6b";

    /// <summary>The simulation phase label on the dashboard's timing line.</summary>
    public const string PhaseStyle = "#ffcf6b";

    /// <summary>Ok requests, a passed scenario, a change in the good direction, a tile in the ok state.</summary>
    public const string OkStyle = "green";

    /// <summary>Failed requests, a failed scenario, errors, a change in the bad direction, a tile in the critical state.</summary>
    public const string FailedStyle = "red";

    /// <summary>The running status badge.</summary>
    public const string RunningStyle = "yellow";

    /// <summary>A caution: a skipped or stopped run's message, a mild failure share, a tile in the warning state.</summary>
    public const string WarningStyle = "yellow";

    /// <summary>Secondary text: labels, column headers, units, tracks and separators.</summary>
    public const string SecondaryStyle = "dim";

    /// <summary>The logo gradient's left edge: deep orange.</summary>
    public const string LogoGradientStartStyle = "#FF5C00";

    /// <summary>The logo gradient's right edge: warm amber.</summary>
    public const string LogoGradientEndStyle = "#FFCF6B";

    /// <summary><see cref="LogoGradientStartStyle"/> as a colour, for per-column interpolation.</summary>
    public static readonly TerminalColor LogoGradientStart = ParseHexColor(LogoGradientStartStyle);

    /// <summary><see cref="LogoGradientEndStyle"/> as a colour, for per-column interpolation.</summary>
    public static readonly TerminalColor LogoGradientEnd = ParseHexColor(LogoGradientEndStyle);

    // The heatmap's six intensity stops, faintest first: dark ember through the logo's warm
    // gradient (its start, its midpoint, its end) to white-hot.

    /// <summary>The heatmap's faintest stop: dark ember.</summary>
    public const string HeatDarkEmberStyle = "#3A1400";

    /// <summary>The heatmap's second stop: ember.</summary>
    public const string HeatEmberStyle = "#9C3800";

    /// <summary>The heatmap's third stop: the logo gradient's deep orange.</summary>
    public const string HeatOrangeStyle = LogoGradientStartStyle;

    /// <summary>The heatmap's fourth stop: the logo gradient's midpoint.</summary>
    public const string HeatLightOrangeStyle = "#FF9536";

    /// <summary>The heatmap's fifth stop: the logo gradient's warm amber.</summary>
    public const string HeatAmberStyle = LogoGradientEndStyle;

    /// <summary>The heatmap's brightest stop: white-hot.</summary>
    public const string HeatWhiteHotStyle = "#FFFFFF";

    /// <summary>
    /// The style a <see cref="StatTileState"/> renders in, in the one status vocabulary: Ok is
    /// <see cref="OkStyle"/>, Warning <see cref="WarningStyle"/>, Critical <see cref="FailedStyle"/>,
    /// and Neutral null — no style, the default foreground.
    /// </summary>
    public static string? StateStyle(StatTileState state)
    {
        switch (state)
        {
            case StatTileState.Neutral: return null;
            case StatTileState.Ok: return OkStyle;
            case StatTileState.Warning: return WarningStyle;
            case StatTileState.Critical: return FailedStyle;
            default: throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown stat tile state.");
        }
    }

    // "#RRGGBB" to its colour; the palette's own hex constants are the only input.
    private static TerminalColor ParseHexColor(string hexStyle)
    {
        return TerminalColor.FromRgb(
            Convert.ToByte(hexStyle.Substring(1, 2), 16),
            Convert.ToByte(hexStyle.Substring(3, 2), 16),
            Convert.ToByte(hexStyle.Substring(5, 2), 16));
    }
}
