namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The visual style of a span of terminal text: optional foreground and background colors plus
/// SGR decoration flags. The default value is plain (no colors, no decorations); values are
/// immutable, so derive variants with `with` expressions.
/// </summary>
internal readonly struct TerminalStyle
{
    /// <summary>Foreground color, or null for the terminal default.</summary>
    public TerminalColor? Foreground { get; init; }

    /// <summary>Background color, or null for the terminal default.</summary>
    public TerminalColor? Background { get; init; }

    /// <summary>Bold (increased intensity) decoration, SGR code 1.</summary>
    public bool Bold { get; init; }

    /// <summary>Dim (decreased intensity) decoration, SGR code 2.</summary>
    public bool Dim { get; init; }

    /// <summary>Italic decoration, SGR code 3.</summary>
    public bool Italic { get; init; }

    /// <summary>Underline decoration, SGR code 4.</summary>
    public bool Underline { get; init; }

    /// <summary>Reverse video (swapped foreground and background) decoration, SGR code 7.</summary>
    public bool Reverse { get; init; }

    /// <summary>Strikethrough decoration, SGR code 9.</summary>
    public bool Strikethrough { get; init; }

    /// <summary>A style with no colors and no decorations.</summary>
    public static TerminalStyle Plain => default;

    /// <summary>True when the style has no colors and no decorations.</summary>
    public bool IsPlain
    {
        get
        {
            return Foreground == null
                && Background == null
                && !Bold && !Dim && !Italic && !Underline && !Reverse && !Strikethrough;
        }
    }
}
