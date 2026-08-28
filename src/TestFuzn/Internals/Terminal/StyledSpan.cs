namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// A run of text with the single style it should be rendered in. Produced by
/// <see cref="MarkupParser.Parse"/> and rendered by <see cref="MarkupRenderer"/>.
/// </summary>
internal readonly struct StyledSpan
{
    /// <summary>The text of the run, with markup tags resolved and escapes unescaped.</summary>
    public string Text { get; }

    /// <summary>The style the whole run renders in.</summary>
    public TerminalStyle Style { get; }

    public StyledSpan(string text, TerminalStyle style)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");

        Text = text;
        Style = style;
    }
}
