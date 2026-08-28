using System.Globalization;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Lays out the standalone runner's test selection menu as one full-screen frame: the logo on
/// top, a title, the numbered test list with the highlighted test marked, a status line with
/// the filter typed so far and the match count, and a key-hint footer that owns the window's
/// last row. Pure composition of the widgets in this namespace over a
/// <see cref="TestSelectionMenuState"/> (no console, no clock), so an unchanged state renders
/// an identical frame, which the <see cref="FrameRenderer"/> diff depends on. The logo follows
/// the dashboard's rule and then grows with the height: dropped below
/// <see cref="MinimumWidthForLogo"/> columns, the compact wordmark right-aligned on the title
/// row (costing no row) from there, promoted to the full banner when the list still keeps
/// <see cref="MinimumListRowsBesideBanner"/> rows beside it — <see cref="SelectLogoVariant"/>
/// answers which, and <see cref="MeasureListRowCount"/> how many rows the list gets, which the
/// caller hands to the state before handling keys so paging and the window follow the terminal
/// size. The list shows the state's window of matches, one test per row — a pointer plus the
/// test's number, dimmed on the other rows, and its name with the matched text underlined on a
/// text search — the highlighted row styled whole, names escaped so brackets render literally
/// and truncated with an ellipsis when too wide, so no emitted line is ever wider than the
/// given width. The frame is fitted to the height as the dashboard's is: content is clipped to
/// the rows above the footer and padded down so the footer lands on the last row; a height
/// below 1 leaves the frame unclipped and unpadded, and a width below 1 renders nothing.
/// Alternate-screen entry/exit and the poll loop are the caller's job. Stateless and thread-safe.
/// </summary>
internal static class TestSelectionMenuLayout
{
    /// <summary>Below this width no logo is shown, as on the dashboard.</summary>
    public const int MinimumWidthForLogo = LiveDashboardLayout.MinimumWidthForLogo;

    /// <summary>The rows the list must keep beside the full banner for the banner to be shown; fewer shows the compact wordmark.</summary>
    public const int MinimumListRowsBesideBanner = 15;

    // The rows around the list at any size: the title, the blank below it, the blank above the
    // status line, the status line and the footer. The banner adds its rows plus a blank.
    private const int FixedRowCount = 5;
    private const int BannerRowCount = LogoWidget.BannerHeight + 1;

    // Columns between the title and the compact logo on the title row.
    private const int ColumnGap = 2;

    // The menu's palette as markup style constants, in the dashboard's warm family.
    private const string HighlightStyle = "bold #ff9d3d";
    private const string SecondaryStyle = "dim";
    private const string MatchStyle = "underline";
    private const string WarningStyle = "yellow";

    private const string TitleMarkup = "[bold]Select a test to run[/]";
    private const string FilterLabel = "Filter: ";
    private const string Caret = "▏";
    private const string Pointer = "▸";

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    // Most important first: the footer drops hints from the end when they do not fit.
    private static readonly KeyHint[] FooterHints =
    {
        new KeyHint("↑↓", "move"),
        new KeyHint("enter", "run"),
        new KeyHint("type", "to filter"),
        new KeyHint("esc", "quit"),
        new KeyHint("pgup/pgdn", "page"),
        new KeyHint("home/end", "first/last")
    };

    /// <summary>
    /// The logo variant for the window size: none below <see cref="MinimumWidthForLogo"/>
    /// columns, the banner when the list keeps at least <see cref="MinimumListRowsBesideBanner"/>
    /// rows beside it, else the compact wordmark.
    /// </summary>
    public static LogoVariant SelectLogoVariant(int width, int height)
    {
        if (width < MinimumWidthForLogo)
            return LogoVariant.None;

        if (height - FixedRowCount - BannerRowCount >= MinimumListRowsBesideBanner)
            return LogoVariant.Banner;

        return LogoVariant.Compact;
    }

    /// <summary>
    /// The rows the list gets at the window size — the height minus the fixed rows and the
    /// banner's, never negative — the value the caller sets as the state's
    /// <see cref="TestSelectionMenuState.VisibleRowCount"/> before handling keys.
    /// </summary>
    public static int MeasureListRowCount(int width, int height)
    {
        var rowCount = height - FixedRowCount;
        if (SelectLogoVariant(width, height) == LogoVariant.Banner)
            rowCount -= BannerRowCount;

        return Math.Max(rowCount, 0);
    }

    /// <summary>
    /// Renders the menu frame for the state at the window size. Returns one <see cref="RenderedLine"/>
    /// per terminal row, ready for <see cref="FrameBuffer.AddLines(IEnumerable{RenderedLine})"/>:
    /// exactly <paramref name="height"/> rows for a height of 1 or more, the content clipped or
    /// padded to the rows above the footer on the last row; the unclipped content plus the
    /// footer for a smaller height. A width below 1 renders nothing. The list rows are the
    /// state's window, <see cref="MeasureListRowCount"/> of them, the ones past the last match blank.
    /// </summary>
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

        // The footer owns the last row: content is cut to the rows above it and padded down to it.
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

    // The title, with the compact logo right-aligned on the same row when requested — the
    // title is fitted to the columns left of the reserved logo area, so the two never overlap.
    private static RenderedLine RenderTitleLine(int width, ColorMode colorMode, bool includeCompactLogo)
    {
        if (!includeCompactLogo)
            return MarkupText.RenderTruncated(TitleMarkup, width, colorMode);

        var logo = LogoWidget.Render(LogoWidget.CompactWidth, LogoWidget.CompactHeight, colorMode);
        var title = MarkupText.RenderFitted(TitleMarkup, width - LogoWidget.CompactWidth - ColumnGap, colorMode);
        return new RenderedLine(title.Text + new string(' ', ColumnGap) + logo[0].Text, width);
    }

    // One row per visible position of the state's window: the match there, or blank past the
    // last match. Numbers are right-aligned to the widest number's digits.
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

    // A pointer column, the test's number and its name; the highlighted row is styled whole,
    // the other rows dim their number.
    private static string RowMarkup(TestSelectionMenuState state, int testIndex, int numberWidth, bool isHighlighted)
    {
        var number = (testIndex + 1).ToString(CultureInfo.InvariantCulture).PadLeft(numberWidth);
        var name = NameMarkup(state, state.TestNames[testIndex]);

        if (isHighlighted)
            return "[" + HighlightStyle + "]" + Pointer + " " + number + "  " + name + "[/]";

        return "  [" + SecondaryStyle + "]" + number + "[/]  " + name;
    }

    // The name escaped so its brackets render literally, with the first occurrence of a text
    // search underlined. A number entry and an empty filter underline nothing, and so does a
    // match whose edge would split a surrogate pair — a lone half typed so far can land inside
    // one — since styling between the halves would break the character.
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

    // The filter typed so far behind its label with a caret where the next character lands
    // (the real cursor is hidden), then the match count: the test count, the matches out of
    // it on a text search, the named test on a number entry while the highlight is still on it
    // (navigation can move the highlight away — the list stays complete, so the plain count
    // is reported then), or a warning when nothing matches.
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
