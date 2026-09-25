using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class MarkupRenderer
{
    private static readonly (byte Red, byte Green, byte Blue)[] StandardPalette =
    {
        (0, 0, 0),
        (128, 0, 0),
        (0, 128, 0),
        (128, 128, 0),
        (0, 0, 128),
        (128, 0, 128),
        (0, 128, 128),
        (192, 192, 192),
        (128, 128, 128),
        (255, 0, 0),
        (0, 255, 0),
        (255, 255, 0),
        (0, 0, 255),
        (255, 0, 255),
        (0, 255, 255),
        (255, 255, 255)
    };

    public static string Render(string? markup, ColorMode colorMode)
    {
        if (colorMode == ColorMode.None)
            return MarkupParser.StripMarkup(markup);

        return Render(MarkupParser.Parse(markup), colorMode);
    }

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
