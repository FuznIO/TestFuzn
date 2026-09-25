namespace Fuzn.TestFuzn.Internals.Terminal;

internal readonly struct TerminalColor
{
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

    public static TerminalColor FromPalette(byte paletteIndex, byte red, byte green, byte blue)
    {
        return new TerminalColor(paletteIndex, red, green, blue);
    }

    public static TerminalColor FromRgb(byte red, byte green, byte blue)
    {
        return new TerminalColor(null, red, green, blue);
    }
}
