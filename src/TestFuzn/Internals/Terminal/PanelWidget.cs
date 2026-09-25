namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class PanelWidget
{
    private const int MinimumWidth = 4;

    public const int ContentOverhead = 4;

    private const int HeaderOverhead = 5;

    public static IReadOnlyList<RenderedLine> Render(string? header, IReadOnlyList<string?> contentLines, int width, ColorMode colorMode)
    {
        if (contentLines == null)
            throw new ArgumentNullException(nameof(contentLines), "Content lines cannot be null.");

        if (width < MinimumWidth)
            return Array.Empty<RenderedLine>();

        var lines = new List<RenderedLine>(contentLines.Count + 2);
        lines.Add(RenderTopBorder(header, width, colorMode));

        var innerWidth = width - ContentOverhead;
        foreach (var contentLine in contentLines)
            lines.Add(new RenderedLine("│ " + MarkupText.RenderFitted(contentLine, innerWidth, colorMode).Text + " │", width));

        lines.Add(RenderBottomBorder(width));
        return lines;
    }

    public static IReadOnlyList<RenderedLine> Render(string? header, IReadOnlyList<RenderedLine> contentLines, int width, ColorMode colorMode)
    {
        if (contentLines == null)
            throw new ArgumentNullException(nameof(contentLines), "Content lines cannot be null.");

        if (width < MinimumWidth)
            return Array.Empty<RenderedLine>();

        var lines = new List<RenderedLine>(contentLines.Count + 2);
        lines.Add(RenderTopBorder(header, width, colorMode));

        var innerWidth = width - ContentOverhead;
        foreach (var contentLine in contentLines)
        {
            if (contentLine.Width > innerWidth)
                throw new ArgumentException($"Content line width {contentLine.Width} exceeds the panel's inner width {innerWidth}; render inner widgets at the panel's inner width.", nameof(contentLines));

            lines.Add(new RenderedLine("│ " + contentLine.Text + new string(' ', innerWidth - contentLine.Width) + " │", width));
        }

        lines.Add(RenderBottomBorder(width));
        return lines;
    }

    private static RenderedLine RenderTopBorder(string? header, int width, ColorMode colorMode)
    {
        var headerWidth = Math.Min(MarkupText.Measure(header), width - HeaderOverhead);
        if (headerWidth < 1)
            return new RenderedLine("╭" + new string('─', width - 2) + "╮", width);

        var renderedHeader = MarkupText.RenderTruncated(header, headerWidth, colorMode);
        return new RenderedLine("╭─ " + renderedHeader.Text + " " + new string('─', width - HeaderOverhead - renderedHeader.Width) + "╮", width);
    }

    private static RenderedLine RenderBottomBorder(int width)
    {
        return new RenderedLine("╰" + new string('─', width - 2) + "╯", width);
    }
}
