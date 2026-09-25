using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class KeyHintBarWidget
{
    private const string SeparatorMarkup = "[dim] · [/]";
    private const int SeparatorWidth = 3;

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<KeyHint> hints, int width, ColorMode colorMode)
    {
        if (hints == null)
            throw new ArgumentNullException(nameof(hints), "Hints cannot be null.");

        if (width < 1)
            return Array.Empty<RenderedLine>();

        var markup = new StringBuilder();
        var usedWidth = 0;
        for (var index = 0; index < hints.Count; index++)
        {
            var hintMarkup = "[bold]" + hints[index].Key + "[/] " + hints[index].Description;
            var hintWidth = MarkupText.Measure(hintMarkup);

            if (index == 0)
            {
                if (hintWidth > width)
                    return new[] { MarkupText.RenderTruncated(hintMarkup, width, colorMode) };

                markup.Append(hintMarkup);
                usedWidth = hintWidth;
                continue;
            }

            if (usedWidth + SeparatorWidth + hintWidth > width)
                break;

            markup.Append(SeparatorMarkup).Append(hintMarkup);
            usedWidth += SeparatorWidth + hintWidth;
        }

        return new[] { MarkupText.RenderTruncated(markup.ToString(), width, colorMode) };
    }
}
