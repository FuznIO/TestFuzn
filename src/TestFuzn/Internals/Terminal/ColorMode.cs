namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Color depth available for terminal rendering.
/// </summary>
internal enum ColorMode
{
    /// <summary>
    /// No escape codes at all, colors and decorations alike: the safety mode for contexts where
    /// ANSI cannot be emitted (redirected output, TERM=dumb, failed VT enablement).
    /// </summary>
    None,

    /// <summary>
    /// Decorations (bold, dim, italic, underline, reverse, strikethrough) emit SGR but
    /// foreground/background colors are dropped (NO_COLOR set on an ANSI terminal).
    /// </summary>
    Monochrome,

    /// <summary>Standard 16-color ANSI palette.</summary>
    Colors16,

    /// <summary>24-bit RGB color (COLORTERM=truecolor or COLORTERM=24bit).</summary>
    TrueColor
}
