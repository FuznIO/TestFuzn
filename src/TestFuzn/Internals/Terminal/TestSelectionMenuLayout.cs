using System.Globalization;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class TestSelectionMenuLayout
{
    public const int MinimumWidthForLogo = LogoWidget.MinimumWidthForLogo;

    public const int MinimumListRowsBesideBanner = 15;

    private const int FixedRowCount = 5;
    private const int BannerRowCount = LogoWidget.BannerHeight + 1;

    private const int ColumnGap = 2;

    private const string HighlightStyle = "bold #ff9d3d";
    private const string SecondaryStyle = "dim";
    private const string MatchStyle = "underline";
    private const string WarningStyle = "yellow";

    private const string TitleMarkup = "[bold]Select a test to run[/]";
    private const string FilterLabel = "Filter: ";
    private const string Caret = "▏";
    private const string Pointer = "▸";

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    private static readonly KeyHint[] FooterHints =
    {
        new KeyHint("↑↓", "move"),
        new KeyHint("enter", "run"),
        new KeyHint("type", "to filter"),
        new KeyHint("esc", "quit"),
        new KeyHint("pgup/pgdn", "page"),
        new KeyHint("home/end", "first/last")
    };

    public static LogoVariant SelectLogoVariant(int width, int height)
    {
        if (width < MinimumWidthForLogo)
            return LogoVariant.None;

        if (height - FixedRowCount - BannerRowCount >= MinimumListRowsBesideBanner)
            return LogoVariant.Banner;

        return LogoVariant.Compact;
    }

    public static int MeasureListRowCount(int width, int height)
    {
        var rowCount = height - FixedRowCount;
        if (SelectLogoVariant(width, height) == LogoVariant.Banner)
            rowCount -= BannerRowCount;

        return Math.Max(rowCount, 0);
    }

    public static IReadOnlyList<RenderedLine> Render(TestSelectionMenuState state, int width, int height, ColorMode colorMode)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state), "State cannot be null.");

        if (width < 1)
            return Array.Empty<RenderedLine>();

        var lines = new List<RenderedLine>();

        var logoVariant = SelectLogoVariant(width, height);
        if (logoVariant == LogoVariant.Banner)
        {
            lines.AddRange(LogoWidget.Render(LogoWidget.BannerWidth, LogoWidget.BannerHeight, colorMode));
            lines.Add(BlankLine);
        }

        lines.Add(RenderTitleLine(width, colorMode, includeCompactLogo: logoVariant == LogoVariant.Compact));
        lines.Add(BlankLine);

        AddListRows(lines, state, MeasureListRowCount(width, height), width, colorMode);

        lines.Add(BlankLine);
        lines.Add(RenderStatusLine(state, width, colorMode));

        if (height >= 1)
        {
            var contentHeight = height - 1;
            if (lines.Count > contentHeight)
                lines.RemoveRange(contentHeight, lines.Count - contentHeight);

            while (lines.Count < contentHeight)
                lines.Add(BlankLine);
        }

        lines.AddRange(KeyHintBarWidget.Render(FooterHints, width, colorMode));
        return lines;
    }

    private static RenderedLine RenderTitleLine(int width, ColorMode colorMode, bool includeCompactLogo)
    {
        if (!includeCompactLogo)
            return MarkupText.RenderTruncated(TitleMarkup, width, colorMode);

        var logo = LogoWidget.Render(LogoWidget.CompactWidth, LogoWidget.CompactHeight, colorMode);
        var title = MarkupText.RenderFitted(TitleMarkup, width - LogoWidget.CompactWidth - ColumnGap, colorMode);
        return new RenderedLine(title.Text + new string(' ', ColumnGap) + logo[0].Text, width);
    }

    private static void AddListRows(List<RenderedLine> lines, TestSelectionMenuState state, int rowCount, int width, ColorMode colorMode)
    {
        var numberWidth = state.TestNames.Count.ToString(CultureInfo.InvariantCulture).Length;
        var highlightedTest = state.HighlightedTest;

        for (var row = 0; row < rowCount; row++)
        {
            var position = state.FirstVisibleMatch + row;
            if (position < 0 || position >= state.MatchingTests.Count)
            {
                lines.Add(BlankLine);
                continue;
            }

            var testIndex = state.MatchingTests[position];
            var isHighlighted = highlightedTest != null && highlightedTest.Value == testIndex;
            lines.Add(MarkupText.RenderTruncated(RowMarkup(state, testIndex, numberWidth, isHighlighted), width, colorMode));
        }
    }

    private static string RowMarkup(TestSelectionMenuState state, int testIndex, int numberWidth, bool isHighlighted)
    {
        var number = (testIndex + 1).ToString(CultureInfo.InvariantCulture).PadLeft(numberWidth);
        var name = NameMarkup(state, state.TestNames[testIndex]);

        if (isHighlighted)
            return "[" + HighlightStyle + "]" + Pointer + " " + number + "  " + name + "[/]";

        return "  [" + SecondaryStyle + "]" + number + "[/]  " + name;
    }

    private static string NameMarkup(TestSelectionMenuState state, string name)
    {
        var filter = state.Filter;
        if (filter.Length == 0 || state.EnteredTestNumber != null)
            return MarkupParser.Escape(name);

        var start = name.IndexOf(filter, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return MarkupParser.Escape(name);

        var end = start + filter.Length;
        if (SplitsSurrogatePair(name, start) || SplitsSurrogatePair(name, end))
            return MarkupParser.Escape(name);

        return MarkupParser.Escape(name.Substring(0, start))
            + "[" + MatchStyle + "]" + MarkupParser.Escape(name.Substring(start, filter.Length)) + "[/]"
            + MarkupParser.Escape(name.Substring(end));
    }

    private static bool SplitsSurrogatePair(string text, int position)
    {
        return position > 0 && position < text.Length && char.IsHighSurrogate(text[position - 1]) && char.IsLowSurrogate(text[position]);
    }

    private static RenderedLine RenderStatusLine(TestSelectionMenuState state, int width, ColorMode colorMode)
    {
        var markup = FilterLabel + MarkupParser.Escape(state.Filter) + "[" + SecondaryStyle + "]" + Caret + "[/]  " + CountMarkup(state);
        return MarkupText.RenderTruncated(markup, width, colorMode);
    }

    private static string CountMarkup(TestSelectionMenuState state)
    {
        var testCount = state.TestNames.Count;
        if (testCount == 0)
            return "[" + WarningStyle + "]no tests[/]";

        var enteredTestNumber = state.EnteredTestNumber;
        var highlightedTest = state.HighlightedTest;
        if (enteredTestNumber != null && highlightedTest != null && highlightedTest.Value == enteredTestNumber.Value - 1)
            return "[" + SecondaryStyle + "]test " + enteredTestNumber.Value.ToString(CultureInfo.InvariantCulture) + " of " + testCount.ToString(CultureInfo.InvariantCulture) + "[/]";

        if (state.Filter.Length == 0 || enteredTestNumber != null)
            return "[" + SecondaryStyle + "]" + TestCountText(testCount) + "[/]";

        var matchCount = state.MatchingTests.Count;
        if (matchCount == 0)
            return "[" + WarningStyle + "]no matching tests[/]";

        return "[" + SecondaryStyle + "]" + matchCount.ToString(CultureInfo.InvariantCulture) + " of " + TestCountText(testCount) + "[/]";
    }

    private static string TestCountText(int testCount)
    {
        if (testCount == 1)
            return "1 test";

        return testCount.ToString(CultureInfo.InvariantCulture) + " tests";
    }
}
