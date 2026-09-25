namespace Fuzn.TestFuzn.Internals.Terminal;

internal readonly struct TerminalStyle
{
    public TerminalColor? Foreground { get; init; }

    public TerminalColor? Background { get; init; }

    public bool Bold { get; init; }

    public bool Dim { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public bool Reverse { get; init; }

    public bool Strikethrough { get; init; }

    public static TerminalStyle Plain => default;

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
