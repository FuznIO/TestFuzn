namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// A color for terminal rendering: either an xterm-256 palette entry (how markup color names
/// resolve, e.g. green is palette index 2 and darkgreen is palette index 22) or a 24-bit RGB
/// value. Palette colors carry their canonical RGB value so <see cref="MarkupRenderer"/> can
/// downgrade entries outside the standard 16 when rendering in <see cref="ColorMode.Colors16"/>.
/// </summary>
internal readonly struct TerminalColor
{
    /// <summary>The xterm-256 palette index, or null for a 24-bit RGB color.</summary>
    public byte? PaletteIndex { get; }

    public byte Red { get; }
    public byte Green { get; }
    public byte Blue { get; }

    private TerminalColor(byte? paletteIndex, byte red, byte green, byte blue)
    {
        PaletteIndex = paletteIndex;
        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>Creates a palette color with its canonical RGB value.</summary>
    public static TerminalColor FromPalette(byte paletteIndex, byte red, byte green, byte blue)
    {
        return new TerminalColor(paletteIndex, red, green, blue);
    }

    /// <summary>Creates a 24-bit RGB color.</summary>
    public static TerminalColor FromRgb(byte red, byte green, byte blue)
    {
        return new TerminalColor(null, red, green, blue);
    }
}
