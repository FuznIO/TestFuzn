using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders markup or pre-parsed styled spans to a string with embedded SGR sequences for the
/// given <see cref="ColorMode"/>, matching what Spectre.Console emits for the same markup:
/// palette colors render as 38;5;N / 48;5;N in <see cref="ColorMode.TrueColor"/> and as the
/// classic 30-37/90-97 codes (+10 for backgrounds) in <see cref="ColorMode.Colors16"/>, where
/// palette entries outside the standard 16 and RGB colors downgrade to the closest standard
/// color; RGB colors render as 38;2;R;G;B / 48;2;R;G;B in true color.
/// <see cref="ColorMode.Monochrome"/> emits decoration SGR (bold, dim, italic, underline,
/// reverse, strikethrough) but drops foreground/background colors, matching Spectre.Console's
/// NoColors color system on an ANSI terminal; <see cref="ColorMode.None"/> renders plain text
/// with no escape codes at all. Every styled span is closed with <see cref="AnsiCodes.Reset"/>,
/// and styling never spans a line break: when a styled span's text contains line breaks
/// (\r\n, \n or a lone \r) the style is closed before each break and reopened after it, with
/// the break characters preserved verbatim, so every physical line is independently
/// style-complete and the rows <see cref="FrameBuffer.AddLine"/> splits rendered text into
/// carry their own styling. (Spectre.Console closes styles around \n the same way, but it also
/// normalizes \r\n to \n — TextWriter behavior, not styling, and not copied here — and leaves
/// a lone \r inside the styled run.) Stateless and thread-safe.
/// </summary>
internal static class MarkupRenderer
{
    // The standard 16-color palette (xterm indices 0-15) used to downgrade other colors in
    // Colors16 mode. RGB values match Spectre.Console's palette so downgrades pick the same
    // standard color it would.
    private static readonly (byte Red, byte Green, byte Blue)[] StandardPalette =
    {
        (0, 0, 0),        // 0 black
        (128, 0, 0),      // 1 maroon
        (0, 128, 0),      // 2 green
        (128, 128, 0),    // 3 olive
        (0, 0, 128),      // 4 navy
        (128, 0, 128),    // 5 purple
        (0, 128, 128),    // 6 teal
        (192, 192, 192),  // 7 silver
        (128, 128, 128),  // 8 grey
        (255, 0, 0),      // 9 red
        (0, 255, 0),      // 10 lime
        (255, 255, 0),    // 11 yellow
        (0, 0, 255),      // 12 blue
        (255, 0, 255),    // 13 fuchsia
        (0, 255, 255),    // 14 aqua
        (255, 255, 255)   // 15 white
    };

    /// <summary>Parses the markup and renders it. Null is treated as an empty string.</summary>
    public static string Render(string? markup, ColorMode colorMode)
    {
        if (colorMode == ColorMode.None)
            return MarkupParser.StripMarkup(markup);

        return Render(MarkupParser.Parse(markup), colorMode);
    }

    /// <summary>Renders pre-parsed or directly composed spans.</summary>
    public static string Render(IReadOnlyList<StyledSpan> spans, ColorMode colorMode)
    {
        if (spans == null)
            throw new ArgumentNullException(nameof(spans), "Spans cannot be null.");

        var output = new StringBuilder();
        foreach (var span in spans)
        {
            if (string.IsNullOrEmpty(span.Text))
                continue;

            var parameters = BuildSgrParameters(span.Style, colorMode);
            if (parameters.Length == 0)
            {
                output.Append(span.Text);
                continue;
            }

            AppendStyledText(output, span.Text, parameters);
        }

        return output.ToString();
    }

    private static readonly char[] LineBreakCharacters = { '\r', '\n' };

    // Emits the text with the style opened before and Reset after every styled segment, closing
    // the style before each line break and reopening it after, with the break characters kept
    // verbatim, so styling never spans a physical line break.
    private static void AppendStyledText(StringBuilder output, string text, string parameters)
    {
        var position = 0;
        while (position < text.Length)
        {
            var breakIndex = text.IndexOfAny(LineBreakCharacters, position);
            if (breakIndex < 0)
            {
                AppendStyledSegment(output, text, position, text.Length - position, parameters);
                return;
            }

            AppendStyledSegment(output, text, position, breakIndex - position, parameters);

            var breakLength = 1;
            if (text[breakIndex] == '\r' && breakIndex + 1 < text.Length && text[breakIndex + 1] == '\n')
                breakLength = 2;

            output.Append(text, breakIndex, breakLength);
            position = breakIndex + breakLength;
        }
    }

    private static void AppendStyledSegment(StringBuilder output, string text, int start, int length, string parameters)
    {
        if (length == 0)
            return;

        output.Append(AnsiCodes.Csi).Append(parameters).Append('m');
        output.Append(text, start, length);
        output.Append(AnsiCodes.Reset);
    }

    private static string BuildSgrParameters(TerminalStyle style, ColorMode colorMode)
    {
        if (colorMode == ColorMode.None || style.IsPlain)
            return string.Empty;

        var parameters = new StringBuilder();

        if (style.Bold)
            AppendParameter(parameters, "1");
        if (style.Dim)
            AppendParameter(parameters, "2");
        if (style.Italic)
            AppendParameter(parameters, "3");
        if (style.Underline)
            AppendParameter(parameters, "4");
        if (style.Reverse)
            AppendParameter(parameters, "7");
        if (style.Strikethrough)
            AppendParameter(parameters, "9");

        if (colorMode != ColorMode.Monochrome)
        {
            if (style.Foreground != null)
                AppendParameter(parameters, ColorParameters(style.Foreground.Value, colorMode, isForeground: true));

            if (style.Background != null)
                AppendParameter(parameters, ColorParameters(style.Background.Value, colorMode, isForeground: false));
        }

        return parameters.ToString();
    }

    private static void AppendParameter(StringBuilder parameters, string parameter)
    {
        if (parameters.Length > 0)
            parameters.Append(';');

        parameters.Append(parameter);
    }

    private static string ColorParameters(TerminalColor color, ColorMode colorMode, bool isForeground)
    {
        if (colorMode == ColorMode.TrueColor)
        {
            if (color.PaletteIndex != null)
                return $"{(isForeground ? 38 : 48)};5;{color.PaletteIndex.Value}";

            return $"{(isForeground ? 38 : 48)};2;{color.Red};{color.Green};{color.Blue}";
        }

        var paletteIndex = color.PaletteIndex;
        if (paletteIndex == null || paletteIndex.Value >= StandardPalette.Length)
            paletteIndex = ClosestStandardPaletteIndex(color.Red, color.Green, color.Blue);

        int code;
        if (paletteIndex.Value < 8)
            code = 30 + paletteIndex.Value;
        else
            code = 90 + (paletteIndex.Value - 8);

        if (!isForeground)
            code += 10;

        return code.ToString();
    }

    private static byte ClosestStandardPaletteIndex(byte red, byte green, byte blue)
    {
        var closestIndex = 0;
        var closestDistance = int.MaxValue;

        for (var index = 0; index < StandardPalette.Length; index++)
        {
            var candidate = StandardPalette[index];
            var distance = ColorDistance(candidate.Red, candidate.Green, candidate.Blue, red, green, blue);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = index;
            }
        }

        return (byte)closestIndex;
    }

    // The low-cost weighted RGB distance Spectre.Console uses (https://stackoverflow.com/a/9085524),
    // kept identical (square root omitted; comparisons only) so downgrades pick the same color.
    private static int ColorDistance(byte firstRed, byte firstGreen, byte firstBlue, byte secondRed, byte secondGreen, byte secondBlue)
    {
        var redMean = (firstRed + secondRed) / 2.0;
        var redDelta = firstRed - secondRed;
        var greenDelta = firstGreen - secondGreen;
        var blueDelta = firstBlue - secondBlue;

        return (((int)(512 + redMean) * redDelta * redDelta) >> 8)
            + (4 * greenDelta * greenDelta)
            + (((int)(767 - redMean) * blueDelta * blueDelta) >> 8);
    }
}
