namespace Fuzn.TestFuzn.Internals.Terminal;

internal sealed class TerminalCapabilities
{
    private const string TermVariable = "TERM";
    private const string ColorTermVariable = "COLORTERM";
    private const string NoColorVariable = "NO_COLOR";

    public bool IsInteractive { get; }

    public bool SupportsAnsi { get; }

    public bool SupportsLiveView => IsInteractive && SupportsAnsi;

    public ColorMode ColorMode { get; }

    public TerminalCapabilities(bool isInteractive, bool supportsAnsi, ColorMode colorMode)
    {
        IsInteractive = isInteractive;
        SupportsAnsi = supportsAnsi;
        ColorMode = colorMode;
    }

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
        if (supportsAnsi)
        {
            if (IsNoColorRequested(noColor))
                colorMode = ColorMode.Monochrome;
            else if (IsTrueColorTerm(colorTerm))
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
