namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Color depth available for terminal rendering.
/// </summary>
internal enum ColorMode
{
    /// <summary>No color output (redirected output, TERM=dumb, or NO_COLOR set).</summary>
    None,

    /// <summary>Standard 16-color ANSI palette.</summary>
    Colors16,

    /// <summary>24-bit RGB color (COLORTERM=truecolor or COLORTERM=24bit).</summary>
    TrueColor
}
