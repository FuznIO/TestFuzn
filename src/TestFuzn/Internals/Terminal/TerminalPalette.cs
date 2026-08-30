using System.Globalization;

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
/// markup tag words for the [style]...[/] dialect; the logo endpoints are also carried as
/// <see cref="TerminalColor"/> values parsed from the same hex strings, and
/// <see cref="LogoGradientColor"/> / <see cref="LogoGradientStyle"/> interpolate between them —
/// the logo per banner column, the timeline per bar column — so retheming them is still one edit.
/// </summary>
internal static class TerminalPalette
{
    /// <summary>Panel headers on the dashboard and in the summaries: bold, in the warm accent.</summary>
    public const string PanelHeaderStyle = "bold #ff9d3d";

    /// <summary>The filled cells of the dashboard's progress bar.</summary>
    public const string ProgressBarStyle = "#ff9d3d";

    /// <summary>The simulation phase label on the dashboard's timeline and title line.</summary>
    public const string PhaseStyle = "#ffcf6b";

    // The latency chart's three bands, deepest for the slowest percentile: the logo gradient's
    // deep orange, the warm accent, the logo gradient's warm amber.

    /// <summary>The latency chart's p99 band: the logo gradient's deep orange.</summary>
    public const string ResponseTimePercentile99Style = LogoGradientStartStyle;

    /// <summary>The latency chart's p95 band: the warm accent.</summary>
    public const string ResponseTimePercentile95Style = "#ff9d3d";

    /// <summary>The latency chart's median (p50) band: the logo gradient's warm amber.</summary>
    public const string ResponseTimeMedianStyle = LogoGradientEndStyle;

    /// <summary>Ok requests, a passed scenario, a change in the good direction, a tile in the ok state.</summary>
    public const string OkStyle = "green";

    /// <summary>Failed requests, a failed scenario, errors, a change in the bad direction, a tile in the critical state.</summary>
    public const string FailedStyle = "red";

    /// <summary>The running status badge.</summary>
    public const string RunningStyle = "yellow";

    /// <summary>The paused badge beside the status badge: the viewer froze the frame, so the numbers on screen are standing still — a caution in the running badge's colour (<see cref="RunningStyle"/>, so retheming it moves the badge too), bold to stand out from it.</summary>
    public const string PausedStyle = "bold " + RunningStyle;

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

    /// <summary>
    /// The logo gradient's colour at <paramref name="position"/> along it — <see cref="LogoGradientStart"/>
    /// at 0, <see cref="LogoGradientEnd"/> at 1, every channel interpolated linearly and rounded
    /// half away from zero, the way the logo colours its banner columns. A position outside 0..1
    /// clamps to the nearer end, and NaN reads as 0.
    /// </summary>
    public static TerminalColor LogoGradientColor(double position)
    {
        if (double.IsNaN(position) || position < 0)
            position = 0;
        else if (position > 1)
            position = 1;

        return TerminalColor.FromRgb(
            InterpolateChannel(LogoGradientStart.Red, LogoGradientEnd.Red, position),
            InterpolateChannel(LogoGradientStart.Green, LogoGradientEnd.Green, position),
            InterpolateChannel(LogoGradientStart.Blue, LogoGradientEnd.Blue, position));
    }

    /// <summary>
    /// <see cref="LogoGradientColor"/> as markup tag words ("#FF9636"), for the widgets that
    /// render through markup rather than composing escapes.
    /// </summary>
    public static string LogoGradientStyle(double position)
    {
        var color = LogoGradientColor(position);
        return "#" + color.Red.ToString("X2", CultureInfo.InvariantCulture)
            + color.Green.ToString("X2", CultureInfo.InvariantCulture)
            + color.Blue.ToString("X2", CultureInfo.InvariantCulture);
    }

    private static byte InterpolateChannel(byte start, byte end, double position)
    {
        return (byte)Math.Round(start + ((end - start) * position), MidpointRounding.AwayFromZero);
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
