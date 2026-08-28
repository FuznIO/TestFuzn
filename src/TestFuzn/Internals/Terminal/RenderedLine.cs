namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// A fully rendered, break-free terminal line whose display width is known at construction.
/// This is the type widgets emit and <see cref="FrameBuffer"/> consumes, and it settles the
/// markup-versus-rendered contract: <see cref="Text"/> is final terminal output — SGR escapes
/// already embedded, markup already resolved — and must never be fed back through
/// <see cref="MarkupParser"/> or into a markup parameter (a panel's markup content lines, a
/// table's cells), where its escapes would be re-measured as text. <see cref="Width"/> is the
/// line's exact display width in columns, declared by the producer that rendered it — nothing
/// ever re-derives it by parsing ANSI escapes out of <see cref="Text"/>. Rendered lines are
/// break-free by construction, so the constructor fails loud on any \r or \n in the text
/// rather than letting a multi-row string masquerade as one line.
/// </summary>
internal readonly struct RenderedLine
{
    private static readonly char[] LineBreakCharacters = { '\r', '\n' };

    /// <summary>The rendered text of the line, with any SGR styling already embedded.</summary>
    public string Text { get; }

    /// <summary>The exact display width of the line in terminal columns.</summary>
    public int Width { get; }

    public RenderedLine(string text, int width)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");
        if (text.IndexOfAny(LineBreakCharacters) >= 0)
            throw new ArgumentException("Text cannot contain line breaks; a rendered line is a single terminal row.", nameof(text));
        if (width < 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width cannot be negative.");

        Text = text;
        Width = width;
    }
}
