namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders a box with rounded borders and an optional header in the top border:
/// <c>╭─ Header ──╮</c> above content rows framed as <c>│ content │</c>, closed with
/// <c>╰────╯</c>. The header is a markup string, and content comes in two forms with one
/// overload each: markup content lines, each fitted to the panel's inner width — the given
/// width minus 4 for the borders and their one-space padding — truncated with an ellipsis when
/// too wide and space-padded when narrower; or pre-rendered <see cref="RenderedLine"/> content
/// (other widgets' output), each line padded right to the inner width by its known
/// <see cref="RenderedLine.Width"/> — never re-measured, never truncated. A pre-rendered line
/// wider than the inner width throws <see cref="ArgumentException"/>: the caller renders inner
/// widgets at the panel's inner width by construction, so an overlong line is a caller bug, and
/// failing loud at development time beats silently corrupting the frame. Either way every
/// emitted line is exactly the given width. The header truncates with an ellipsis when it
/// cannot fit and is dropped entirely below 6 columns (the top border then renders plain); a
/// panel narrower than 4 columns renders nothing. Stateless and thread-safe.
/// </summary>
internal static class PanelWidget
{
    private const int MinimumWidth = 4;

    // The borders and their one-space padding around a content row: "│ " before it, " │" after.
    private const int ContentOverhead = 4;

    // The top border's fixed columns around a header: "╭─ " before it, " " after it, "╮" last.
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
