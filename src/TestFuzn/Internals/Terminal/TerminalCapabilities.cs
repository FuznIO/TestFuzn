namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Resolved terminal capabilities: whether the terminal is interactive, whether ANSI/VT
/// sequences can be emitted, and which color depth is available.
/// </summary>
internal sealed class TerminalCapabilities
{
    private const string TermVariable = "TERM";
    private const string ColorTermVariable = "COLORTERM";
    private const string NoColorVariable = "NO_COLOR";

    /// <summary>Both output and input are attached to a terminal (neither redirected).</summary>
    public bool IsInteractive { get; }

    /// <summary>ANSI/VT sequences (cursor addressing, erase, screen modes) can be emitted.</summary>
    public bool SupportsAnsi { get; }

    /// <summary>The live view can render: the terminal is interactive and ANSI sequences can be emitted.</summary>
    public bool SupportsLiveView => IsInteractive && SupportsAnsi;

    /// <summary>Color depth to render with. NO_COLOR disables color without disabling ANSI.</summary>
    public ColorMode ColorMode { get; }

    public TerminalCapabilities(bool isInteractive, bool supportsAnsi, ColorMode colorMode)
    {
        IsInteractive = isInteractive;
        SupportsAnsi = supportsAnsi;
        ColorMode = colorMode;
    }

    /// <summary>
    /// Production factory: reads the real console redirect state and environment variables,
    /// enabling VT processing on Windows first, then resolves capabilities.
    /// </summary>
    public static TerminalCapabilities Detect(IEnvironmentWrapper environment)
    {
        if (environment == null)
            throw new ArgumentNullException(nameof(environment), "Environment cannot be null.");

        var isOutputRedirected = Console.IsOutputRedirected;

        var isVirtualTerminalEnabled = true;
        if (!isOutputRedirected)
            isVirtualTerminalEnabled = WindowsVirtualTerminal.TryEnable();

        return Resolve(
            isOutputRedirected,
            Console.IsInputRedirected,
            isVirtualTerminalEnabled,
            environment.GetEnvironmentVariable(TermVariable),
            environment.GetEnvironmentVariable(ColorTermVariable),
            environment.GetEnvironmentVariable(NoColorVariable));
    }

    /// <summary>
    /// Resolves capabilities from raw inputs, so tests can drive every combination without a TTY.
    /// </summary>
    public static TerminalCapabilities Resolve(
        bool isOutputRedirected,
        bool isInputRedirected,
        bool isVirtualTerminalEnabled,
        string? term,
        string? colorTerm,
        string? noColor)
    {
        var isInteractive = !isOutputRedirected && !isInputRedirected;
        var supportsAnsi = !isOutputRedirected && isVirtualTerminalEnabled && !IsDumbTerminal(term);

        var colorMode = ColorMode.None;
        if (supportsAnsi && !IsNoColorRequested(noColor))
        {
            if (IsTrueColorTerm(colorTerm))
                colorMode = ColorMode.TrueColor;
            else
                colorMode = ColorMode.Colors16;
        }

        return new TerminalCapabilities(isInteractive, supportsAnsi, colorMode);
    }

    private static bool IsDumbTerminal(string? term)
    {
        if (term == null)
            return false;

        return term.Equals("dumb", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoColorRequested(string? noColor)
    {
        // Per https://no-color.org: any non-empty value disables color.
        return !string.IsNullOrEmpty(noColor);
    }

    private static bool IsTrueColorTerm(string? colorTerm)
    {
        if (colorTerm == null)
            return false;

        return colorTerm.Equals("truecolor", StringComparison.OrdinalIgnoreCase)
            || colorTerm.Equals("24bit", StringComparison.OrdinalIgnoreCase);
    }
}
