using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// The error log and help overlay goldens of <see cref="LiveDashboardLayout"/> — the view
/// state's <see cref="LiveDashboardView.ErrorLog"/> and <see cref="LiveDashboardViewState.ShowHelp"/>
/// — over the snapshots of the main file and the ones below: the log's title, header and
/// grouped entries at 120×40 and 80×24, the scroll indicator over a 27-entry log at a
/// scrolled position, past the end and where a group header rides with a mid-group entry
/// or closes the page, the last page — the fullest page that still shows the log's end
/// whole — where a page from one entry earlier would overrun the rows, over the 27-entry
/// log, over three nine-line entries and past an over-tall entry mid-log, and the last entry
/// alone cut from the bottom when no page holds it, the no-errors and no-scenario notices,
/// the unlisted note, messages wrapped without the ticker's cap, hostile names and messages,
/// the TrueColor styling, and the help panel centred over the overview, the detail, the log
/// and two columns — the rows outside it untouched, the covered rows spliced to exactly the
/// width, the footer never covered, the width and height caps on a narrow frame, its right
/// edge short of the logo's gap at every width that carries one — plus hostile sweeps of
/// both. Derived by hand as the main file's summary describes: a log entry is the ticker's
/// line without the step name and its dot, indented two columns under its <c>▸</c> group
/// header; the help panel is 35 columns (the widest line, <c>↑ ↓  select step / scroll</c>
/// after a nine-column key column, and the borders) by 12 rows, at the floor of half the
/// leftover columns and rows.
/// </summary>
public partial class LiveDashboardLayoutTests
{
    /// <summary>The error log's footer with its last two hints given up: 51 columns.</summary>
    private const string ErrorLogFooterAt51 = "Esc back · ↑↓ scroll · 1 overview · 2 step · q quit";

    /// <summary>The rich snapshot's errors as the log lays them out — the ticker's lines without the step name — 81 and 61 columns before the group indent.</summary>
    private const string CheckoutLogEntry = "30× (2.0/s)  Connection refused (localhost:7058)   first 1m 12s ago · last 2s ago";
    private const string AddToCartLogEntry = " 2× (0.0/s)  Timeout after 30s   first 45s ago · last 20s ago";

    /// <summary>The help panel's width: nine columns of key, two of gap, twenty of the widest description and the borders.</summary>
    private const int HelpPanelWidth = 35;

    /// <summary>The columns between a title line's text and the compact logo it carries.</summary>
    private const int TitleLogoGap = 2;

    /// <summary>The error log's view state at the given scroll, every sample in the window, not paused, no help.</summary>
    private static LiveDashboardViewState LogView(int errorLogScroll = 0)
    {
        return DefaultView with { View = LiveDashboardView.ErrorLog, ErrorLogScroll = errorLogScroll };
    }

    /// <summary>
    /// The width of the error log footer at the given frame width, from the rule the class
    /// summary gives: the widest of 70, 61, 51, 42, 29, 17 and 6 that fits, and the width
    /// itself below 6.
    /// </summary>
    private static int ExpectedErrorLogFooterWidth(int width)
    {
        foreach (var footerWidth in new[] { 70, 61, 51, 42, 29, 17, 6 })
        {
            if (footerWidth <= width)
                return footerWidth;
        }

        return width;
    }

    /// <summary>The width of the help footer at the given frame width: 28, 16 or 6, whichever fits first, and the width itself below 6.</summary>
    private static int ExpectedHelpFooterWidth(int width)
    {
        foreach (var footerWidth in new[] { 28, 16, 6 })
        {
            if (footerWidth <= width)
                return footerWidth;
        }

        return width;
    }

    /// <summary>
    /// A run with 27 errors E1 … E27 over four steps — E1 to E5 on Browse, E6 to E10 on Add
    /// to cart, E11 to E20 on Checkout, E21 to E27 on Place order — En counted n times and
    /// last seen n seconds before the ring's origin, so E1 is the most recently active and the
    /// log's order is E1 to E27 with the groups in that order too; no sample, so no ages.
    /// </summary>
    private static LiveMetricsSnapshot LogSnapshot()
    {
        var errors = new LiveErrorEntry[27];
        for (var index = 0; index < errors.Length; index++)
        {
            var number = index + 1;
            var stepName = "Browse";
            if (number > 20)
                stepName = "Place order";
            else if (number > 10)
                stepName = "Checkout";
            else if (number > 5)
                stepName = "Add to cart";

            errors[index] = new LiveErrorEntry { StepName = stepName, Message = "E" + number, Count = number, FirstSeen = SampleTime(-60), LastSeen = SampleTime(-number) };
        }

        return new LiveMetricsSnapshot { ScenarioName = "Log", Errors = errors };
    }

    /// <summary>
    /// Five errors whose ticker order interleaves three steps — E1 Checkout, E2 Browse, E3
    /// Checkout, E4 Add to cart, E5 Browse, En last seen n seconds before the origin — so
    /// the log's grouping shows: Checkout first with E1 and E3, Browse with E2 and E5, Add to
    /// cart with E4.
    /// </summary>
    private static LiveMetricsSnapshot GroupSnapshot()
    {
        var stepNames = new[] { "Checkout", "Browse", "Checkout", "Add to cart", "Browse" };
        var errors = new LiveErrorEntry[stepNames.Length];
        for (var index = 0; index < errors.Length; index++)
            errors[index] = new LiveErrorEntry { StepName = stepNames[index], Message = "E" + (index + 1), Count = index + 1, LastSeen = SampleTime(-(index + 1)) };

        return new LiveMetricsSnapshot { ScenarioName = "Group", Errors = errors };
    }

    /// <summary>The log line of the 27-entry run's En: the two-column count, the rate and the message under the group indent.</summary>
    private static string LogEntry(int number)
    {
        return "  " + number.ToString().PadLeft(2) + "× (0.0/s)  E" + number;
    }

    /// <summary>
    /// A run with the given number of errors, up to three, on the Checkout step, each a
    /// 500-character word without a space — the first of a's, the second of b's, the third of
    /// c's — the nth counted n times and last seen n seconds before the ring's origin, so the
    /// log's order is a, b, c; no sample, so no ages. At 80 columns the entries are 78 wide
    /// and each wraps onto nine lines: the prefix <c>1× (0.0/s) </c> alone — the last space
    /// that fits is the one after the rate — then, under the 12-column indent of the count,
    /// its ×, the rate and the separators, seven lines of 66 characters and one of 38.
    /// </summary>
    private static LiveMetricsSnapshot TallSnapshot(int entryCount)
    {
        var errors = new LiveErrorEntry[entryCount];
        for (var index = 0; index < errors.Length; index++)
            errors[index] = new LiveErrorEntry { StepName = "Checkout", Message = new string((char)('a' + index), 500), Count = index + 1, FirstSeen = SampleTime(-60), LastSeen = SampleTime(-(index + 1)) };

        return new LiveMetricsSnapshot { ScenarioName = "Tall", Errors = errors };
    }

    /// <summary>A continuation line of a tall entry: the group indent, the 12-column entry indent and the given run of the entry's letter.</summary>
    private static string TallEntryLine(char letter, int count)
    {
        return "  " + Spaces(12) + new string(letter, count);
    }

    private static string GroupHeader(string stepName)
    {
        return LiveDashboardLayout.Pointer + " " + stepName;
    }

    /// <summary>One content row of the help panel: the key padded to nine columns, two spaces and the description padded to twenty, between the borders.</summary>
    private static string HelpPanelLine(string key, string description)
    {
        return Box(key.PadRight(9) + "  " + description.PadRight(20));
    }

    /// <summary>The help panel's twelve rows at its natural width.</summary>
    private static string[] HelpPanelRows()
    {
        return new[]
        {
            PanelTop("? help", HelpPanelWidth),
            HelpPanelLine("1", "overview"),
            HelpPanelLine("2 ⏎", "step detail"),
            HelpPanelLine("3", "error log"),
            HelpPanelLine("↑ ↓", "select step / scroll"),
            HelpPanelLine("PgUp PgDn", "page the error log"),
            HelpPanelLine("Esc", "close help / back"),
            HelpPanelLine("p", "pause"),
            HelpPanelLine("+ -", "time window"),
            HelpPanelLine("?", "help"),
            HelpPanelLine("q", "quit"),
            Bottom(HelpPanelWidth)
        };
    }

    /// <summary>The columns [start, end) of a plain rendered line — one character per column — padded with spaces past its end.</summary>
    private static string PlainColumns(RenderedLine line, int start, int end)
    {
        var text = line.Text;
        if (text.Length < end)
            text = text.PadRight(end);

        return text.Substring(start, end - start);
    }

    /// <summary>
    /// The help panel over a plain frame: the rows above and below the panel are the frame's,
    /// the footer is the help footer, and every covered row is the frame's columns before the
    /// panel, the panel row and the frame's columns after it — exactly the width.
    /// </summary>
    private static void AssertHelpOverlay(IReadOnlyList<RenderedLine> frame, IReadOnlyList<RenderedLine> help, int width, int top, int left)
    {
        var panel = HelpPanelRows();
        Assert.HasCount(frame.Count, help);
        for (var row = 0; row < top; row++)
            AssertLine(frame[row].Text, frame[row].Width, help[row]);

        for (var row = 0; row < panel.Length; row++)
            AssertLine(PlainColumns(frame[top + row], 0, left) + panel[row] + PlainColumns(frame[top + row], left + HelpPanelWidth, width), width, help[top + row]);

        for (var row = top + panel.Length; row < frame.Count - 1; row++)
            AssertLine(frame[row].Text, frame[row].Width, help[row]);

        AssertLine(HelpFooter, 28, help[help.Count - 1]);
    }

    [Test]
    public async Task Verify_error_log_golden_frame_at_120x40()
    {
        // The rich scenario's two errors on two steps: the Checkout error is the most recently
        // active, so its group comes first. The title carries the phase label, as the detail's
        // does; the header counts both entries; each entry is the ticker's line without the
        // step name and its dot — 81 and 61 columns — two columns in under its group header.
        await Scenario()
            .Step("The frame is the title with the phase, the header, a group header and its entry per step, blank rows and the error log's footer", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(), 120, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine(RichDetailTitle + Spaces(49) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine("errors · 1–2 of 2 · Esc back", 28, lines[1]);
                AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                AssertLine("  " + CheckoutLogEntry, 83, lines[3]);
                AssertLine(GroupHeader("Add to cart"), 13, lines[4]);
                AssertLine("  " + AddToCartLogEntry, 63, lines[5]);
                AssertLine(string.Empty, 0, lines[6]);
                AssertLine(string.Empty, 0, lines[38]);
                AssertLine(ErrorLogFooter, 70, lines[39]);
                AssertMaximumWidth(120, lines);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain("Steps", line.Text);
                    Assert.DoesNotContain("Requests", line.Text);
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
                    Assert.DoesNotContain(" · Connection", line.Text);
                }
            })
            .Step("TrueColor: the header's word in the accent and its dots and hint dim around the plain indicator, the group header in the accent, and the entry styled as the ticker's — the count red, the rate and the ages dim", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(), 120, 40, ColorMode.TrueColor);

                AssertLine(Sgr(BoldAccent, "errors") + " " + Sgr(Dim, "·") + " 1–2 of 2 " + Sgr(Dim, "· Esc back"), 28, lines[1]);
                AssertLine(Sgr(BoldAccent, GroupHeader("Checkout")), 10, lines[2]);
                AssertLine("  " + Sgr(Red, "30×") + " " + Sgr(Dim, "(2.0/s)") + "  Connection refused (localhost:7058)   " + Sgr(Dim, "first 1m 12s ago · last 2s ago"), 83, lines[3]);
                AssertMaximumWidth(120, lines);
            })
            .Step("The paused badge sits on the title line before the phase, and with two scenarios the log is still the first scenario's alone", context =>
            {
                var paused = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView() with { IsPaused = true }, 120, 40, ColorMode.None);
                AssertLine("Checkout flow  ● Running · " + LiveDashboardLayout.PausedBadgeText + " · sim 1/2: Gradual Load 10→50 rps" + Spaces(38) + "  ⚡ TestFuzn", 120, paused[0]);
                AssertLine("errors · 1–2 of 2 · Esc back", 28, paused[1]);

                var two = LiveDashboardLayout.Render(new[] { RichSnapshot(), HostileSnapshot() }, LogView(), 160, 40, ColorMode.None);
                AssertLine(RichDetailTitle + Spaces(89) + "  ⚡ TestFuzn", 160, two[0]);
                AssertLine("errors · 1–2 of 2 · Esc back", 28, two[1]);
                AssertLine("  " + CheckoutLogEntry, 83, two[3]);
                foreach (var line in two)
                    Assert.DoesNotContain("Hostile", line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_log_golden_frame_at_80x24()
    {
        // At 80 columns the entries are 78 wide: the 81-column Checkout entry breaks at the
        // last space that fits, before the last "ago", and the rest continues under the
        // message — an indent of the two-column count, its ×, the seven-column rate and the
        // separators, 13. The footer's seventy columns fit.
        await Scenario()
            .Step("The wrapped entry's last word sits on its own line under the message, the ages split over the break", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(), 80, 24, ColorMode.None);

                Assert.HasCount(24, lines);
                AssertLine(RichDetailTitle + Spaces(9) + "  ⚡ TestFuzn", 80, lines[0]);
                AssertLine("errors · 1–2 of 2 · Esc back", 28, lines[1]);
                AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                AssertLine("  30× (2.0/s)  Connection refused (localhost:7058)   first 1m 12s ago · last 2s", 79, lines[3]);
                AssertLine("  " + Spaces(13) + "ago", 18, lines[4]);
                AssertLine(GroupHeader("Add to cart"), 13, lines[5]);
                AssertLine("  " + AddToCartLogEntry, 63, lines[6]);
                AssertLine(string.Empty, 0, lines[7]);
                AssertLine(string.Empty, 0, lines[22]);
                AssertLine(ErrorLogFooter, 70, lines[23]);
                AssertMaximumWidth(80, lines);
            })
            .Step("Below 80 columns the ages go and below 60 the rates, as in the ticker, and the footer gives up its hints from the right", context =>
            {
                var noAges = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(), 79, 24, ColorMode.None);
                AssertLine("  30× (2.0/s)  Connection refused (localhost:7058)", 50, noAges[3]);
                AssertLine("   2× (0.0/s)  Timeout after 30s", 32, noAges[5]);
                AssertLine(ErrorLogFooter, 70, noAges[23]);

                var bare = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(), 59, 24, ColorMode.None);
                AssertLine("  30× Connection refused (localhost:7058)", 41, bare[3]);
                AssertLine("   2× Timeout after 30s", 23, bare[5]);
                AssertLine(ErrorLogFooterAt51, 51, bare[23]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_log_groups_entries_by_step_in_the_order_of_their_most_recent_error()
    {
        await Scenario()
            .Step("The ticker order E1 to E5 interleaves the steps; the log brings each step's entries together, the groups in the order of their first entry and the entries in the ticker's order inside", context =>
            {
                // Under an unbounded height every entry is in: the title, the header, three
                // group headers and five one-line entries, then the footer on row 10.
                var lines = LiveDashboardLayout.Render(new[] { GroupSnapshot() }, LogView(), 120, 0, ColorMode.None);

                Assert.HasCount(11, lines);
                AssertLine("Group  ● Running" + Spaces(91) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine("errors · 1–5 of 5 · Esc back", 28, lines[1]);
                AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                AssertLine("  1× (0.0/s)  E1", 16, lines[3]);
                AssertLine("  3× (0.0/s)  E3", 16, lines[4]);
                AssertLine(GroupHeader("Browse"), 8, lines[5]);
                AssertLine("  2× (0.0/s)  E2", 16, lines[6]);
                AssertLine("  5× (0.0/s)  E5", 16, lines[7]);
                AssertLine(GroupHeader("Add to cart"), 13, lines[8]);
                AssertLine("  4× (0.0/s)  E4", 16, lines[9]);
                AssertLine(ErrorLogFooter, 70, lines[10]);
            })
            .Step("A tie on the last-seen instant goes to the higher count, as in the ticker, for the entries and so for the groups", context =>
            {
                var tied = new LiveMetricsSnapshot
                {
                    ScenarioName = "Tie",
                    Errors = new[]
                    {
                        new LiveErrorEntry { StepName = "Later", Message = "few", Count = 2, LastSeen = SampleTime(-1) },
                        new LiveErrorEntry { StepName = "Sooner", Message = "many", Count = 9, LastSeen = SampleTime(-1) }
                    }
                };
                var lines = LiveDashboardLayout.Render(new[] { tied }, LogView(), 120, 0, ColorMode.None);

                AssertLine(GroupHeader("Sooner"), 8, lines[2]);
                AssertLine("  9× (0.0/s)  many", 18, lines[3]);
                AssertLine(GroupHeader("Later"), 7, lines[4]);
                AssertLine("  2× (0.0/s)  few", 17, lines[5]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_log_scrolls_by_entry_with_the_indicator_and_clamps_to_the_last_page()
    {
        // The 27-entry run at 120 columns and 18 rows: the title, the header and the footer
        // leave 15 rows for the log, every entry one line and every group header one row.
        // From E15 the log's tail takes the 15 rows exactly, so here the last page is a full
        // one; Verify_error_log_last_page_is_the_fullest_page_that_shows_the_end_whole pins
        // the rule where no page ends full.
        await Scenario()
            .Step("From the top the page holds the first twelve entries with their three group headers: 1–12 of 27, the Checkout header two entries before the end", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(), 120, 18, ColorMode.None);

                Assert.HasCount(18, lines);
                AssertLine("Log  ● Running" + Spaces(93) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine("errors · 1–12 of 27 · Esc back", 30, lines[1]);
                AssertLine(GroupHeader("Browse"), 8, lines[2]);
                AssertLine(LogEntry(1), 17, lines[3]);
                AssertLine(LogEntry(5), 17, lines[7]);
                AssertLine(GroupHeader("Add to cart"), 13, lines[8]);
                AssertLine(LogEntry(6), 17, lines[9]);
                AssertLine(LogEntry(10), 18, lines[13]);
                AssertLine(GroupHeader("Checkout"), 10, lines[14]);
                AssertLine(LogEntry(11), 18, lines[15]);
                AssertLine(LogEntry(12), 18, lines[16]);
                AssertLine(ErrorLogFooter, 70, lines[17]);
            })
            .Step("Scrolled two entries the page opens with E3 under its Browse header and runs to E14: 3–14 of 27", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(2), 120, 18, ColorMode.None);

                AssertLine("errors · 3–14 of 27 · Esc back", 30, lines[1]);
                AssertLine(GroupHeader("Browse"), 8, lines[2]);
                AssertLine(LogEntry(3), 17, lines[3]);
                AssertLine(LogEntry(5), 17, lines[5]);
                AssertLine(GroupHeader("Add to cart"), 13, lines[6]);
                AssertLine(LogEntry(6), 17, lines[7]);
                AssertLine(LogEntry(10), 18, lines[11]);
                AssertLine(GroupHeader("Checkout"), 10, lines[12]);
                AssertLine(LogEntry(11), 18, lines[13]);
                AssertLine(LogEntry(14), 18, lines[16]);
                AssertLine(ErrorLogFooter, 70, lines[17]);
                for (var row = 2; row <= 16; row++)
                {
                    Assert.IsFalse(lines[row].Text.EndsWith(" E1", StringComparison.Ordinal), $"E1 on row {row}");
                    Assert.IsFalse(lines[row].Text.EndsWith(" E2", StringComparison.Ordinal), $"E2 on row {row}");
                }
            })
            .Step("Scrolled to the last entry of a group the group header still rides with it: from E5, 5–16 of 27", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(4), 120, 18, ColorMode.None);

                AssertLine("errors · 5–16 of 27 · Esc back", 30, lines[1]);
                AssertLine(GroupHeader("Browse"), 8, lines[2]);
                AssertLine(LogEntry(5), 17, lines[3]);
                AssertLine(GroupHeader("Add to cart"), 13, lines[4]);
                AssertLine(LogEntry(6), 17, lines[5]);
                AssertLine(LogEntry(10), 18, lines[9]);
                AssertLine(GroupHeader("Checkout"), 10, lines[10]);
                AssertLine(LogEntry(11), 18, lines[11]);
                AssertLine(LogEntry(16), 18, lines[16]);
            })
            .Step("A scroll past the end lands on the last page — from E15, whose Checkout header, six entries, the Place order header and its seven fill the 15 rows exactly — and so does a scroll onto any offset past it", context =>
            {
                foreach (var scroll in new[] { 14, 20, 26, 999, int.MaxValue })
                {
                    var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(scroll), 120, 18, ColorMode.None);

                    AssertLine("errors · 15–27 of 27 · Esc back", 31, lines[1]);
                    AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                    AssertLine(LogEntry(15), 18, lines[3]);
                    AssertLine(LogEntry(20), 18, lines[8]);
                    AssertLine(GroupHeader("Place order"), 13, lines[9]);
                    AssertLine(LogEntry(21), 18, lines[10]);
                    AssertLine(LogEntry(27), 18, lines[16]);
                    AssertLine(ErrorLogFooter, 70, lines[17]);
                }

                // One entry before it the page is full from E14 and short of the end.
                var before = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(13), 120, 18, ColorMode.None);
                AssertLine("errors · 14–26 of 27 · Esc back", 31, before[1]);
                AssertLine(LogEntry(14), 18, before[3]);
                AssertLine(LogEntry(26), 18, before[16]);
            })
            .Step("A group header may close the page, announcing the next group: at 16 rows the 13 log rows are the first two groups and the Checkout header alone, 1–10 of 27", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(), 120, 16, ColorMode.None);

                Assert.HasCount(16, lines);
                AssertLine("errors · 1–10 of 27 · Esc back", 30, lines[1]);
                AssertLine(GroupHeader("Add to cart"), 13, lines[8]);
                AssertLine(LogEntry(10), 18, lines[13]);
                AssertLine(GroupHeader("Checkout"), 10, lines[14]);
                AssertLine(ErrorLogFooter, 70, lines[15]);
            })
            .Step("At 80 columns and 24 rows the 21 log rows hold the first eighteen entries: 1–18 of 27", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(), 80, 24, ColorMode.None);

                Assert.HasCount(24, lines);
                AssertLine("Log  ● Running" + Spaces(53) + "  ⚡ TestFuzn", 80, lines[0]);
                AssertLine("errors · 1–18 of 27 · Esc back", 30, lines[1]);
                AssertLine(GroupHeader("Browse"), 8, lines[2]);
                AssertLine(LogEntry(1), 17, lines[3]);
                AssertLine(GroupHeader("Add to cart"), 13, lines[8]);
                AssertLine(GroupHeader("Checkout"), 10, lines[14]);
                AssertLine(LogEntry(11), 18, lines[15]);
                AssertLine(LogEntry(18), 18, lines[22]);
                AssertLine(ErrorLogFooter, 70, lines[23]);
            })
            .Step("Under an unbounded height there is no page to fill, so the offset applies as it is, clamped into the entries: from E21 the last group alone", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(20), 120, 0, ColorMode.None);

                Assert.HasCount(11, lines);
                AssertLine("errors · 21–27 of 27 · Esc back", 31, lines[1]);
                AssertLine(GroupHeader("Place order"), 13, lines[2]);
                AssertLine(LogEntry(21), 18, lines[3]);
                AssertLine(LogEntry(27), 18, lines[9]);
                AssertLine(ErrorLogFooter, 70, lines[10]);

                var past = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(999), 120, 0, ColorMode.None);
                Assert.HasCount(5, past);
                AssertLine("errors · 27–27 of 27 · Esc back", 31, past[1]);
                AssertLine(GroupHeader("Place order"), 13, past[2]);
                AssertLine(LogEntry(27), 18, past[3]);
            })
            .Step("A short log is never scrolled: the rich run's two entries stay at the top whatever the offset, and a height without room for an entry line reads 0 of the count", context =>
            {
                var scrolled = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(1), 120, 40, ColorMode.None);
                AssertLine("errors · 1–2 of 2 · Esc back", 28, scrolled[1]);
                AssertLine(GroupHeader("Checkout"), 10, scrolled[2]);

                // Three rows: the title, the header and the footer — no log row at all.
                var cramped = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(), 120, 3, ColorMode.None);
                Assert.HasCount(3, cramped);
                AssertLine("errors · 0 of 2 · Esc back", 26, cramped[1]);
                AssertLine(ErrorLogFooter, 70, cramped[2]);

                // Four rows: the group header takes the one log row, so no entry is on the page either.
                var headerOnly = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(), 120, 4, ColorMode.None);
                AssertLine("errors · 0 of 2 · Esc back", 26, headerOnly[1]);
                AssertLine(GroupHeader("Checkout"), 10, headerOnly[2]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_log_last_page_is_the_fullest_page_that_shows_the_end_whole()
    {
        // Where no page ends on the log's last line exactly, the last page is the one entry
        // further down: the offset whose entries, with their group headers, are the most the
        // rows hold with the log's end still whole. It is the top end of the scroll alone —
        // an offset under it applies as it is, so a page that runs out of rows before the end
        // is one the scroll passes through on its way there, never one it stops on.
        await Scenario()
            .Step("The 27-entry log at 120×12 has nine log rows: from E20 a page would need ten — the Checkout header, E20, the Place order header and its seven — so the last page opens on E21 and ends on E27, and every offset past it lands there", context =>
            {
                // From E21 the tail is the Place order header and seven entries, eight of the
                // nine rows, and one entry earlier it is ten: 20 is the last offset. The old
                // clamp stopped on 19, whose page needs those ten rows, so E27 was off the
                // bottom of every page the scroll could reach.
                foreach (var scroll in new[] { 20, 26, 999, int.MaxValue })
                {
                    var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(scroll), 120, 12, ColorMode.None);

                    Assert.HasCount(12, lines);
                    AssertLine("Log  ● Running" + Spaces(93) + "  ⚡ TestFuzn", 120, lines[0]);
                    AssertLine("errors · 21–27 of 27 · Esc back", 31, lines[1]);
                    AssertLine(GroupHeader("Place order"), 13, lines[2]);
                    AssertLine(LogEntry(21), 18, lines[3]);
                    AssertLine(LogEntry(27), 18, lines[9]);
                    AssertLine(string.Empty, 0, lines[10]);
                    AssertLine(ErrorLogFooter, 70, lines[11]);
                }
            })
            .Step("The offsets under it apply as they are: from E20 the nine rows stop at E26 and from E19 at E25, pages the scroll passes through on its way to the last one", context =>
            {
                // From E20: its Checkout header, E20, the Place order header and six entries.
                // From E19: the Checkout header, E19 and E20, the Place order header and five.
                var cut = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(19), 120, 12, ColorMode.None);

                AssertLine("errors · 20–26 of 27 · Esc back", 31, cut[1]);
                AssertLine(GroupHeader("Checkout"), 10, cut[2]);
                AssertLine(LogEntry(20), 18, cut[3]);
                AssertLine(GroupHeader("Place order"), 13, cut[4]);
                AssertLine(LogEntry(21), 18, cut[5]);
                AssertLine(LogEntry(26), 18, cut[10]);
                AssertLine(ErrorLogFooter, 70, cut[11]);
                foreach (var line in cut)
                    Assert.DoesNotContain("E27", line.Text);

                var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView(18), 120, 12, ColorMode.None);

                AssertLine("errors · 19–25 of 27 · Esc back", 31, lines[1]);
                AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                AssertLine(LogEntry(19), 18, lines[3]);
                AssertLine(LogEntry(20), 18, lines[4]);
                AssertLine(GroupHeader("Place order"), 13, lines[5]);
                AssertLine(LogEntry(21), 18, lines[6]);
                AssertLine(LogEntry(25), 18, lines[10]);
                AssertLine(ErrorLogFooter, 70, lines[11]);
            })
            .Step("Three nine-line entries at 80×24: the 21 log rows take two of them under their group header, so the last page opens on the second and the third is whole on it", context =>
            {
                // The tail from the second entry is the Checkout header and eighteen lines, 19
                // of the 21 rows; from the first it is 28. So 1 is the last offset, and the
                // two blank rows under the third entry are what the page has left.
                foreach (var scroll in new[] { 1, 2, 999 })
                {
                    var lines = LiveDashboardLayout.Render(new[] { TallSnapshot(3) }, LogView(scroll), 80, 24, ColorMode.None);

                    Assert.HasCount(24, lines);
                    AssertLine("Tall  ● Running" + Spaces(52) + "  ⚡ TestFuzn", 80, lines[0]);
                    AssertLine("errors · 2–3 of 3 · Esc back", 28, lines[1]);
                    AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                    AssertLine("  2× (0.0/s) ", 13, lines[3]);
                    AssertLine(TallEntryLine('b', 66), 80, lines[4]);
                    AssertLine(TallEntryLine('b', 66), 80, lines[10]);
                    AssertLine(TallEntryLine('b', 38), 52, lines[11]);
                    AssertLine("  3× (0.0/s) ", 13, lines[12]);
                    AssertLine(TallEntryLine('c', 66), 80, lines[13]);
                    AssertLine(TallEntryLine('c', 66), 80, lines[19]);
                    AssertLine(TallEntryLine('c', 38), 52, lines[20]);
                    AssertLine(string.Empty, 0, lines[21]);
                    AssertLine(string.Empty, 0, lines[22]);
                    AssertLine(ErrorLogFooter, 70, lines[23]);
                }

                // From the top the page is the header and the first two entries whole, then two
                // lines of the third — which the indicator counts as shown.
                var top = LiveDashboardLayout.Render(new[] { TallSnapshot(3) }, LogView(), 80, 24, ColorMode.None);
                AssertLine("errors · 1–3 of 3 · Esc back", 28, top[1]);
                AssertLine("  1× (0.0/s) ", 13, top[3]);
                AssertLine("  2× (0.0/s) ", 13, top[12]);
                AssertLine("  3× (0.0/s) ", 13, top[21]);
                AssertLine(TallEntryLine('c', 66), 80, top[22]);
                AssertLine(ErrorLogFooter, 70, top[23]);
            })
            .Step("When even the last entry alone overruns the rows it is the last page by itself, cut from the bottom", context =>
            {
                // Nine rows at 80×12, and the second entry's group header and nine lines are
                // ten: no offset shows the log's end whole, so the walk stops on the last entry
                // and the page is its first eight lines under the header.
                var lines = LiveDashboardLayout.Render(new[] { TallSnapshot(2) }, LogView(999), 80, 12, ColorMode.None);

                Assert.HasCount(12, lines);
                AssertLine("errors · 2–2 of 2 · Esc back", 28, lines[1]);
                AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                AssertLine("  2× (0.0/s) ", 13, lines[3]);
                for (var row = 4; row <= 10; row++)
                    AssertLine(TallEntryLine('b', 66), 80, lines[row]);

                AssertLine(ErrorLogFooter, 70, lines[11]);

                // The first entry is the page from the top, cut the same way.
                var top = LiveDashboardLayout.Render(new[] { TallSnapshot(2) }, LogView(), 80, 12, ColorMode.None);
                AssertLine("errors · 1–1 of 2 · Esc back", 28, top[1]);
                AssertLine("  1× (0.0/s) ", 13, top[3]);
                AssertLine(TallEntryLine('a', 66), 80, top[10]);
                AssertLine(ErrorLogFooter, 70, top[11]);
            })
            .Step("An entry taller than the page earlier in the log leaves the scroll free: the last page opens past it and it is still the page from the top", context =>
            {
                // Five log rows at 80×8 over a nine-line entry and a one-line one: the tail
                // from the short entry is its group header and its line, and from the tall one
                // eleven, so the last page is the short entry's alone.
                var mixed = new LiveMetricsSnapshot
                {
                    ScenarioName = "Mixed",
                    Errors = new[]
                    {
                        new LiveErrorEntry { StepName = "Checkout", Message = new string('a', 500), Count = 1, LastSeen = SampleTime(-1) },
                        new LiveErrorEntry { StepName = "Checkout", Message = "short", Count = 2, LastSeen = SampleTime(-2) }
                    }
                };

                var last = LiveDashboardLayout.Render(new[] { mixed }, LogView(999), 80, 8, ColorMode.None);
                Assert.HasCount(8, last);
                AssertLine("errors · 2–2 of 2 · Esc back", 28, last[1]);
                AssertLine(GroupHeader("Checkout"), 10, last[2]);
                AssertLine("  2× (0.0/s)  short", 19, last[3]);
                AssertLine(string.Empty, 0, last[4]);
                AssertLine(string.Empty, 0, last[6]);
                AssertLine(ErrorLogFooter, 70, last[7]);

                var top = LiveDashboardLayout.Render(new[] { mixed }, LogView(), 80, 8, ColorMode.None);
                AssertLine("errors · 1–1 of 2 · Esc back", 28, top[1]);
                AssertLine(GroupHeader("Checkout"), 10, top[2]);
                AssertLine("  1× (0.0/s) ", 13, top[3]);
                AssertLine(TallEntryLine('a', 66), 80, top[4]);
                AssertLine(TallEntryLine('a', 66), 80, top[6]);
                AssertLine(ErrorLogFooter, 70, top[7]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_log_notices_and_unlisted_note()
    {
        await Scenario()
            .Step("Without an error the header reads 0 of 0 over the no-errors notice, under the title", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, LogView(), 120, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine("Browse catalog  ● Running · warmup: Fixed Load 50 rps" + Spaces(54) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine("errors · 0 of 0 · Esc back", 26, lines[1]);
                AssertLine("no errors", 9, lines[2]);
                for (var row = 3; row < 39; row++)
                    AssertLine(string.Empty, 0, lines[row]);
                AssertLine(ErrorLogFooter, 70, lines[39]);

                var styled = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, LogView(), 120, 40, ColorMode.TrueColor);
                AssertLine(Sgr(BoldAccent, "errors") + " " + Sgr(Dim, "·") + " 0 of 0 " + Sgr(Dim, "· Esc back"), 26, styled[1]);
                AssertLine(Sgr(Dim, "no errors"), 9, styled[2]);
            })
            .Step("Without a scenario the header and the notice stand alone over blank rows and the log's footer", context =>
            {
                var lines = LiveDashboardLayout.Render(Array.Empty<LiveMetricsSnapshot>(), LogView(5), 40, 5, ColorMode.None);

                Assert.HasCount(5, lines);
                AssertLine("errors · 0 of 0 · Esc back", 26, lines[0]);
                AssertLine("no errors", 9, lines[1]);
                AssertLine(string.Empty, 0, lines[2]);
                AssertLine(string.Empty, 0, lines[3]);
                AssertLine("Esc back · ↑↓ scroll · q quit", 29, lines[4]);
            })
            .Step("Distinct errors beyond what the snapshot carries are noted in the header, before the back hint", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(3, distinctErrorCount: 7) }, LogView(), 120, 40, ColorMode.None);

                AssertLine("Many  ● Running" + Spaces(92) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine("errors · 1–3 of 3 · +4 unlisted · Esc back", 42, lines[1]);
                AssertLine(GroupHeader("S"), 3, lines[2]);
                AssertLine("  1× (0.0/s)  E1", 16, lines[3]);
                AssertLine("  3× (0.0/s)  E3", 16, lines[5]);
                AssertLine(string.Empty, 0, lines[6]);

                var styled = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(3, distinctErrorCount: 7) }, LogView(), 120, 40, ColorMode.TrueColor);
                AssertLine(Sgr(BoldAccent, "errors") + " " + Sgr(Dim, "·") + " 1–3 of 3 " + Sgr(Dim, "· +4 unlisted · Esc back"), 42, styled[1]);

                // A distinct count no larger than the list adds no note.
                var listed = LiveDashboardLayout.Render(new[] { ManyErrorsSnapshot(3, distinctErrorCount: 3) }, LogView(), 120, 40, ColorMode.None);
                AssertLine("errors · 1–3 of 3 · Esc back", 28, listed[1]);
            })
            .Step("The header is cut with an ellipsis at a narrow width", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(), 20, 10, ColorMode.None);

                AssertLine("errors · 1–2 of 2 ·…", 20, lines[1]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_log_wraps_whole_messages_without_the_ticker_cap()
    {
        await Scenario()
            .Step("The 102-character message breaks at the last space that fits the 118 columns — after the message, inside its three-space gap — and the ages continue under the message", context =>
            {
                // The prefix "7× (1.5/s)  " is 12 columns: the line holds the message and two
                // of the gap's spaces, 116 columns, and the ages follow under a 12-column indent.
                var lines = LiveDashboardLayout.Render(new[] { WrapSnapshot(LongMessage) }, LogView(), 120, 40, ColorMode.None);

                AssertLine("errors · 1–1 of 1 · Esc back", 28, lines[1]);
                AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                AssertLine("  7× (1.5/s)  " + LongMessage + Spaces(2), 118, lines[3]);
                AssertLine("  " + Spaces(12) + "first 30s ago · last 3s ago", 41, lines[4]);
                AssertLine(string.Empty, 0, lines[5]);

                var styled = LiveDashboardLayout.Render(new[] { WrapSnapshot(LongMessage) }, LogView(), 120, 40, ColorMode.TrueColor);
                AssertLine("  " + Sgr(Red, "7×") + " " + Sgr(Dim, "(1.5/s)") + "  " + LongMessage + Spaces(2), 118, styled[3]);
                AssertLine("  " + Spaces(12) + Sgr(Dim, "first 30s ago · last 3s ago"), 41, styled[4]);
            })
            .Step("A 300-character word takes every line it needs — eight, none ending in an ellipsis — where the ticker keeps three", context =>
            {
                // At 60 columns the entries are 58 wide with rates and without ages; the last
                // space that fits is the one after the rate, so the first line is the prefix
                // alone, and the continuation lines hold 46 characters after the 12-column
                // indent: six full lines and one of 24.
                var lines = LiveDashboardLayout.Render(new[] { WrapSnapshot(new string('x', 300)) }, LogView(), 60, 40, ColorMode.None);

                AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                AssertLine("  7× (1.5/s) ", 13, lines[3]);
                AssertLine("  " + Spaces(12) + new string('x', 46), 60, lines[4]);
                AssertLine("  " + Spaces(12) + new string('x', 46), 60, lines[9]);
                AssertLine("  " + Spaces(12) + new string('x', 24), 38, lines[10]);
                AssertLine(string.Empty, 0, lines[11]);
                AssertLine(ErrorLogFooterAt51, 51, lines[39]);
                foreach (var line in lines)
                    Assert.DoesNotContain(MarkupText.Ellipsis.ToString(), line.Text);

                var ticker = LiveDashboardLayout.Render(new[] { WrapSnapshot(new string('x', 300)) }, DefaultView, 60, 0, ColorMode.None);
                Assert.Contains(MarkupText.Ellipsis.ToString(), ticker[IndexOfPanel(ticker, "Errors") + 3].Text);
            })
            .Step("An entry taller than the page shows what fits, and the indicator counts it as shown", context =>
            {
                // Ten rows: the title, the header, seven log rows and the footer — the group
                // header and the first six of the entry's eight lines.
                var lines = LiveDashboardLayout.Render(new[] { WrapSnapshot(new string('x', 300)) }, LogView(), 60, 10, ColorMode.None);

                Assert.HasCount(10, lines);
                AssertLine("errors · 1–1 of 1 · Esc back", 28, lines[1]);
                AssertLine(GroupHeader("Checkout"), 10, lines[2]);
                AssertLine("  7× (1.5/s) ", 13, lines[3]);
                AssertLine("  " + Spaces(12) + new string('x', 46), 60, lines[4]);
                AssertLine("  " + Spaces(12) + new string('x', 46), 60, lines[8]);
                AssertLine(ErrorLogFooterAt51, 51, lines[9]);
            })
            .Step("Markup and control characters in a step name and a message render literally, in the group header and the entry alike", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { HostileSnapshot() }, LogView(), 120, 0, ColorMode.None);
                var header = IndexOfLineStartingWith(lines, GroupHeader("Control [step]"));

                AssertLine(GroupHeader("Control [step]"), 16, lines[header]);
                // The hostile log's count column is ten wide ("2000000000×") and its rate
                // column fourteen ("(1000000000/s)"), so the Control entry's count and its dash
                // rate are padded left to them; the message's escapes and controls are spaces.
                Assert.StartsWith("  " + Spaces(9) + "3× " + Spaces(9) + "(—/s)" + "  [bold]Boom[/]  [31mred [0m  tail ", lines[header + 1].Text);
                Assert.DoesNotContain(AnsiCodes.Escape, lines[header + 1].Text);
                Assert.AreEqual(lines[header + 1].Text.Length, lines[header + 1].Width);

                var rocket = IndexOfLineStartingWith(lines, GroupHeader(Rocket + " Launch ☃"));
                Assert.StartsWith("  " + Spaces(8) + "40× ", lines[rocket + 1].Text);
                AssertNoLoneSurrogate(lines, "120×0");
            })
            .Run();
    }

    [Test]
    public async Task Verify_help_overlay_on_the_overview()
    {
        // At 120×40 the 39 rows above the footer leave 27 around the 12-row panel, so it sits
        // on rows 13-24; the 85 columns around its 35 put its left edge on column 42 and the
        // frame's columns 77-119 after it. The overview's rows 13 and 14 are the chart
        // panels' fifth and sixth body rows and row 24 the requests panel's top border.
        await Scenario()
            .Step("The panel lists every binding, key column aligned, centred over the overview's chart and requests rows; the rows above and below are untouched, and the footer is the help's", context =>
            {
                var overview = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 40, ColorMode.None);
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { ShowHelp = true }, 120, 40, ColorMode.None);

                AssertHelpOverlay(overview, help, 120, top: 13, left: 42);
                AssertLine("│    │" + Glyphs('⣿', 36) + PanelTop("? help", HelpPanelWidth) + Glyphs('⣿', 33) + Spaces(8) + " │", 120, help[13]);
                AssertLine("│   0┤" + Glyphs('⣿', 36) + HelpPanelLine("1", "overview") + Glyphs('⣀', 33) + Spaces(8) + " │", 120, help[14]);
                AssertLine("╭─ Requests " + Glyphs('─', 30) + Bottom(HelpPanelWidth) + Glyphs('─', 42) + "╮", 120, help[24]);
                AssertMaximumWidth(120, help);
            })
            .Step("TrueColor: the parts around the panel keep their styles — a span the panel's edge cuts is closed before the panel and reopened after it — and the panel's header and keys are styled as the widget renders them", context =>
            {
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { ShowHelp = true }, 120, 40, ColorMode.TrueColor);

                AssertLine("│    " + Sgr(Dim, "│") + Sgr(Green, Glyphs('⣿', 36)) + "╭─ " + Sgr(BoldAccent, "? help") + " " + Glyphs('─', 24) + "╮" + Sgr(Percentile99, Glyphs('⣿', 33)) + Spaces(8) + " │", 120, help[13]);
                AssertLine("│   " + Sgr(Dim, "0┤") + Sgr(Green, Glyphs('⣿', 36)) + "│ " + Sgr(Bold, "1") + Spaces(8) + "  overview" + Spaces(12) + " │" + Sgr(Median, Glyphs('⣀', 33)) + Spaces(8) + " │", 120, help[14]);
                AssertLine("╭─ " + Sgr(BoldAccent, "Requests") + " " + Glyphs('─', 30) + Bottom(HelpPanelWidth) + Glyphs('─', 42) + "╮", 120, help[24]);
                Assert.Contains("│ " + Sgr(Bold, "PgUp PgDn") + "  page the error log" + Spaces(2) + " │", help[18].Text);
                AssertMaximumWidth(120, help);
            })
            .Step("At 80×24 the panel sits on rows 5-16 from column 22: over the timeline, the blank, the requests panel and the ticker", context =>
            {
                var overview = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 80, 24, ColorMode.None);
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { ShowHelp = true }, 80, 24, ColorMode.None);

                AssertHelpOverlay(overview, help, 80, top: 5, left: 22);
                AssertLine(Glyphs('=', 16) + "#### G" + PanelTop("? help", HelpPanelWidth) + "xed Load 50 rps -------", 80, help[5]);
                AssertLine(Spaces(22) + HelpPanelLine("2 ⏎", "step detail") + Spaces(23), 80, help[7]);
                AssertMaximumWidth(80, help);
            })
            .Step("At 40×20 the panel fits whole on rows 3-14 from column 2, and its last line is still the quit key", context =>
            {
                var overview = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 40, 20, ColorMode.None);
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { ShowHelp = true }, 40, 20, ColorMode.None);

                AssertHelpOverlay(overview, help, 40, top: 3, left: 2);
                Assert.Contains(HelpPanelLine("q", "quit"), help[13].Text);
            })
            .Step("A narrow frame caps the panel: at 30×12 it is 28 columns from column 1 with its lines cut, and 11 rows from row 0 with the quit line dropped from the bottom — the footer row never covered", context =>
            {
                // The overview at 30×12: the title, two tile rows of four lines, a blank and
                // the requests panel's top border on row 10 over the footer. The panel's inner
                // width is 24: "↑ ↓  select step / scroll" is cut to 23 and the ellipsis.
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { ShowHelp = true }, 30, 12, ColorMode.None);

                Assert.HasCount(12, help);
                AssertLine("C" + PanelTop("? help", 28) + " ", 30, help[0]);
                AssertLine("╭" + Box("1" + Spaces(8) + "  overview" + Spaces(5)) + "╮", 30, help[1]);
                AssertLine("╰" + Box("↑ ↓" + Spaces(6) + "  select step …") + "╯", 30, help[4]);
                AssertLine(" " + Box("?" + Spaces(8) + "  help" + Spaces(9)) + " ", 30, help[9]);
                AssertLine("╭" + Bottom(28) + "╮", 30, help[10]);
                AssertLine(HelpFooter, 28, help[11]);
                AssertMaximumWidth(30, help);
                foreach (var line in help)
                    Assert.DoesNotContain("quit" + Spaces(9), line.Text);
            })
            .Step("Below six columns or three rows there is no room for the panel and the frame is as without the help but for the footer", context =>
            {
                // At five columns the help footer's hints are all too wide: the quit hint
                // alone, cut with the widget's ellipsis.
                var narrow = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { ShowHelp = true }, 5, 12, ColorMode.None);
                var plain = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 5, 12, ColorMode.None);
                for (var row = 0; row < 11; row++)
                    AssertLine(plain[row].Text, plain[row].Width, narrow[row]);
                AssertLine("q qu…", 5, narrow[11]);

                var flat = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { ShowHelp = true }, 120, 2, ColorMode.None);
                var flatPlain = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView, 120, 2, ColorMode.None);
                AssertLine(flatPlain[0].Text, flatPlain[0].Width, flat[0]);
                AssertLine(HelpFooter, 28, flat[1]);

                // Six columns and four rows: a four-column panel — no header, no content
                // column — of one empty line, centred a column in over the frame's rows.
                var six = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DefaultView with { ShowHelp = true }, 6, 4, ColorMode.None);
                Assert.HasCount(4, six);
                Assert.AreEqual("╭──╮", six[0].Text.Substring(1, 4));
                Assert.AreEqual("│  │", six[1].Text.Substring(1, 4));
                Assert.AreEqual("╰──╯", six[2].Text.Substring(1, 4));
                AssertMaximumWidth(6, six);
                for (var row = 0; row < 3; row++)
                    Assert.AreEqual(6, six[row].Width, $"Covered row {row}");
            })
            .Run();
    }

    [Test]
    public async Task Verify_help_overlay_on_the_step_detail_and_the_error_log()
    {
        await Scenario()
            .Step("Over the detail the panel covers its chart, error and blank rows, the entry's columns kept either side of it", context =>
            {
                var detail = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(1), 120, 40, ColorMode.None);
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, DetailView(1) with { ShowHelp = true }, 120, 40, ColorMode.None);

                AssertHelpOverlay(detail, help, 120, top: 13, left: 42);
                AssertLine("│ 30× (2.0/s)  Checkout · Connection refus" + HelpPanelLine("↑ ↓", "select step / scroll") + "ago · last 2s ago" + Spaces(24) + " │", 120, help[17]);
                AssertLine(Spaces(42) + HelpPanelLine("Esc", "close help / back") + Spaces(43), 120, help[19]);
                AssertLine(CheckoutDetailHeader, 30, help[1]);
            })
            .Step("Over the error log the panel covers blank rows: the title, the header and the entries above it are untouched", context =>
            {
                var log = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView(), 120, 40, ColorMode.None);
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, LogView() with { ShowHelp = true }, 120, 40, ColorMode.None);

                AssertHelpOverlay(log, help, 120, top: 13, left: 42);
                AssertLine("errors · 1–2 of 2 · Esc back", 28, help[1]);
                AssertLine("  " + CheckoutLogEntry, 83, help[3]);
                AssertLine(Spaces(42) + PanelTop("? help", HelpPanelWidth) + Spaces(43), 120, help[13]);
                AssertLine(Spaces(42) + Bottom(HelpPanelWidth) + Spaces(43), 120, help[24]);
            })
            .Step("Over the paused, windowed detail the badge and the window stay: the help changes nothing but the covered rows and the footer", context =>
            {
                var state = DetailView(1) with { IsPaused = true, TimeWindow = 60 };
                var detail = LiveDashboardLayout.Render(new[] { RichSnapshot() }, state, 120, 40, ColorMode.None);
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot() }, state with { ShowHelp = true }, 120, 40, ColorMode.None);

                AssertHelpOverlay(detail, help, 120, top: 13, left: 42);
                Assert.Contains(LiveDashboardLayout.PausedBadgeText, help[0].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_help_overlay_on_two_columns_at_160x40()
    {
        // The two-scenario frame at 160×40 is the columns golden's: two 79-column columns and
        // the two-column gap between them, 39 joined rows over the footer. The 12-row panel
        // leaves 27 rows around it, so it sits on rows 13-24; its 35 columns leave 125, so its
        // left edge is column 62 and the frame's columns 97-159 — the tail of the second
        // column — follow it. Rows 13-14 are both columns' requests chart bodies, 15 their
        // bottom borders and 24 their Requests panel tops; no column reaches 80 columns, so
        // none carries the logo here.
        await Scenario()
            .Step("The panel is centred over the joined rows: the rows outside it are the column frame's, every covered row is exactly 160 columns, and the footer is the help's", context =>
            {
                var columns = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 160, 40, ColorMode.None);
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView with { ShowHelp = true }, 160, 40, ColorMode.None);

                AssertHelpOverlay(columns, help, 160, top: 13, left: 62);
                for (var row = 13; row <= 24; row++)
                    Assert.AreEqual(160, help[row].Width, $"Covered row {row}");

                AssertMaximumWidth(160, help);
                foreach (var line in help)
                    Assert.DoesNotContain("⚡", line.Text);
            })
            .Step("The covered rows keep both columns' own glyphs either side of the panel: 56 of the rich column's 65 chart cells before it, the tail of the indeterminate column's after it", context =>
            {
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView with { ShowHelp = true }, 160, 40, ColorMode.None);

                // Row 13: the rich column's border and padding, its blank three-column value
                // label and axis, and its cells from column 6 to 61; after the panel, columns
                // 97-159 are the indeterminate column's empty body and its right border.
                AssertLine("│ " + "   │" + Glyphs('⣿', 56) + PanelTop("? help", HelpPanelWidth) + Spaces(62) + "│", 160, help[13]);
                // Row 15 is both columns' bottom borders, row 24 both Requests panel tops.
                AssertLine("╰" + Glyphs('─', 61) + HelpPanelLine("2 ⏎", "step detail") + Glyphs('─', 62) + "╯", 160, help[15]);
                AssertLine("╭─ Requests " + Glyphs('─', 50) + Bottom(HelpPanelWidth) + Glyphs('─', 62) + "╮", 160, help[24]);
            })
            .Step("TrueColor: the green cells the panel's left edge cuts are closed before it, the panel keeps its own styling, and every row outside it is the column frame's byte for byte", context =>
            {
                var columns = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView, 160, 40, ColorMode.TrueColor);
                var help = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, DefaultView with { ShowHelp = true }, 160, 40, ColorMode.TrueColor);

                Assert.StartsWith("│    " + Sgr(Dim, "│") + Sgr(Green, Glyphs('⣿', 56)) + "╭─ " + Sgr(BoldAccent, "? help") + " " + Glyphs('─', 24) + "╮", help[13].Text);
                Assert.StartsWith("│   " + Sgr(Dim, "0┤") + Sgr(Green, Glyphs('⣿', 56)) + "│ " + Sgr(Bold, "1") + Spaces(8) + "  overview" + Spaces(12) + " │", help[14].Text);
                for (var row = 0; row < 13; row++)
                    AssertLine(columns[row].Text, columns[row].Width, help[row]);

                for (var row = 25; row < 39; row++)
                    AssertLine(columns[row].Text, columns[row].Width, help[row]);

                AssertMaximumWidth(160, help);
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_help_panel_never_reaches_the_compact_logo()
    {
        // A title line that carries the logo keeps its last thirteen columns for it: eleven of
        // "⚡ TestFuzn" after the two-column gap, so the gap opens on column width − 13. The
        // panel is 35 columns at the floor of half the leftover, so its right edge is
        // (width − 35) / 2 + 35 — at 80 columns, the narrowest a logo rides, 22 + 35 = 57,
        // ten columns short of the 67 the gap opens on — and every column added moves the edge
        // half a column right and the gap a whole one, so the margin only widens. The splice
        // depends on it: it takes the columns after the panel short by the row's surplus, the
        // lightning's second column, which is only right while the lightning is uncovered.
        await Scenario()
            .Step("At every width from the logo's minimum to 220 the panel's right edge is short of the logo's gap, and the title row it covers keeps its lightning and its declared width", context =>
            {
                for (var width = LiveDashboardLayout.MinimumWidthForLogo; width <= 220; width++)
                {
                    var left = (width - HelpPanelWidth) / 2;
                    Assert.IsLessThanOrEqualTo(width - LogoWidget.CompactWidth - TitleLogoGap, left + HelpPanelWidth, $"Panel right edge at {width}");

                    // Fourteen rows: thirteen above the footer, so the panel tops row 0 — the
                    // log's title line, the one row that carries the logo.
                    var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView() with { ShowHelp = true }, width, 14, ColorMode.None);

                    Assert.StartsWith("Log  ● Running", lines[0].Text, $"Title at {width}");
                    Assert.Contains(PanelTop("? help", HelpPanelWidth), lines[0].Text, $"Panel on the title at {width}");
                    Assert.EndsWith(Spaces(TitleLogoGap) + "⚡ TestFuzn", lines[0].Text, $"Logo at {width}");
                    Assert.AreEqual(width, lines[0].Width, $"Declared width at {width}");
                    Assert.AreEqual(width - 1, lines[0].Text.Length, $"Characters at {width}");
                }
            })
            .Step("A column below the logo's minimum the title carries none, and the covered row is the width in characters", context =>
            {
                var width = LiveDashboardLayout.MinimumWidthForLogo - 1;
                var lines = LiveDashboardLayout.Render(new[] { LogSnapshot() }, LogView() with { ShowHelp = true }, width, 14, ColorMode.None);

                Assert.DoesNotContain("⚡", lines[0].Text);
                Assert.Contains(PanelTop("? help", HelpPanelWidth), lines[0].Text);
                Assert.AreEqual(width, lines[0].Width);
                Assert.AreEqual(width, lines[0].Text.Length);
            })
            .Run();
    }

    [Test]
    public async Task Verify_hostile_error_log_and_help_never_break_the_frame()
    {
        await Scenario()
            .Step("At every other width from 1 to 220 (and the footer's edges) and every height from 1 to 60, scrolled 0, 5 and 999, with and without the help, the hostile log is exactly the height, the footer owns the last row, no line exceeds the width and no surrogate pair is split", context =>
            {
                var snapshots = new[] { HostileSnapshot() };
                var boundaries = new[] { 5, 7, 17, 29, 43, 51, 61, 79 };
                foreach (var scroll in new[] { 0, 5, 999 })
                {
                    foreach (var showHelp in new[] { false, true })
                    {
                        var viewState = LogView(scroll) with { ShowHelp = showHelp };
                        for (var width = 1; width <= 220; width++)
                        {
                            if (width % 2 != 0 && Array.IndexOf(boundaries, width) < 0)
                                continue;

                            for (var height = 1; height <= 60; height++)
                            {
                                var lines = LiveDashboardLayout.Render(snapshots, viewState, width, height, ColorMode.TrueColor);
                                var size = $"{width}×{height} scrolled {scroll}, help {showHelp}";

                                Assert.HasCount(height, lines, "Row count at " + size);
                                Assert.AreEqual(showHelp ? ExpectedHelpFooterWidth(width) : ExpectedErrorLogFooterWidth(width), lines[height - 1].Width, "Footer at " + size);
                                if (width >= 6)
                                    Assert.EndsWith(Sgr(Bold, "q") + " quit", lines[height - 1].Text, "Footer at " + size);
                                AssertMaximumWidth(width, lines);
                                AssertNoLoneSurrogate(lines, size);
                                // The log's own footer names the help key too, so only the
                                // help frames — whose footer does not — are checked for the panel.
                                if (showHelp)
                                    Assert.AreEqual(width >= 13 && height >= 3, CountLinesContaining(lines, "? help") == 1, "Help panel at " + size);
                            }
                        }
                    }
                }
            })
            .Step("Color mode None renders the log without an escape or a control character, every line's text at exactly its declared width", context =>
            {
                foreach (var scroll in new[] { 0, 5, 999 })
                {
                    var lines = LiveDashboardLayout.Render(new[] { HostileSnapshot() }, LogView(scroll), 120, 40, ColorMode.None);

                    Assert.HasCount(40, lines);
                    Assert.StartsWith("errors · ", lines[1].Text);
                    for (var row = 0; row < lines.Count; row++)
                    {
                        var text = lines[row].Text;
                        Assert.DoesNotContain("\u001b", text, $"Escape on row {row} scrolled {scroll}");
                        Assert.DoesNotContain("\t", text, $"Tab on row {row} scrolled {scroll}");
                        Assert.DoesNotContain("\0", text, $"NUL on row {row} scrolled {scroll}");
                        Assert.DoesNotContain("\r", text, $"CR on row {row} scrolled {scroll}");
                        Assert.DoesNotContain("\n", text, $"LF on row {row} scrolled {scroll}");
                        Assert.AreEqual(lines[row].Width, text.Length + CountOccurrences(text, "⚡"), $"Declared width of row {row} scrolled {scroll}");
                    }
                }
            })
            .Step("The help over the hostile overview and detail at every sampled width and height keeps the same invariants, and the panel is there from six columns and three rows", context =>
            {
                var cases = new (LiveMetricsSnapshot Snapshot, LiveDashboardViewState ViewState, string Name)[]
                {
                    (HostileSnapshot(), new LiveDashboardViewState { SelectedStepIndex = 1, ShowHelp = true }, "overview"),
                    (HostileDetailSnapshot(), DetailView(0) with { ShowHelp = true }, "detail")
                };
                foreach (var (snapshot, viewState, name) in cases)
                {
                    for (var width = 1; width <= 220; width += 2)
                    {
                        for (var height = 1; height <= 60; height++)
                        {
                            var lines = LiveDashboardLayout.Render(new[] { snapshot }, viewState, width, height, ColorMode.TrueColor);
                            var size = $"{width}×{height} on the {name}";

                            Assert.HasCount(height, lines, "Row count at " + size);
                            Assert.AreEqual(ExpectedHelpFooterWidth(width), lines[height - 1].Width, "Footer at " + size);
                            AssertMaximumWidth(width, lines);
                            AssertNoLoneSurrogate(lines, size);
                            Assert.AreEqual(width >= 13 && height >= 3, CountLinesContaining(lines, "? help") == 1, "Help panel at " + size);
                        }
                    }
                }
            })
            .Step("The help over a frame carrying surrogate pairs and the double-width logo never splits a pair, and a covered title row keeps the logo's declared width", context =>
            {
                // A frame of 14 rows puts the panel's top on row 0, over the title line and
                // its logo; the hostile scenario's name has no pair, so the overview's own
                // ticker rows — the rocket entries — are what the panel's edges cut.
                var lines = LiveDashboardLayout.Render(new[] { HostileSnapshot() }, new LiveDashboardViewState { ShowHelp = true }, 100, 14, ColorMode.None);

                // The title's text is a character short of its declared width — the logo's
                // lightning is declared two columns wide — and stays so once spliced: the
                // columns after the panel are taken short by the surplus, so the row ends on
                // the logo with no padding after it.
                Assert.HasCount(14, lines);
                Assert.StartsWith("Hostile  ● Running" + Spaces(14) + PanelTop("? help", HelpPanelWidth), lines[0].Text);
                Assert.EndsWith("⚡ TestFuzn", lines[0].Text);
                Assert.AreEqual(100, lines[0].Width);
                Assert.AreEqual(99, lines[0].Text.Length);
                AssertNoLoneSurrogate(lines, "100×14");

                var rockets = new LiveMetricsSnapshot
                {
                    ScenarioName = Rockets(60),
                    Errors = new[] { new LiveErrorEntry { StepName = Rockets(30), Message = Rockets(60), Count = 1 } }
                };
                // Every covered row is the width in characters — the title row a character
                // short of it once the logo rides it, from 80 columns, as the title itself is.
                for (var width = 60; width <= 130; width++)
                {
                    var frame = LiveDashboardLayout.Render(new[] { rockets }, LogView() with { ShowHelp = true }, width, 8, ColorMode.None);
                    AssertNoLoneSurrogate(frame, $"{width}×8");
                    AssertMaximumWidth(width, frame);
                    Assert.HasCount(8, frame);
                    for (var row = 0; row < 7; row++)
                        Assert.AreEqual(width, frame[row].Text.Length + CountOccurrences(frame[row].Text, "⚡"), $"Covered row {row} at {width}×8");
                }
            })
            .Run();
    }
}
