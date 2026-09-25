namespace Fuzn.TestFuzn.Internals.Terminal;

internal readonly struct RenderedLine
{
    private static readonly char[] LineBreakCharacters = { '\r', '\n' };

    public string Text { get; }

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
