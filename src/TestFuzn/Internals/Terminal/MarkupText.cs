using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Measures, truncates and pads markup text by its display width so widgets can lay out styled
/// content without counting SGR escape bytes: widths are computed on the markup's stripped text
/// (via <see cref="MarkupParser"/>) while rendering goes through <see cref="MarkupRenderer"/>,
/// so the emitted string carries the styling but the math never sees it. Every character counts
/// as one display column — the box-drawing, block and braille glyphs the widgets emit are
/// single-width, and wide glyphs (CJK, emoji) are out of scope for dashboard content. Content
/// wider than the given width truncates with a trailing <see cref="Ellipsis"/> instead of
/// overflowing; the ellipsis takes the style of the character it replaced, so truncated styled
/// content stays Reset-closed, and a cut never lands between a high surrogate and its low half —
/// it backs off one character so a surrogate pair stays intact. Control characters can never
/// reach the terminal: every C0 control (0x00-0x1F — line breaks, TAB, ESC, BEL, all of them)
/// and DEL (0x7F) sanitizes to a single space, with a \r\n pair collapsing to one space, which
/// keeps two guarantees unconditional — the display width is exact (a TAB cannot silently widen
/// a row, an embedded escape sequence cannot clear the screen or bleed styling), and
/// <see cref="ColorMode.None"/> output contains zero ESC bytes. The fitted and truncated
/// renders return a <see cref="RenderedLine"/> — always a single terminal row, its width known
/// exactly; callers render multi-line text as one widget line per row instead. Stateless and
/// thread-safe.
/// </summary>
internal static class MarkupText
{
    /// <summary>The single-column character appended where text was truncated.</summary>
    public const char Ellipsis = '…';

    /// <summary>
    /// Returns the display width of the markup's text: tags contribute nothing, escapes count
    /// unescaped, and each control character counts as the single space it sanitizes to (with
    /// \r\n counting as one). Null is treated as an empty string.
    /// </summary>
    public static int Measure(string? markup)
    {
        return TotalWidth(SanitizedSpans(markup));
    }

    /// <summary>
    /// Renders the markup at exactly <paramref name="width"/> display columns: content that is
    /// too wide truncates with an ellipsis, narrower content pads with plain spaces on the side
    /// opposite the alignment (outside any styling). A width below 1 renders an empty line of
    /// width 0.
    /// </summary>
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
            // Truncation can land one column short of the width when it backs off a surrogate
            // pair, so the padding math uses the truncated spans' actual width.
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

    /// <summary>
    /// Renders the markup at up to <paramref name="maxWidth"/> display columns, truncating with
    /// an ellipsis when it is wider and never padding. A maximum below 1 renders an empty line
    /// of width 0.
    /// </summary>
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

    // Replaces every C0 control (0x00-0x1F) and DEL (0x7F) with a single space, collapsing a
    // \r\n pair to one, so sanitized text is break-free, exactly one column per character, and
    // free of ESC bytes.
    private static string SanitizeControlCharacters(string text)
    {
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

    // Cuts the spans down to maxWidth columns ending in an ellipsis. The ellipsis takes the
    // style of the character it replaced: it is appended to the last kept span when the cut
    // lands inside one, and emitted as its own span in the first removed span's style when the
    // cut lands on a span boundary. A cut that would land between a high surrogate and its low
    // half backs off one character so the pair stays intact — the result is then one column
    // short of maxWidth, which the callers absorb by measuring the truncated spans.
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
