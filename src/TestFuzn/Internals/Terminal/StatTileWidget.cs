namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders a two-line stat tile: a dimmed label above the value in bold. Both are markup
/// strings, truncated with an ellipsis when wider than the given width, and caller styling
/// composes with the emphasis — a colored value stays colored and gains bold. Lines are not
/// padded to the width; a width below 1 renders nothing. Stateless and thread-safe.
/// </summary>
internal static class StatTileWidget
{
    public static IReadOnlyList<RenderedLine> Render(string? label, string? value, int width, ColorMode colorMode)
    {
        if (width < 1)
            return Array.Empty<RenderedLine>();

        return new[]
        {
            MarkupText.RenderTruncated("[dim]" + label + "[/]", width, colorMode),
            MarkupText.RenderTruncated("[bold]" + value + "[/]", width, colorMode)
        };
    }
}
