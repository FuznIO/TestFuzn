namespace Fuzn.TestFuzn.Internals.Terminal;

internal readonly struct StyledSpan
{
    public string Text { get; }

    public TerminalStyle Style { get; }

    public StyledSpan(string text, TerminalStyle style)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");

        Text = text;
        Style = style;
    }
}
