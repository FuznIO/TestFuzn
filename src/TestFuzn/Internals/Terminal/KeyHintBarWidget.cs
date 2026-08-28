using System.Text;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Renders a single-line footer of key hints — each key in bold followed by its description,
/// separated by a dimmed " · " (<c>q quit · f filter</c>). Rendering stops at the first hint
/// that does not fit entirely, dropping it and the rest; when even the first hint is too wide
/// it truncates with an ellipsis. No hints renders an empty line. The line is not padded to
/// the width; a width below 1 renders nothing. Stateless and thread-safe.
/// </summary>
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

        // The accumulated markup measures at most the width by construction, so this truncation
        // is a no-op — rendering through MarkupText is what sanitizes control characters in the
        // hints (a line break in a description would otherwise split the returned single line).
        return new[] { MarkupText.RenderTruncated(markup.ToString(), width, colorMode) };
    }
}
