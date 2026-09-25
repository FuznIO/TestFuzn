using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class MarkupParser
{
    private static readonly Dictionary<string, TerminalColor> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "black", TerminalColor.FromPalette(0, 0, 0, 0) },
        { "maroon", TerminalColor.FromPalette(1, 128, 0, 0) },
        { "green", TerminalColor.FromPalette(2, 0, 128, 0) },
        { "olive", TerminalColor.FromPalette(3, 128, 128, 0) },
        { "navy", TerminalColor.FromPalette(4, 0, 0, 128) },
        { "purple", TerminalColor.FromPalette(5, 128, 0, 128) },
        { "teal", TerminalColor.FromPalette(6, 0, 128, 128) },
        { "silver", TerminalColor.FromPalette(7, 192, 192, 192) },
        { "grey", TerminalColor.FromPalette(8, 128, 128, 128) },
        { "gray", TerminalColor.FromPalette(8, 128, 128, 128) },
        { "red", TerminalColor.FromPalette(9, 255, 0, 0) },
        { "lime", TerminalColor.FromPalette(10, 0, 255, 0) },
        { "yellow", TerminalColor.FromPalette(11, 255, 255, 0) },
        { "blue", TerminalColor.FromPalette(12, 0, 0, 255) },
        { "fuchsia", TerminalColor.FromPalette(13, 255, 0, 255) },
        { "aqua", TerminalColor.FromPalette(14, 0, 255, 255) },
        { "white", TerminalColor.FromPalette(15, 255, 255, 255) },
        { "darkgreen", TerminalColor.FromPalette(22, 0, 95, 0) }
    };

    public static IReadOnlyList<StyledSpan> Parse(string? markup)
    {
        var spans = new List<StyledSpan>();
        if (string.IsNullOrEmpty(markup))
            return spans;

        var styleStack = new Stack<TerminalStyle>();
        var currentStyle = TerminalStyle.Plain;
        var text = new StringBuilder();

        var position = 0;
        while (position < markup.Length)
        {
            var character = markup[position];

            if (character == '[')
            {
                if (position + 1 < markup.Length && markup[position + 1] == '[')
                {
                    text.Append('[');
                    position += 2;
                    continue;
                }

                var closeBracket = markup.IndexOf(']', position + 1);
                if (closeBracket < 0)
                {
                    text.Append(markup, position, markup.Length - position);
                    break;
                }

                var tagContent = markup.Substring(position + 1, closeBracket - position - 1);
                if (tagContent == "/")
                {
                    if (styleStack.Count > 0)
                    {
                        FlushSpan(spans, text, currentStyle);
                        currentStyle = styleStack.Pop();
                    }
                }
                else if (TryResolveTag(tagContent, currentStyle, out var tagStyle))
                {
                    FlushSpan(spans, text, currentStyle);
                    styleStack.Push(currentStyle);
                    currentStyle = tagStyle;
                }
                else
                {
                    text.Append('[').Append(tagContent).Append(']');
                }

                position = closeBracket + 1;
                continue;
            }

            if (character == ']')
            {
                if (position + 1 < markup.Length && markup[position + 1] == ']')
                {
                    text.Append(']');
                    position += 2;
                    continue;
                }

                text.Append(']');
                position++;
                continue;
            }

            text.Append(character);
            position++;
        }

        FlushSpan(spans, text, currentStyle);
        return spans;
    }

    public static string StripMarkup(string? markup)
    {
        var spans = Parse(markup);
        if (spans.Count == 0)
            return string.Empty;
        if (spans.Count == 1)
            return spans[0].Text;

        var text = new StringBuilder();
        foreach (var span in spans)
            text.Append(span.Text);

        return text.ToString();
    }

    public static string Escape(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");

        if (text.IndexOf('[') < 0 && text.IndexOf(']') < 0)
            return text;

        return text.Replace("[", "[[").Replace("]", "]]");
    }

    private static void FlushSpan(List<StyledSpan> spans, StringBuilder text, TerminalStyle style)
    {
        if (text.Length == 0)
            return;

        spans.Add(new StyledSpan(text.ToString(), style));
        text.Clear();
    }

    private static bool TryResolveTag(string tagContent, TerminalStyle currentStyle, out TerminalStyle tagStyle)
    {
        tagStyle = currentStyle;

        if (tagContent.StartsWith('/'))
            return false;

        var words = tagContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return false;

        var expectBackground = false;
        foreach (var word in words)
        {
            if (word.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                if (expectBackground)
                    return false;

                expectBackground = true;
                continue;
            }

            if (expectBackground)
            {
                if (!TryResolveColor(word, out var background))
                    return false;

                tagStyle = tagStyle with { Background = background };
                expectBackground = false;
                continue;
            }

            if (TryResolveStyleWord(word, ref tagStyle))
                continue;

            if (TryResolveColor(word, out var foreground))
            {
                tagStyle = tagStyle with { Foreground = foreground };
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool TryResolveStyleWord(string word, ref TerminalStyle style)
    {
        if (word.Equals("bold", StringComparison.OrdinalIgnoreCase) || word.Equals("b", StringComparison.OrdinalIgnoreCase))
        {
            style = style with { Bold = true };
            return true;
        }

        if (word.Equals("dim", StringComparison.OrdinalIgnoreCase))
        {
            style = style with { Dim = true };
            return true;
        }

        if (word.Equals("italic", StringComparison.OrdinalIgnoreCase) || word.Equals("i", StringComparison.OrdinalIgnoreCase))
        {
            style = style with { Italic = true };
            return true;
        }

        if (word.Equals("underline", StringComparison.OrdinalIgnoreCase) || word.Equals("u", StringComparison.OrdinalIgnoreCase))
        {
            style = style with { Underline = true };
            return true;
        }

        if (word.Equals("reverse", StringComparison.OrdinalIgnoreCase) || word.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            style = style with { Reverse = true };
            return true;
        }

        if (word.Equals("strikethrough", StringComparison.OrdinalIgnoreCase) || word.Equals("s", StringComparison.OrdinalIgnoreCase))
        {
            style = style with { Strikethrough = true };
            return true;
        }

        if (word.Equals("default", StringComparison.OrdinalIgnoreCase) || word.Equals("none", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool TryResolveColor(string word, out TerminalColor color)
    {
        if (NamedColors.TryGetValue(word, out color))
            return true;

        return TryParseHexColor(word, out color);
    }

    private static bool TryParseHexColor(string word, out TerminalColor color)
    {
        color = default;

        if (word.Length != 4 && word.Length != 7)
            return false;
        if (word[0] != '#')
            return false;

        if (word.Length == 4)
        {
            if (!TryParseHexDigit(word[1], out var red)
                || !TryParseHexDigit(word[2], out var green)
                || !TryParseHexDigit(word[3], out var blue))
                return false;

            color = TerminalColor.FromRgb((byte)(red * 17), (byte)(green * 17), (byte)(blue * 17));
            return true;
        }

        if (!TryParseHexBytePair(word[1], word[2], out var redByte)
            || !TryParseHexBytePair(word[3], word[4], out var greenByte)
            || !TryParseHexBytePair(word[5], word[6], out var blueByte))
            return false;

        color = TerminalColor.FromRgb(redByte, greenByte, blueByte);
        return true;
    }

    private static bool TryParseHexBytePair(char highCharacter, char lowCharacter, out byte value)
    {
        value = 0;
        if (!TryParseHexDigit(highCharacter, out var high) || !TryParseHexDigit(lowCharacter, out var low))
            return false;

        value = (byte)((high * 16) + low);
        return true;
    }

    private static bool TryParseHexDigit(char character, out int value)
    {
        if (character >= '0' && character <= '9')
        {
            value = character - '0';
            return true;
        }

        if (character >= 'a' && character <= 'f')
        {
            value = character - 'a' + 10;
            return true;
        }

        if (character >= 'A' && character <= 'F')
        {
            value = character - 'A' + 10;
            return true;
        }

        value = 0;
        return false;
    }
}
