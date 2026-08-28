using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders a single-line progress bar of filled █ and empty ░ cells, optionally followed by a
/// right-aligned percent label (<c>███░░  50%</c>). The fraction clamps to 0..1 — NaN counts
/// as 0 — and both the filled cell count and the percent round half away from zero. The label
/// takes 5 columns (a separating space plus "100%" right-aligned in 4) and is omitted when the
/// width leaves no room for at least one bar cell beside it; a width below 1 renders nothing.
/// The optional bar style is markup tag words (e.g. "green" or "bold #ff8800") applied to the
/// filled cells; without one the bar is plain text in every color mode. Stateless and
/// thread-safe.
/// </summary>
internal static class ProgressBarWidget
{
    private const char FilledCell = '█';
    private const char EmptyCell = '░';
    private const int LabelWidth = 5;

    public static IReadOnlyList<RenderedLine> Render(double fraction, int width, ColorMode colorMode, bool showPercentLabel = true, string? barStyle = null)
    {
        if (width < 1)
            return Array.Empty<RenderedLine>();

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

        // Fitted by construction; rendering through MarkupText keeps an invalid bar style (which
        // parses as literal text) from pushing the line past the width.
        return new[] { MarkupText.RenderTruncated(markup.ToString(), width, colorMode) };
    }
}
