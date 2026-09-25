namespace Fuzn.TestFuzn.Internals.Terminal;

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
