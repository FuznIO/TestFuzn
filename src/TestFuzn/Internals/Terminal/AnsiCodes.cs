namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// ANSI/VT escape sequences for terminal rendering: SGR styling, cursor addressing,
/// erase operations, and DEC private modes (synchronized output, alternate screen, cursor
/// visibility). Sequences are emitted verbatim; callers gate on <see cref="TerminalCapabilities"/>.
/// </summary>
internal static class AnsiCodes
{
    public const string Escape = "\u001b";
    public const string Csi = Escape + "[";

    // SGR styling
    public const string Reset = Csi + "0m";
    public const string Bold = Csi + "1m";
    public const string Dim = Csi + "2m";
    public const string Italic = Csi + "3m";
    public const string Underline = Csi + "4m";
    public const string Reverse = Csi + "7m";
    public const string Strikethrough = Csi + "9m";
    public const string DefaultForeground = Csi + "39m";
    public const string DefaultBackground = Csi + "49m";

    // Cursor addressing
    public const string CursorHome = Csi + "H";

    // Erase operations
    public const string EraseLine = Csi + "2K";
    public const string EraseToEndOfLine = Csi + "K";
    public const string EraseScreen = Csi + "2J";

    // DEC private mode: synchronized output (?2026)
    public const string BeginSynchronizedOutput = Csi + "?2026h";
    public const string EndSynchronizedOutput = Csi + "?2026l";

    // DEC private mode: alternate screen buffer (?1049)
    public const string EnterAlternateScreen = Csi + "?1049h";
    public const string ExitAlternateScreen = Csi + "?1049l";

    // DEC private mode: cursor visibility (?25)
    public const string ShowCursor = Csi + "?25h";
    public const string HideCursor = Csi + "?25l";

    /// <summary>
    /// Moves the cursor to the given 1-based row and column.
    /// </summary>
    public static string MoveCursor(int row, int column)
    {
        if (row < 1)
            throw new ArgumentOutOfRangeException(nameof(row), row, "Row must be 1 or greater.");
        if (column < 1)
            throw new ArgumentOutOfRangeException(nameof(column), column, "Column must be 1 or greater.");

        return $"{Csi}{row};{column}H";
    }

    /// <summary>
    /// Sets the foreground from the standard 16-color palette.
    /// </summary>
    public static string Foreground(ConsoleColor color)
    {
        return $"{Csi}{ForegroundCode(color)}m";
    }

    /// <summary>
    /// Sets the background from the standard 16-color palette.
    /// </summary>
    public static string Background(ConsoleColor color)
    {
        return $"{Csi}{ForegroundCode(color) + 10}m";
    }

    /// <summary>
    /// Sets a 24-bit RGB foreground. Only valid when <see cref="ColorMode.TrueColor"/> is available.
    /// </summary>
    public static string ForegroundTrueColor(byte red, byte green, byte blue)
    {
        return $"{Csi}38;2;{red};{green};{blue}m";
    }

    /// <summary>
    /// Sets a 24-bit RGB background. Only valid when <see cref="ColorMode.TrueColor"/> is available.
    /// </summary>
    public static string BackgroundTrueColor(byte red, byte green, byte blue)
    {
        return $"{Csi}48;2;{red};{green};{blue}m";
    }

    private static int ForegroundCode(ConsoleColor color)
    {
        switch (color)
        {
            case ConsoleColor.Black: return 30;
            case ConsoleColor.DarkRed: return 31;
            case ConsoleColor.DarkGreen: return 32;
            case ConsoleColor.DarkYellow: return 33;
            case ConsoleColor.DarkBlue: return 34;
            case ConsoleColor.DarkMagenta: return 35;
            case ConsoleColor.DarkCyan: return 36;
            case ConsoleColor.Gray: return 37;
            case ConsoleColor.DarkGray: return 90;
            case ConsoleColor.Red: return 91;
            case ConsoleColor.Green: return 92;
            case ConsoleColor.Yellow: return 93;
            case ConsoleColor.Blue: return 94;
            case ConsoleColor.Magenta: return 95;
            case ConsoleColor.Cyan: return 96;
            case ConsoleColor.White: return 97;
            default: throw new ArgumentOutOfRangeException(nameof(color), color, "Unknown console color.");
        }
    }
}
