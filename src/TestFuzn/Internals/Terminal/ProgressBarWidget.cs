using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class ProgressBarWidget
{
    private const char FilledCell = '█';
    private const char EmptyCell = '░';
    private const int LabelWidth = 5;

    public static IReadOnlyList<RenderedLine> Render(double fraction, int width, ColorMode colorMode, bool showPercentLabel = true, string? barStyle = null)
    {
        if (width < 1)
            return Array.Empty<RenderedLine>();

        return new[] { MarkupText.RenderTruncated(RenderMarkup(fraction, width, showPercentLabel, barStyle), width, colorMode) };
    }

    public static string RenderMarkup(double fraction, int width, bool showPercentLabel = true, string? barStyle = null)
    {
        if (width < 1)
            return string.Empty;

        var clamped = fraction;
        if (double.IsNaN(clamped) || clamped < 0)
            clamped = 0;
        else if (clamped > 1)
            clamped = 1;

        var showLabel = showPercentLabel && width - LabelWidth >= 1;
        var barWidth = showLabel ? width - LabelWidth : width;
        var filledCount = (int)Math.Round(clamped * barWidth, MidpointRounding.AwayFromZero);

        var markup = new StringBuilder();
        if (filledCount > 0 && !string.IsNullOrEmpty(barStyle))
            markup.Append('[').Append(barStyle).Append(']').Append(FilledCell, filledCount).Append("[/]");
        else
            markup.Append(FilledCell, filledCount);

        markup.Append(EmptyCell, barWidth - filledCount);

        if (showLabel)
        {
            var percent = (int)Math.Round(clamped * 100, MidpointRounding.AwayFromZero);
            markup.Append(' ').Append((percent + "%").PadLeft(4));
        }

        return markup.ToString();
    }
}
