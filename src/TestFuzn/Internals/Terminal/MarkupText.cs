using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class MarkupText
{
    public const char Ellipsis = '…';

    public static int Measure(string? markup)
    {
        return TotalWidth(SanitizedSpans(markup));
    }

    public static RenderedLine RenderFitted(string? markup, int width, ColorMode colorMode, TextAlignment alignment = TextAlignment.Left)
    {
        if (width <= 0)
            return new RenderedLine(string.Empty, 0);

        var spans = SanitizedSpans(markup);
        var contentWidth = TotalWidth(spans);

        string rendered;
        if (contentWidth <= width)
        {
            rendered = MarkupRenderer.Render(spans, colorMode);
        }
        else
        {
            var truncated = TruncateSpans(spans, width);
            rendered = MarkupRenderer.Render(truncated, colorMode);
            contentWidth = TotalWidth(truncated);
        }

        var padding = width - contentWidth;
        if (padding == 0)
            return new RenderedLine(rendered, width);

        if (alignment == TextAlignment.Right)
            return new RenderedLine(new string(' ', padding) + rendered, width);

        return new RenderedLine(rendered + new string(' ', padding), width);
    }

    public static RenderedLine RenderTruncated(string? markup, int maxWidth, ColorMode colorMode)
    {
        if (maxWidth <= 0)
            return new RenderedLine(string.Empty, 0);

        var spans = SanitizedSpans(markup);
        var contentWidth = TotalWidth(spans);
        if (contentWidth <= maxWidth)
            return new RenderedLine(MarkupRenderer.Render(spans, colorMode), contentWidth);

        var truncated = TruncateSpans(spans, maxWidth);
        return new RenderedLine(MarkupRenderer.Render(truncated, colorMode), TotalWidth(truncated));
    }

    private static List<StyledSpan> SanitizedSpans(string? markup)
    {
        var spans = MarkupParser.Parse(markup);
        var sanitized = new List<StyledSpan>(spans.Count);
        foreach (var span in spans)
            sanitized.Add(new StyledSpan(SanitizeControlCharacters(span.Text), span.Style));

        return sanitized;
    }

    internal static string SanitizeControlCharacters(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");

        if (!ContainsControlCharacter(text))
            return text;

        var sanitized = new StringBuilder(text.Length);
        var position = 0;
        while (position < text.Length)
        {
            var character = text[position];
            if (character == '\r')
            {
                sanitized.Append(' ');
                if (position + 1 < text.Length && text[position + 1] == '\n')
                    position++;
            }
            else if (IsControlCharacter(character))
            {
                sanitized.Append(' ');
            }
            else
            {
                sanitized.Append(character);
            }

            position++;
        }

        return sanitized.ToString();
    }

    private static bool ContainsControlCharacter(string text)
    {
        foreach (var character in text)
        {
            if (IsControlCharacter(character))
                return true;
        }

        return false;
    }

    private static bool IsControlCharacter(char character)
    {
        return character < ' ' || character == '\u007f';
    }

    private static int TotalWidth(List<StyledSpan> spans)
    {
        var width = 0;
        foreach (var span in spans)
            width += span.Text.Length;

        return width;
    }

    private static List<StyledSpan> TruncateSpans(List<StyledSpan> spans, int maxWidth)
    {
        var kept = new List<StyledSpan>();
        var remaining = maxWidth - 1;
        foreach (var span in spans)
        {
            if (remaining >= span.Text.Length)
            {
                kept.Add(span);
                remaining -= span.Text.Length;
                continue;
            }

            var cut = remaining;
            if (cut > 0 && char.IsHighSurrogate(span.Text[cut - 1]) && char.IsLowSurrogate(span.Text[cut]))
                cut--;

            if (cut > 0)
                kept.Add(new StyledSpan(span.Text.Substring(0, cut) + Ellipsis, span.Style));
            else
                kept.Add(new StyledSpan(Ellipsis.ToString(), span.Style));

            return kept;
        }

        return kept;
    }
}
