using System.Globalization;
using System.Text;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Thresholds;
using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Lays out the standalone runner's full-screen live load dashboard as one section per scenario
/// — a title line (scenario name, status badge, the compact logo top-right when the width
/// allows), a KPI tile row, the plan's timeline, a requests chart and a latency chart, a
/// latency heatmap, a requests panel with Ok/Failed rows (count, current rate and the
/// response-time spread), a per-step live table, and an error ticker — the sections stacked,
/// or side by side as columns from <see cref="MinimumWidthForColumns"/> columns of width (the
/// Columns paragraph), and closed by a key-hint footer that owns the window's last row. Pure
/// composition of the widgets in this namespace:
/// everything shown comes from the passed <see cref="LiveMetricsSnapshot"/>s and the
/// viewer's <see cref="LiveDashboardViewState"/> (no console, no clock — elapsed, ETA and the
/// error ticker's ages are snapshot values), so identical inputs render an identical frame,
/// which the <see cref="FrameRenderer"/> diff depends on. Every rate on screen is one measure —
/// the current-interval rate, on the rps tile, in the requests rows (the newest sample's ok and
/// failed deltas over its interval) and in the step rows alike — never a lifetime average, and
/// a value that is absent or not finite renders as an em dash instead of a fake zero.
/// <para>
/// <b>Tiles.</b> A <see cref="TileRowWidget"/> row of <see cref="StatTile"/>s: rps (the newest
/// interval's rate), p95 (the newest interval's Ok p95), errors (the newest interval's failed
/// share as a percentage), requests (ok + failed of the measurement phase, with "warmup n" on
/// the unit line once warmup requests exist) and elapsed (the run clock, the planned total
/// after a slash on the unit line, and a gauge of the progress fraction ending in the time
/// remaining), followed by one tile per declared p99 or mean threshold. The rps and p95 tiles
/// carry a delta against the sample <see cref="DeltaSampleDistance"/> samples earlier — the
/// signed change in the tile's own unit, hidden while the series is shorter than that, while
/// either endpoint is no data, and when the change rounds to zero in that unit (the number the
/// tile would show — a steady reading carries no arrow, never a permanent ▲ 0); up is good for
/// rps, bad for p95 — and a trend of the newest <see cref="TrendSampleCount"/> samples. An
/// interval p95 of zero is an idle second, not a latency: the p95 tile shows no data for it,
/// hides its delta and leaves a gap in its trend. An interval without requests
/// (<see cref="LiveMetricsSnapshot.IntervalRequestCount"/> zero — a stalled second, the drained
/// final sample) has no failed share: the errors tile shows no data for it, never a green
/// 0.0 %. A zero rate is a true reading and stays 0. A declared threshold turns its tile into
/// the gauge variant — the live reading against the limit, the evaluator's warning band
/// marked, the comparison passed straight through — and colours it by the live state (Ok,
/// Warning, Breached → Critical), placeholder and frozen readings included, as they come;
/// only the tile's value follows the no-data rule, the gauge stays as the threshold reports it.
/// Without one the tile's state is heuristic, computed only then: an error rate of zero is Ok,
/// one up to <see cref="ErrorRateWarningLimit"/> Warning and above it Critical; a p95 above
/// <see cref="ResponseTimeSpikeFactor"/> times the median of the trailing
/// <see cref="TrendSampleCount"/> non-idle samples is Warning once
/// <see cref="ResponseTimeHeuristicMinimumSampleCount"/> such samples exist, else Neutral;
/// no data, rps and the rest are Neutral. A tile's texts are sized to the box it lands in —
/// the warmup and planned units are left out when the value line would not fit the box's inner
/// width, so the widget never has to drop the tile for them — and its delta, gauge and trend
/// show when they fit, as the widget documents.
/// </para>
/// <para>
/// <b>Timeline and phase.</b> One <see cref="TimelineWidget"/> line (two with its legend) built
/// from the snapshot's plan entries one to one with the progress fraction as the position;
/// only a snapshot that carries a plan has one. The marker's segment is the phase, so the phase
/// label is written after the bar only when the plan gives the marker no position (an
/// indeterminate plan), and only while the bar keeps <see cref="MinimumTimelineWidth"/>
/// columns beside it. The phase is never invisible: when the section renders no timeline — a
/// snapshot without plan entries (the console manager's init placeholder) or a height below
/// <see cref="MinimumHeightForTimeline"/> — the phase label follows the status badge on the
/// title line after a dim middle dot, as long as the title keeps its full width with it: the
/// name and the badge come first, so a label that would truncate the line is dropped, the way
/// the timeline drops its own. A Failed status is never reason-less on screen —
/// <see cref="LiveMetricsSnapshot.StatusDetail"/> renders as its own styled line under the
/// timeline.
/// </para>
/// <para>
/// <b>Charts.</b> Two <see cref="ChartWidget"/> panels over the view state's time window
/// (<see cref="LiveDashboardViewState.TimeWindow"/>: the newest that many samples, every sample
/// without one) — stretched from the window's first sample and scrolling once the window
/// outgrows the body — in a <see cref="PanelWidget"/> frame each, the axis in the secondary
/// style. Each panel's header is its chart's legend: the series named top-down,
/// which is their paint order, so the first name is always the series whose newest value the
/// ▶ annotation shows (the widget annotates its first series). "requests — ok / failed": the
/// per-interval ok and failed counts (<see cref="LiveMetricsSnapshot.OkDeltaSeries"/>,
/// <see cref="LiveMetricsSnapshot.FailedDeltaSeries"/>) as two areas on one count scale, ok
/// first and failed painted over it in the failed colour — a line at the scale's floor would
/// replace the fill's bottom row, and a second scale would misstate the ratio, so a small
/// failed count is sub-cell and the legend is what names the colours — with the count format
/// on the axis and on the annotation, which is the newest ok count: the ok requests of the
/// interval, not the rps tile's rate (ok and failed together over the interval's length), so
/// the two need not read the same. "latency — p99 / p95 / p50": the per-interval p99 and p95
/// series as two areas, the p99 painted first and the p95 over it so each band shows where it
/// owns the cell, under the median as a line painted last — a flat median is a thin
/// median-coloured line along the scale's floor and a moving one a line through the bands, a
/// cell the line passes showing only the line's dots, as the widget composes — in the
/// palette's three response-time styles, with the response-time format on the axis and on the
/// annotation, which is the newest p99. An interval reading that is not a latency — an idle
/// interval's zero, a value no TimeSpan holds — is a gap in every band and in the line, never
/// a dip to zero: the p95 tile's own rule. A declared p95 or p99 threshold adds its limit as a
/// flat line over the p95 series' samples, <see cref="TerminalPalette.WarningStyle"/> for the
/// p95 limit and <see cref="TerminalPalette.FailedStyle"/> for the p99 one, both when both are
/// declared; the line shares the scale, which is what puts the bands in proportion to the
/// limit, and the title names it in the line's style ("· limit 500 ms", "· limits 500 ms /
/// 800 ms"). The panels sit side by side from <see cref="MinimumWidthForChartsSideBySide"/>
/// columns, stacked requests over latency from <see cref="MinimumWidthForCharts"/>, and are
/// dropped below that; their bodies are <see cref="ChartHeight"/> rows from
/// <see cref="MinimumHeightForTallCharts"/> rows of height and <see cref="CompactChartHeight"/>
/// below — or when the budget compacts them (the Height order).
/// </para>
/// <para>
/// <b>Heatmap.</b> A full-width "latency heatmap" panel under the charts: a
/// <see cref="HeatmapWidget"/> over <see cref="LiveMetricsSnapshot.LatencyBucketSeries"/>, the
/// charts' time window applied to it the same way (the newest that many samples, one per
/// column at the right of the body),
/// <see cref="HeatmapHeight"/> rows for the fifteen <see cref="LatencyBuckets"/> — the widget
/// merges every pair below the slowest bucket — labelled by their inclusive upper bounds,
/// "≤ 1 ms" through "≤ 30 s" and "> 30 s" for the open-ended last one, in the secondary style.
/// The budget takes its rows first, one at a time down to <see cref="MinimumHeatmapHeight"/>
/// (the widget merges further, from the middle outward), before anything else yields. Shown
/// only with the charts' width and from <see cref="MinimumHeightForHeatmap"/> rows of height.
/// </para>
/// <para>
/// <b>Steps.</b> A "Steps" panel with a <see cref="TableWidget"/> of the snapshot's top-level
/// steps, one row each, sorted by pain — the failure share (failed over ok + failed, the
/// fail% column's number) descending, then the newest interval p95 descending, a step
/// without a p95 reading after every step with one, and declaration order on a tie — so the
/// rows a shrinking budget keeps are the ones that hurt. The columns: a pointer column and
/// the name; count (ok + failed); rps (the step's current-interval rate, the shared no-data
/// rule); mean (the step's cumulative Ok mean); p95 — the newest sample of the step's
/// <see cref="LiveStepMetrics.ResponseTimePercentile95Series"/>, the interval reading, under
/// the p95 tile's rule (an idle interval's zero and anything no TimeSpan holds are no data),
/// so it can differ from the cumulative p95 the final summary prints; failed; fail% — a
/// <see cref="FailureBarCellCount"/>-cell bar of the failure share (a nonzero share fills at
/// least one cell) with the share as a percentage, both in the state the errors tile's
/// heuristic gives that share (zero Ok, up to <see cref="ErrorRateWarningLimit"/> Warning,
/// above it Critical; no requests at all is the empty track alone, Neutral); and trend, a
/// <see cref="StepTrendWidth"/>-column <see cref="SparklineWidget"/> of the newest samples of
/// the step's <see cref="LiveStepMetrics.RequestsPerSecondSeries"/> in the secondary style,
/// windowed as the widget documents. The column set is the widest tier whose natural width
/// fits the panel, like the requests table: the trend goes first, then mean and failed, and
/// only when even that tier cannot fit does the widget shrink columns. The view state's
/// <see cref="LiveDashboardViewState.SelectedStepIndex"/> — an index into the displayed,
/// pain-sorted rows, clamped into them as the type documents — marks its row with
/// <see cref="Pointer"/> in the pointer column and paints the whole row, padding to the
/// panel's inner width included, in reverse video: a single plain reverse span over the
/// row's text, so the highlight bar stays uniform where the cells' own colours would break
/// it. Unselected rows keep the pointer column blank, so the columns line up either way, and
/// in <see cref="ColorMode.None"/> the pointer is the whole highlight. Rows the height budget
/// hides — the least painful — are announced by a "+N more" line under the rows, in the
/// pointer column's indent.
/// </para>
/// <para>
/// <b>Errors.</b> An "Errors" panel — the ticker — of the snapshot's distinct errors, most
/// recently active first (<see cref="LiveErrorEntry.LastSeen"/> descending, then the count
/// descending, the snapshot's order on a tie), one entry each: the count right-aligned across
/// the ticker's entries with a × in the failed style, the current rate in parentheses per
/// second ("(2.0/s)", the rate format, right-aligned likewise, no data as the em dash)
/// between a space and two, the step name in bold (one space after the × when the rates are
/// off), a dim middle dot, the message, and after three spaces the ages in the secondary
/// style — "first 1m 12s ago · last 2s ago", the plan's duration
/// format, each measured from the newest sample's timestamp (the instant the snapshot
/// describes; never the clock) and floored at zero. Step names and messages are exception
/// text: markup-escaped, their control characters sanitized to spaces up front, so a message
/// can never widen a row or leak markup or an escape. An entry wraps to the panel's inner
/// width: greedy word wrapping at the last space that fits (a run without one breaks at the
/// width, never between a surrogate pair), the continuation lines indented under the step
/// name unless that indent would take more than half the panel, and each line re-marked-up
/// from its own runs, so a break never lands inside a tag or an SGR sequence; an entry
/// keeps at most <see cref="MaximumErrorEntryLines"/> lines, the last ending in an ellipsis
/// when the text goes on. The pieces go by width: the ages below
/// <see cref="MinimumWidthForErrorAges"/> columns (and while the snapshot has no sample to
/// measure them from), the rates below <see cref="MinimumWidthForErrorRates"/>. Entries the
/// budget hides and distinct errors beyond what the snapshot carries
/// (<see cref="LiveMetricsSnapshot.DistinctErrorCount"/> over the list) are announced
/// together by a "+N more" line under the entries.
/// </para>
/// <para>
/// <b>Width.</b> No emitted line is ever wider than the given width at any width: the logo
/// drops below <see cref="MinimumWidthForLogo"/> columns and the tile trends below
/// <see cref="MinimumWidthForTileTrends"/>; the tiles wrap onto two rows, the first taking the
/// larger half, when the row cannot give every box <see cref="MinimumTileBoxWidth"/> columns
/// (<see cref="MinimumWidthForSingleTileRow"/> — 64 for the five standard tiles: the
/// eight-column clock or the eight-letter requests label plus the box's borders and padding;
/// a wider value, a nine-digit count, drops its tile from the right as the widget does); the
/// chart panels stack below <see cref="MinimumWidthForChartsSideBySide"/> and go, with the
/// heatmap, below <see cref="MinimumWidthForCharts"/>; the error ticker drops its ages below
/// <see cref="MinimumWidthForErrorAges"/> and its rates below
/// <see cref="MinimumWidthForErrorRates"/>; the requests and steps tables narrow their column
/// sets by the columns' actual content widths (min/p75/p99 go first, then p50/max; the trend
/// first, then mean/failed) so a number is never cut short; and the remaining pieces degrade
/// through the widgets' own narrow-width behavior down to rendering nothing at degenerate
/// widths.
/// </para>
/// <para>
/// <b>Height.</b> The frame is fitted to the height: content is clipped to the rows above the
/// footer (a frame taller than the window loses its tail, never the quit hint — each section
/// leads with its most important lines) and padded down so the footer lands on the last row;
/// a height below 1 leaves the frame unclipped and unpadded with every optional row in. A
/// section gives rows back in one order. The first four steps follow the window's height
/// alone; the rest are taken only while the section still overruns its budget — the rows
/// above the footer, less what the sections before it took (a section stacked under another,
/// a row of columns under another, a wrapped tile block) — each step only while it does, so a
/// section pays what it owes and
/// no more wherever a step can be measured out:
/// <list type="number">
/// <item><description>the heatmap panel goes below <see cref="MinimumHeightForHeatmap"/> rows;</description></item>
/// <item><description>two rows of each chart body (<see cref="ChartHeight"/> to <see cref="CompactChartHeight"/>) and the tile trends go below <see cref="MinimumHeightForTallCharts"/> (one step — <see cref="MinimumHeightForTileTrends"/> is the same height);</description></item>
/// <item><description>the tile gauges (the threshold bars and the elapsed tile's progress with its time remaining) go below <see cref="MinimumHeightForTileGauges"/>;</description></item>
/// <item><description>the timeline goes below <see cref="MinimumHeightForTimeline"/>;</description></item>
/// <item><description>the heatmap body gives back a row at a time, from <see cref="HeatmapHeight"/> down to <see cref="MinimumHeatmapHeight"/> — the widget merges more buckets per row — so a small overrun costs exactly its rows;</description></item>
/// <item><description>the heatmap panel goes whole;</description></item>
/// <item><description>the chart bodies compact from <see cref="ChartHeight"/> to <see cref="CompactChartHeight"/> rows when the height left them tall — side by side the pair together, two rows; stacked, four;</description></item>
/// <item><description>the step rows go, last first (the least painful), down to one row;</description></item>
/// <item><description>the error entries go likewise, least recently active first, down to one entry;</description></item>
/// <item><description>the latency chart goes — stacked, alone; side by side the pair splits and the requests chart spans the width, which frees no row, so the next step follows at once;</description></item>
/// <item><description>the requests chart goes;</description></item>
/// <item><description>the Steps panel goes whole;</description></item>
/// <item><description>the Errors panel goes whole;</description></item>
/// <item><description>and last the clipping above, which cuts the requests panel from its bottom border up and never the footer.</description></item>
/// </list>
/// A table is never a header alone: a panel that cannot keep a row is dropped whole, and
/// hidden rows are never silent — the first row hidden costs its panel a "+N more" line, so a
/// table whose rows are all needed gives nothing back (two rows cannot become one and a more
/// line) and the panel goes instead. The steps are greedy and never undone: rows a table gave
/// back stay hidden when a later step frees more than the section still owed. So a window of
/// 12 to 14 rows shows the title with the phase after the badge, the two-line tiles and the
/// requests panel whole, and one of 10 or 11 rows the requests panel cut after its header
/// lines.
/// </para>
/// <para>
/// <b>Columns.</b> Two or more scenarios render side by side from
/// <see cref="MinimumWidthForColumns"/> columns of width — three abreast from
/// <see cref="MinimumWidthForThreeColumns"/> with three or more, never more than three — and
/// stack, one section under another with a blank line between, below that width or with one
/// scenario. The columns share the width less a <see cref="ColumnGap"/>-column gap between
/// neighbours equally, the columns left over widening the first columns by one each, the way
/// the tile boxes share a row; the thresholds are where the narrowest column reaches
/// <see cref="MinimumWidthForCharts"/> — two and three scenarios' worth of it with the gaps
/// between, 122 and 184 — so a column has its charts from the first width it exists at (two
/// chartless columns would be worse than the stacked frame they replace), and the widths
/// just past a threshold (123; 185 and 186) widen the first columns by the leftover. A column
/// is the full section laid out at the column's width, the pieces going by that width as
/// they would in a window that wide — the tiles wrap, the charts stack, the ticker sheds its
/// ages — except the heatmap, which a column never shows whatever the height: its rows are
/// what a column cannot spare, and that is column mode's one rule of its own. The scenarios
/// are dealt into rows of columns in order — with more scenarios than columns the next row
/// starts under the first, after a blank line, its scenarios at the same column widths and
/// any column it leaves empty blank, so every scenario in the frame is the width of its
/// column — and each row of columns is fitted into the rows the ones before it left, every
/// column against that same budget; a row's columns are padded with blank lines to its
/// tallest and joined row by row into lines of exactly the width — a column's line wider
/// than its column (a layout bug: every piece renders to the width it is given) is cut to it
/// with an ellipsis rather than trusted, since a frame row past the window would wrap on a
/// real terminal and corrupt every row below. The logo rides the last column of the first
/// row of columns alone — the frame's top-right, where the stacked frame puts it, and never a
/// column with a right neighbour: the compact logo's lightning is declared two columns wide
/// (<see cref="LogoWidget.CompactWidth"/>), and a terminal that draws it single-width would
/// leave that column a cell short and pull its neighbour's title row a cell left — when that
/// column keeps <see cref="MinimumWidthForLogo"/>; the footer is one for the frame either
/// way, and the view state's step selection applies in every column alike.
/// </para>
/// <para>
/// <b>Interaction.</b> The rest of the view state: the view, the pause and the help, which
/// <see cref="LiveDashboardKeyHandler"/> sets. While the state is paused every section's
/// status badge is followed by a dim middle dot and <c>⏸ paused</c> in
/// <see cref="TerminalPalette.PausedStyle"/> — part of the badge, so it comes before the phase
/// label and is never dropped for it; the numbers on screen are then the console manager's
/// frozen snapshots, and the layout only says so. The footer is a <see cref="KeyHintBarWidget"/>
/// line of the view's hints followed, last and always, by the quit hint (<see cref="QuitKey"/>):
/// the overview's <c>1 overview · 2 step · 3 errors · ↑↓ select · ⏎ detail · p pause ·
/// +- window · ? help · q quit</c>, the step detail's <c>Esc back · ↑↓ step · 1 overview ·
/// 3 errors · p pause · +- window · ? help · q quit</c>, the error log's <c>Esc back ·
/// ↑↓ scroll · 1 overview · 2 step · p pause · ? help · q quit</c>, and <c>? close ·
/// Esc close · q quit</c> whatever the view while the help is up. The widget drops the hints
/// that do not fit from the right, which would take the quit hint first, so the footer is the
/// longest prefix of the view's hints that fits the width together with the quit hint — the
/// view's hints go from the right, the quit hint never — and the quit hint alone, cut to the
/// width with the widget's ellipsis, below its six columns. Every view lays out the overview
/// body under its own footer until the step detail and the error log have bodies of their own;
/// <see cref="LiveDashboardViewState.ErrorLogScroll"/> and the help have nothing to render yet.
/// </para>
/// Alternate-screen entry/exit and the render loop are the caller's job. Stateless and
/// thread-safe.
/// </summary>
internal static class LiveDashboardLayout
{
    /// <summary>Below this width the title line that carries the compact logo — the first section's in the stacked frame, the last column's of the first row of columns — drops it.</summary>
    public const int MinimumWidthForLogo = 80;

    /// <summary>Below this width the chart panels and the heatmap panel are dropped.</summary>
    public const int MinimumWidthForCharts = 60;

    /// <summary>From this width the requests and latency chart panels sit side by side; below it they stack.</summary>
    public const int MinimumWidthForChartsSideBySide = 100;

    /// <summary>Below this width the tiles carry no trend sparkline.</summary>
    public const int MinimumWidthForTileTrends = 80;

    /// <summary>Below this width the error ticker's entries carry no first-seen and last-seen ages.</summary>
    public const int MinimumWidthForErrorAges = 80;

    /// <summary>Below this width the error ticker's entries carry no current rate.</summary>
    public const int MinimumWidthForErrorRates = 60;

    /// <summary>From this width two or more scenarios render side by side as two columns — two of <see cref="MinimumWidthForCharts"/> and the gap between them, the width at which the narrower column reaches the charts' width; below it they stack.</summary>
    public const int MinimumWidthForColumns = (2 * MinimumWidthForCharts) + ColumnGap;

    /// <summary>From this width three or more scenarios render as three columns — three of <see cref="MinimumWidthForCharts"/> and the two gaps between them, the width at which the narrowest column reaches the charts' width — and never more.</summary>
    public const int MinimumWidthForThreeColumns = (3 * MinimumWidthForCharts) + (2 * ColumnGap);

    /// <summary>
    /// The narrowest box the tiles share one row at: the eight-column clock or requests label
    /// plus the box's borders and padding. A row that cannot give every box this many columns
    /// wraps onto two rows (see <see cref="MinimumWidthForSingleTileRow"/>).
    /// </summary>
    public const int MinimumTileBoxWidth = 12;

    /// <summary>Below this height the heatmap panel is dropped.</summary>
    public const int MinimumHeightForHeatmap = 36;

    /// <summary>Below this height the chart bodies shrink from <see cref="ChartHeight"/> to <see cref="CompactChartHeight"/> rows — the same step as the tile trends.</summary>
    public const int MinimumHeightForTallCharts = 30;

    /// <summary>Below this height the tiles carry no trend sparkline — the same step as the chart bodies.</summary>
    public const int MinimumHeightForTileTrends = 30;

    /// <summary>Below this height the tiles carry no gauge — neither the threshold bars nor the elapsed tile's progress and time remaining.</summary>
    public const int MinimumHeightForTileGauges = 20;

    /// <summary>Below this height the timeline is dropped.</summary>
    public const int MinimumHeightForTimeline = 15;

    /// <summary>The rows of a chart body from <see cref="MinimumHeightForTallCharts"/> rows of height.</summary>
    public const int ChartHeight = 6;

    /// <summary>The rows of a chart body below <see cref="MinimumHeightForTallCharts"/> rows of height.</summary>
    public const int CompactChartHeight = 4;

    /// <summary>The rows of the heatmap body when the budget allows; the drop order takes them one at a time down to <see cref="MinimumHeatmapHeight"/>.</summary>
    public const int HeatmapHeight = 8;

    /// <summary>The fewest rows the heatmap body keeps under the budget: a section still over at this height drops the panel whole rather than shrinking it further.</summary>
    public const int MinimumHeatmapHeight = 4;

    /// <summary>The columns of a step row's trend — its rate sparkline — in the steps table's widest column set.</summary>
    public const int StepTrendWidth = 12;

    /// <summary>The most lines one error ticker entry wraps onto; a longer entry's last line ends in an ellipsis.</summary>
    public const int MaximumErrorEntryLines = 3;

    /// <summary>The cells of a step row's fail% bar; a nonzero failure share fills at least one.</summary>
    public const int FailureBarCellCount = 5;

    /// <summary>The glyph in the pointer column of the Steps table's selected row; the column is blank on every other row.</summary>
    public const string Pointer = "▸";

    /// <summary>The fewest columns the timeline bar keeps when the phase label is written after it; a label that would leave fewer is dropped.</summary>
    public const int MinimumTimelineWidth = 24;

    /// <summary>The samples a tile's trend shows, newest last, and the window the p95 heuristic takes its median over.</summary>
    public const int TrendSampleCount = 60;

    /// <summary>How many samples back a tile's delta compares the newest sample against.</summary>
    public const int DeltaSampleDistance = 10;

    /// <summary>Without a declared error-rate threshold, a nonzero error rate up to this share is Warning and above it Critical.</summary>
    public const double ErrorRateWarningLimit = 0.01;

    /// <summary>Without a declared p95 threshold, an interval p95 above this many times the trailing median is Warning.</summary>
    public const double ResponseTimeSpikeFactor = 2;

    /// <summary>The fewest non-idle p95 samples in the trailing window for the median to be worth judging by.</summary>
    public const int ResponseTimeHeuristicMinimumSampleCount = 10;

    // Every style comes from <see cref="TerminalPalette"/>, which the final summary
    // (<see cref="LoadSummaryLayout"/>) shares, so the summary in the scrollback reads as the
    // dashboard's sibling and retheming is a one-place edit.

    // What every place with no number to show renders: a value that is absent (no sample yet)
    // or not finite. The tile widget styles its own value, so the tiles take the bare text.
    private const string NoDataText = "—";
    private const string NoDataMarkup = "[" + TerminalPalette.SecondaryStyle + "]" + NoDataText + "[/]";

    // The tile labels: the pty smoke and the summaries look for these words.
    private const string RateTileLabel = "rps";
    private const string ResponseTimeTileLabel = "p95";
    private const string ErrorsTileLabel = "errors";
    private const string RequestsTileLabel = "requests";
    private const string ElapsedTileLabel = "elapsed";

    private const string ResponseTimeUnit = "ms";
    private const string PercentUnit = "%";
    private const string WarmupUnitPrefix = "warmup ";
    private const string PlannedUnitPrefix = "/ ";

    // The chart panel headers double as the charts' legends: the widget has none, so the
    // series' words are written here in the series' styles, top-down in the series' paint
    // order — the first word names the series the widget's ▶ annotation reads.
    private const string RequestsChartHeader = "[" + TerminalPalette.PanelHeaderStyle + "]requests[/] [" + TerminalPalette.SecondaryStyle + "]—[/] ["
        + TerminalPalette.OkStyle + "]ok[/] [" + TerminalPalette.SecondaryStyle + "]/[/] [" + TerminalPalette.FailedStyle + "]failed[/]";
    private const string LatencyChartHeaderPrefix = "[" + TerminalPalette.PanelHeaderStyle + "]latency[/] [" + TerminalPalette.SecondaryStyle + "]—[/] ["
        + TerminalPalette.ResponseTimePercentile99Style + "]p99[/] [" + TerminalPalette.SecondaryStyle + "]/[/] ["
        + TerminalPalette.ResponseTimePercentile95Style + "]p95[/] [" + TerminalPalette.SecondaryStyle + "]/[/] ["
        + TerminalPalette.ResponseTimeMedianStyle + "]p50[/]";
    private const string HeatmapHeader = "[" + TerminalPalette.PanelHeaderStyle + "]latency heatmap[/]";

    // The tiles every scenario has, in row order: rps, p95, errors, requests, elapsed; a
    // declared p99 or mean threshold adds its tile after them.
    private const int StandardTileCount = 5;

    // Columns between horizontally adjacent pieces: title and logo, the two chart panels, the
    // timeline bar and the phase label after it, and two scenario columns.
    private const int ColumnGap = 2;

    // The rows a PanelWidget adds around its content: the top and bottom borders.
    private const int PanelFrameRows = 2;

    // The pointer column of a step row while the row is not selected: one blank column, so
    // the names line up with the selected row's.
    private const string PointerColumnBlank = " ";

    private const string StepsHeader = "[" + TerminalPalette.PanelHeaderStyle + "]Steps[/]";
    private const string ErrorsHeader = "[" + TerminalPalette.PanelHeaderStyle + "]Errors[/]";

    // The largest magnitude in milliseconds a TimeSpan can hold (long.MaxValue ticks), for
    // guarding series values that never went through a TimeSpan.
    private const double MaxResponseTimeMilliseconds = long.MaxValue / TimeSpan.TicksPerMillisecond;

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    /// <summary>The key the footer advertises for a graceful stop of the run; the console manager acts on it in either case.</summary>
    public const char QuitKey = 'q';

    /// <summary>The paused badge's text, after the status badge on every section's title line while the view state is paused.</summary>
    public const string PausedBadgeText = "⏸ paused";

    // The footer's hints: the quit hint closes every view's set, and the view's own hints are
    // given up from the right before it (see the class summary's Interaction).
    private static readonly KeyHint QuitHint = new KeyHint(QuitKey.ToString(), "quit");

    private static readonly KeyHint[] OverviewHints =
    {
        new KeyHint("1", "overview"),
        new KeyHint("2", "step"),
        new KeyHint("3", "errors"),
        new KeyHint("↑↓", "select"),
        new KeyHint("⏎", "detail"),
        new KeyHint(LiveDashboardKeyHandler.PauseKey.ToString(), "pause"),
        new KeyHint(LiveDashboardKeyHandler.WidenTimeWindowKey.ToString() + LiveDashboardKeyHandler.NarrowTimeWindowKey, "window"),
        new KeyHint(LiveDashboardKeyHandler.HelpKey.ToString(), "help")
    };

    private static readonly KeyHint[] StepDetailHints =
    {
        new KeyHint("Esc", "back"),
        new KeyHint("↑↓", "step"),
        new KeyHint("1", "overview"),
        new KeyHint("3", "errors"),
        new KeyHint(LiveDashboardKeyHandler.PauseKey.ToString(), "pause"),
        new KeyHint(LiveDashboardKeyHandler.WidenTimeWindowKey.ToString() + LiveDashboardKeyHandler.NarrowTimeWindowKey, "window"),
        new KeyHint(LiveDashboardKeyHandler.HelpKey.ToString(), "help")
    };

    private static readonly KeyHint[] ErrorLogHints =
    {
        new KeyHint("Esc", "back"),
        new KeyHint("↑↓", "scroll"),
        new KeyHint("1", "overview"),
        new KeyHint("2", "step"),
        new KeyHint(LiveDashboardKeyHandler.PauseKey.ToString(), "pause"),
        new KeyHint(LiveDashboardKeyHandler.HelpKey.ToString(), "help")
    };

    private static readonly KeyHint[] HelpHints =
    {
        new KeyHint(LiveDashboardKeyHandler.HelpKey.ToString(), "close"),
        new KeyHint("Esc", "close")
    };

    // The chart and heatmap panels' options: the layout's own formats and styles over every
    // sample, shared across renders (the options are immutable); a view state's time window
    // is laid over a copy per render (ChartOptionsFor, HeatmapOptionsFor), so only a windowed
    // frame allocates.
    private static readonly ChartOptions RequestsChartOptions = new ChartOptions { ValueFormatter = FormatCountLabel, AxisStyle = TerminalPalette.SecondaryStyle };
    private static readonly ChartOptions LatencyChartOptions = new ChartOptions { ValueFormatter = FormatResponseTimeLabel, AxisStyle = TerminalPalette.SecondaryStyle };
    private static readonly HeatmapOptions LatencyHeatmapOptions = new HeatmapOptions { LabelStyle = TerminalPalette.SecondaryStyle };

    // The heatmap's row labels, one per latency bucket in bucket order: each bucket's inclusive
    // upper bound, the open-ended last bucket as everything above the last bound.
    private static readonly string[] LatencyBucketLabels = BuildLatencyBucketLabels();

    private static readonly TableColumn[] RequestsColumns =
    {
        new TableColumn(string.Empty),
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]count[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]rps[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]min[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]mean[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]p50[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]p75[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]p95[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]p99[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]max[/]") { Alignment = TextAlignment.Right }
    };

    // The requests table's column sets as indexes into RequestsColumns, widest first: the full
    // spread, then without min/p75/p99, then without p50/max as well — count, rps, mean and p95
    // always stay. The set in use is the widest whose natural width (the columns' actual
    // content — a 9-digit count or an hour-class response time widens its column) fits the
    // panel, so a number is never truncated: a right-aligned number cut from the right reads
    // as a smaller number.
    private static readonly int[][] RequestsColumnTiers =
    {
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
        new[] { 0, 1, 2, 4, 5, 7, 9 },
        new[] { 0, 1, 2, 4, 7 }
    };

    // The steps table's columns; the first holds the pointer column and the step name, its
    // header indented past the pointer column so the names line up under "step".
    private static readonly TableColumn[] StepColumns =
    {
        new TableColumn(PointerColumnBlank + "[" + TerminalPalette.SecondaryStyle + "]step[/]") { MaxWidth = 32 },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]count[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]rps[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]mean[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]p95[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]failed[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]fail%[/]"),
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]trend[/]")
    };

    // The steps table's column sets as indexes into StepColumns, widest first: everything,
    // then without the trend, then without mean and failed as well — the name, count, rps,
    // p95 and fail% always stay. Chosen by natural width like the requests table's tiers.
    private static readonly int[][] StepColumnTiers =
    {
        new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
        new[] { 0, 1, 2, 3, 4, 5, 6 },
        new[] { 0, 1, 2, 4, 6 }
    };

    /// <summary>
    /// Renders the full dashboard frame for the given scenario snapshots, in order — stacked,
    /// or side by side in columns from <see cref="MinimumWidthForColumns"/> columns of width,
    /// as the class summary's Columns describes — under the viewer's state, at the given
    /// window size. Returns one <see cref="RenderedLine"/> per terminal row, ready for
    /// <see cref="FrameBuffer.AddLines(IEnumerable{RenderedLine})"/>: exactly
    /// <paramref name="height"/> rows for a height of 1 or more, the content clipped or padded
    /// to the rows above the footer on the last row, each section's optional rows given up in
    /// the order the class summary describes; the unclipped content plus the footer, every
    /// optional row in, for a smaller height. A width below 1 renders nothing. The view
    /// state's step selection, pause badge and time window apply to every section alike, and
    /// its view and help choose the footer's hints (the Interaction paragraph). The glyph set is passed through
    /// to the tile trends, the chart panels and the step trends so the caller can match it to
    /// the terminal's font support. The spinner glyph, when given, is drawn in place of the
    /// dot on a running scenario's status badge — a single-column glyph the caller's render
    /// loop advances per frame; null keeps the dot, and finished badges (passed, failed,
    /// skipped) always keep theirs.
    /// </summary>
    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<LiveMetricsSnapshot> snapshots, LiveDashboardViewState viewState, int width, int height, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet = SparklineGlyphSet.Braille, string? spinnerGlyph = null)
    {
        if (snapshots == null)
            throw new ArgumentNullException(nameof(snapshots), "Snapshots cannot be null.");

        if (width < 1)
            return Array.Empty<RenderedLine>();

        // The rows above the footer, handed to the sections in order: each one is fitted into
        // what the ones before it left. An unbounded height has rows for everything.
        var remainingRows = height >= 1 ? height - 1 : int.MaxValue;

        var lines = new List<RenderedLine>();
        var columnCount = ColumnCount(snapshots.Count, width);
        if (columnCount > 1)
        {
            AddColumnRows(lines, snapshots, viewState, width, height, columnCount, remainingRows, colorMode, sparklineGlyphSet, spinnerGlyph);
        }
        else
        {
            for (var index = 0; index < snapshots.Count; index++)
            {
                if (index > 0)
                {
                    lines.Add(BlankLine);
                    remainingRows--;
                }

                var sectionStart = lines.Count;
                AddScenarioSection(lines, snapshots[index], viewState, width, height, remainingRows, colorMode, sparklineGlyphSet, spinnerGlyph, includeLogo: index == 0 && width >= MinimumWidthForLogo, includeHeatmap: true);
                remainingRows -= lines.Count - sectionStart;
            }
        }

        // The footer owns the last row: content is cut to the rows above it (a frame taller
        // than the window would otherwise push the quit hint off screen) and padded down to it.
        if (height >= 1)
        {
            var contentHeight = height - 1;
            if (lines.Count > contentHeight)
                lines.RemoveRange(contentHeight, lines.Count - contentHeight);

            while (lines.Count < contentHeight)
                lines.Add(BlankLine);
        }

        lines.AddRange(RenderFooter(viewState, width, colorMode));
        return lines;
    }

    // The footer: the longest prefix of the view's hints that fits the width together with the
    // quit hint, else the quit hint alone, cut to the width by the widget. A candidate fits
    // when the widget renders it whole at an unbounded width no wider than the width — measured
    // through the widget itself, so the fit follows its hint format and separator wherever they
    // go — and that rendering is the footer: a line that fits renders the same at any width,
    // so the winner is not rendered twice. The width is 1 or more here, so the widget always
    // returns its one line.
    private static IReadOnlyList<RenderedLine> RenderFooter(LiveDashboardViewState viewState, int width, ColorMode colorMode)
    {
        var viewHints = FooterHintsFor(viewState);
        for (var count = viewHints.Length; count > 0; count--)
        {
            var hints = new KeyHint[count + 1];
            Array.Copy(viewHints, hints, count);
            hints[count] = QuitHint;

            var candidate = KeyHintBarWidget.Render(hints, int.MaxValue, colorMode);
            if (candidate[0].Width <= width)
                return candidate;
        }

        return KeyHintBarWidget.Render(new[] { QuitHint }, width, colorMode);
    }

    // The view's own hints, before the quit hint: how to close the help while it is up,
    // whatever the view, else the view's set.
    private static KeyHint[] FooterHintsFor(LiveDashboardViewState viewState)
    {
        if (viewState.ShowHelp)
            return HelpHints;

        switch (viewState.View)
        {
            case LiveDashboardView.StepDetail:
                return StepDetailHints;
            case LiveDashboardView.ErrorLog:
                return ErrorLogHints;
            default:
                return OverviewHints;
        }
    }

    /// <summary>
    /// The narrowest width at which <paramref name="tileCount"/> tiles share one row:
    /// <see cref="MinimumTileBoxWidth"/> per box and the widget's gap between boxes.
    /// </summary>
    public static int MinimumWidthForSingleTileRow(int tileCount)
    {
        return (tileCount * MinimumTileBoxWidth) + ((tileCount - 1) * TileRowWidget.Gap);
    }

    // The columns the frame renders the snapshots in: one below MinimumWidthForColumns or with
    // a single snapshot, two from there, and three from MinimumWidthForThreeColumns with three
    // or more snapshots — never more.
    private static int ColumnCount(int snapshotCount, int width)
    {
        if (snapshotCount < 2 || width < MinimumWidthForColumns)
            return 1;

        if (snapshotCount >= 3 && width >= MinimumWidthForThreeColumns)
            return 3;

        return 2;
    }

    // The widths of count pieces that share the width less gap columns between neighbours
    // equally, the columns left over widening the first pieces by one each: the tile boxes'
    // rule, which the tile row mirrors (AddTileRow) and the scenario columns follow.
    private static int[] ShareWidth(int count, int width, int gap)
    {
        var available = width - ((count - 1) * gap);
        var baseWidth = Math.DivRem(available, count, out var leftover);
        var widths = new int[count];
        for (var index = 0; index < count; index++)
            widths[index] = baseWidth + (index < leftover ? 1 : 0);

        return widths;
    }

    // The snapshots dealt into rows of columnCount columns, in order, each row of columns
    // fitted into the rows the ones before it left with a blank line between rows: every
    // column of a row is the full section at its column's width, without the heatmap, planned
    // against the same budget, and the row's columns are padded to its tallest and joined row
    // by row. A last row with fewer snapshots than columns leaves its spare columns blank. The
    // logo goes to the last column of the first row alone — the frame's top-right, never a
    // column with a right neighbour — when its width allows one.
    private static void AddColumnRows(List<RenderedLine> lines, IReadOnlyList<LiveMetricsSnapshot> snapshots, LiveDashboardViewState viewState, int width, int height, int columnCount, int remainingRows, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet, string? spinnerGlyph)
    {
        var columnWidths = ShareWidth(columnCount, width, ColumnGap);
        for (var first = 0; first < snapshots.Count; first += columnCount)
        {
            if (first > 0)
            {
                lines.Add(BlankLine);
                remainingRows--;
            }

            var columns = new IReadOnlyList<RenderedLine>[columnCount];
            var tallest = 0;
            for (var column = 0; column < columnCount; column++)
            {
                var section = new List<RenderedLine>();
                var index = first + column;
                if (index < snapshots.Count)
                    AddScenarioSection(section, snapshots[index], viewState, columnWidths[column], height, remainingRows, colorMode, sparklineGlyphSet, spinnerGlyph, includeLogo: first == 0 && column == columnCount - 1 && columnWidths[column] >= MinimumWidthForLogo, includeHeatmap: false);

                columns[column] = section;
                tallest = Math.Max(tallest, section.Count);
            }

            for (var row = 0; row < tallest; row++)
                lines.Add(JoinSideBySide(columns, columnWidths, row));

            remainingRows -= tallest;
        }
    }

    // One frame line of pieces laid side by side — scenario columns, the chart pair, the
    // title and the logo: each piece's line for that row (a blank one past the piece's last)
    // padded to the piece's width by its declared width and joined with the gap; the widths
    // and the gaps between them add up to the line's, which it declares. A line wider than
    // its piece is cut to it (CutToWidth) rather than trusted, so a row can never outgrow the
    // width the frame is laid out for.
    private static RenderedLine JoinSideBySide(IReadOnlyList<RenderedLine>[] pieces, int[] widths, int row)
    {
        var text = new StringBuilder();
        var width = 0;
        for (var index = 0; index < pieces.Length; index++)
        {
            if (index > 0)
            {
                text.Append(' ', ColumnGap);
                width += ColumnGap;
            }

            var line = row < pieces[index].Count ? pieces[index][row] : BlankLine;
            if (line.Width > widths[index])
                line = CutToWidth(line, widths[index]);

            text.Append(line.Text).Append(' ', widths[index] - line.Width);
            width += widths[index];
        }

        return new RenderedLine(text.ToString(), width);
    }

    // A line cut to a width it exceeds: its first width − 1 columns and an ellipsis, as
    // MarkupText cuts markup — a column per character, a cut that would split a surrogate
    // pair backing off one and the row padded out — with every SGR sequence on the way kept
    // (they take no column) and the styling closed by a reset after the cut when the line
    // carried any. No widget emits such a line, each renders to the width it is given, and a
    // rendered line's text is final, never parsed back — so this is the one place the layout
    // reads past a line's escapes, and only to contain a fault: a frame row wider than the
    // window would wrap on a real terminal and corrupt every row below it.
    private static RenderedLine CutToWidth(RenderedLine line, int width)
    {
        if (width < 1)
            return BlankLine;

        var text = line.Text;
        var cut = new StringBuilder(text.Length);
        var kept = 0;
        var styled = false;
        var position = 0;
        while (position < text.Length && kept < width - 1)
        {
            var character = text[position];
            if (character == AnsiCodes.Escape[0])
            {
                var end = EndOfEscapeSequence(text, position);
                cut.Append(text, position, end - position);
                position = end;
                styled = true;
                continue;
            }

            if (char.IsHighSurrogate(character) && position + 1 < text.Length && char.IsLowSurrogate(text[position + 1]) && kept + 2 > width - 1)
                break;

            cut.Append(character);
            kept++;
            position++;
        }

        cut.Append(MarkupText.Ellipsis);
        if (styled)
            cut.Append(AnsiCodes.Reset);

        cut.Append(' ', width - 1 - kept);
        return new RenderedLine(cut.ToString(), width);
    }

    // The index past the escape sequence starting at position: a CSI sequence — the escape,
    // '[', its parameter and intermediate bytes and its final byte — the only kind the
    // engine's styling emits; an escape byte that starts no such sequence is one on its own.
    private static int EndOfEscapeSequence(string text, int position)
    {
        var end = position + 1;
        if (end < text.Length && text[end] == '[')
        {
            end++;
            while (end < text.Length && text[end] >= ' ' && text[end] <= '?')
                end++;

            if (end < text.Length && text[end] >= '@' && text[end] <= '~')
                end++;
        }

        return end;
    }

    // One scenario's section: the header block (title, tiles, timeline, status detail, a
    // blank), the chart and heatmap panels, the requests panel, the steps table and the errors
    // panel — the header and the requests panel rendered first, and the step rows sorted and
    // the error entries laid out (their line counts are what the budget trims by), since the
    // budget steps of the drop order fit the rest of the section around them. The heatmap is
    // offered only when the caller allows it: a column never shows one.
    private static void AddScenarioSection(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, LiveDashboardViewState viewState, int width, int height, int budget, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet, string? spinnerGlyph, bool includeLogo, bool includeHeatmap)
    {
        var thresholds = new DeclaredThresholds(snapshot.Thresholds);
        var includeTimeline = snapshot.PlanEntries.Count > 0 && HasRowsFor(height, MinimumHeightForTimeline);

        var header = new List<RenderedLine>();
        header.Add(RenderTitleLine(snapshot, viewState.IsPaused, width, colorMode, spinnerGlyph, includeLogo, includePhaseLabel: !includeTimeline));
        AddTileRows(header, snapshot, thresholds, width, height, colorMode, sparklineGlyphSet);

        if (includeTimeline)
            AddTimeline(header, snapshot, width, colorMode);

        if (snapshot.StatusDetail != null)
            header.Add(MarkupText.RenderTruncated("[" + TerminalPalette.FailedStyle + "]✗ " + MarkupParser.Escape(snapshot.StatusDetail) + "[/]", width, colorMode));

        header.Add(BlankLine);

        var requests = RenderRequestsPanel(snapshot, width, colorMode);
        var steps = SortStepsByPain(snapshot.Steps);
        var errorEntries = RenderErrorEntries(snapshot, width, colorMode);
        var distinctErrorCount = Math.Max(snapshot.DistinctErrorCount, snapshot.Errors.Count);
        var plan = PlanSection(width, height, header.Count + requests.Count, budget, steps.Count, errorEntries, distinctErrorCount, includeHeatmap);

        lines.AddRange(header);

        if (plan.IncludeRequestsChart || plan.IncludeLatencyChart)
            AddChartPanels(lines, snapshot, thresholds, plan, viewState.TimeWindow, width, colorMode, sparklineGlyphSet);

        if (plan.IncludeHeatmap)
            lines.AddRange(RenderHeatmapPanel(snapshot, viewState.TimeWindow, width, plan.HeatmapHeight, colorMode));

        lines.AddRange(requests);

        if (plan.IncludeSteps)
            AddStepsPanel(lines, steps, plan.StepRowCount, SelectedStepRow(viewState, plan.StepRowCount), width, colorMode, sparklineGlyphSet);

        if (plan.IncludeErrors)
            AddErrorsPanel(lines, errorEntries, plan.ErrorEntryCount, distinctErrorCount - plan.ErrorEntryCount, width, colorMode);
    }

    // Which of the section's optional pieces render, how tall the heatmap and chart bodies
    // are, and how many step rows and error entries: the height steps of the drop order
    // first, from the window's height alone (the heatmap only where the caller offers it —
    // never in a column), then the budget steps in the class summary's order, each taken only
    // while the section's rows — fixedRows for the header block and the requests panel, plus
    // the pieces still in — exceed the budget. Panel frames count, and so does the "+N more"
    // line a trimmed table gains: a table's rows are given back one at a time while that
    // still saves a row and a row remains, and a table that cannot keep a row goes whole.
    // Nothing given back is taken back: a later step that frees more than the section still
    // owed leaves the earlier ones as they are.
    private static SectionPlan PlanSection(int width, int height, int fixedRows, int budget, int stepCount, IReadOnlyList<IReadOnlyList<RenderedLine>> errorEntries, int distinctErrorCount, bool includeHeatmap)
    {
        var includeCharts = width >= MinimumWidthForCharts;
        var sideBySideCharts = width >= MinimumWidthForChartsSideBySide;
        var chartBodyHeight = HasRowsFor(height, MinimumHeightForTallCharts) ? ChartHeight : CompactChartHeight;
        var includeRequestsChart = includeCharts;
        var includeLatencyChart = includeCharts;
        var heatmapHeight = includeHeatmap && includeCharts && HasRowsFor(height, MinimumHeightForHeatmap) ? HeatmapHeight : 0;
        var stepRowCount = stepCount;
        var includeSteps = stepRowCount > 0;
        var errorEntryCount = errorEntries.Count;
        var includeErrors = errorEntryCount > 0;

        var rows = fixedRows + ChartRows(sideBySideCharts, chartBodyHeight, includeRequestsChart, includeLatencyChart) + HeatmapRows(heatmapHeight);
        if (includeSteps)
            rows += StepsPanelRows(stepRowCount, stepCount);
        if (includeErrors)
            rows += ErrorsPanelRows(errorEntries, errorEntryCount, distinctErrorCount);

        var over = rows - budget;

        // The heatmap body, a row at a time down to its floor — the one step that pays
        // exactly the rows owed — and then the panel whole.
        while (over > 0 && heatmapHeight > MinimumHeatmapHeight)
        {
            heatmapHeight--;
            over--;
        }

        if (over > 0 && heatmapHeight > 0)
        {
            over -= HeatmapRows(heatmapHeight);
            heatmapHeight = 0;
        }

        // The chart bodies, when the height left them tall: side by side the pair together.
        if (over > 0 && includeCharts && chartBodyHeight > CompactChartHeight)
        {
            var before = ChartRows(sideBySideCharts, chartBodyHeight, includeRequestsChart, includeLatencyChart);
            chartBodyHeight = CompactChartHeight;
            over -= before - ChartRows(sideBySideCharts, chartBodyHeight, includeRequestsChart, includeLatencyChart);
        }

        Func<int, int> stepsPanelRows = rowCount => StepsPanelRows(rowCount, stepCount);
        while (over > 0 && CanTrim(stepsPanelRows, stepRowCount))
        {
            var before = stepsPanelRows(stepRowCount);
            stepRowCount--;
            over -= before - stepsPanelRows(stepRowCount);
        }

        Func<int, int> errorsPanelRows = entryCount => ErrorsPanelRows(errorEntries, entryCount, distinctErrorCount);
        while (over > 0 && CanTrim(errorsPanelRows, errorEntryCount))
        {
            var before = errorsPanelRows(errorEntryCount);
            errorEntryCount--;
            over -= before - errorsPanelRows(errorEntryCount);
        }

        // The latency chart: stacked its rows go with it; side by side the pair splits and the
        // requests chart takes the width — no row freed, so the requests chart follows at once.
        if (over > 0 && includeLatencyChart)
        {
            var before = ChartRows(sideBySideCharts, chartBodyHeight, includeRequestsChart, includeLatencyChart);
            includeLatencyChart = false;
            over -= before - ChartRows(sideBySideCharts, chartBodyHeight, includeRequestsChart, includeLatencyChart);
        }

        if (over > 0 && includeRequestsChart)
        {
            includeRequestsChart = false;
            over -= chartBodyHeight + PanelFrameRows;
        }

        if (over > 0 && includeSteps)
        {
            includeSteps = false;
            over -= StepsPanelRows(stepRowCount, stepCount);
        }

        if (over > 0 && includeErrors)
        {
            includeErrors = false;
            over -= ErrorsPanelRows(errorEntries, errorEntryCount, distinctErrorCount);
        }

        return new SectionPlan(sideBySideCharts, chartBodyHeight, includeRequestsChart, includeLatencyChart, heatmapHeight, includeSteps, stepRowCount, includeErrors, errorEntryCount);
    }

    // The rows the chart panels take: side by side the two share one panel's rows; stacked, or
    // once the latency chart of a side-by-side pair is gone and the requests chart spans the
    // width, each panel still in has its own.
    private static int ChartRows(bool sideBySideCharts, int chartBodyHeight, bool includeRequestsChart, bool includeLatencyChart)
    {
        var panelRows = chartBodyHeight + PanelFrameRows;
        if (includeRequestsChart && includeLatencyChart)
            return sideBySideCharts ? panelRows : 2 * panelRows;

        if (includeRequestsChart || includeLatencyChart)
            return panelRows;

        return 0;
    }

    // The rows of the heatmap panel around a body of bodyHeight rows; none without a body.
    private static int HeatmapRows(int bodyHeight)
    {
        if (bodyHeight < 1)
            return 0;

        return bodyHeight + PanelFrameRows;
    }

    // Whether a table showing count of its entries can give one back to save a row — an entry
    // that saves a row itself, or one whose successor does: the first entry given back costs
    // the more line, so it pays only with the next. Never below one entry.
    private static bool CanTrim(Func<int, int> panelRows, int count)
    {
        if (count <= 1)
            return false;

        if (panelRows(count - 1) < panelRows(count))
            return true;

        return count > 2 && panelRows(count - 2) < panelRows(count);
    }

    // The rows of the Steps panel showing the first rowCount of stepCount steps: the frame,
    // the column header, the rows, and the more line once any step is hidden.
    private static int StepsPanelRows(int rowCount, int stepCount)
    {
        var rows = PanelFrameRows + 1 + rowCount;
        if (rowCount < stepCount)
            rows++;

        return rows;
    }

    // The rows of the Errors panel showing the first entryCount entries of distinctErrorCount
    // distinct errors: the frame, the entries' lines, and the more line once any error is
    // hidden — trimmed by the budget or never carried by the snapshot.
    private static int ErrorsPanelRows(IReadOnlyList<IReadOnlyList<RenderedLine>> entries, int entryCount, int distinctErrorCount)
    {
        var rows = PanelFrameRows;
        for (var index = 0; index < entryCount; index++)
            rows += entries[index].Count;

        if (entryCount < distinctErrorCount)
            rows++;

        return rows;
    }

    // The displayed row the view state's selection lands on: the index clamped into the rows
    // shown, so a selection past the last row (one the budget trimmed) stays on the last row
    // and a negative one lands on the first; none without a selection or without rows.
    private static int? SelectedStepRow(LiveDashboardViewState viewState, int rowCount)
    {
        if (viewState.SelectedStepIndex == null || rowCount < 1)
            return null;

        return Math.Clamp(viewState.SelectedStepIndex.Value, 0, rowCount - 1);
    }

    // Whether the window has the rows for an optional piece; an unbounded height (below 1)
    // has them all.
    private static bool HasRowsFor(int height, int minimumHeight)
    {
        return height < 1 || height >= minimumHeight;
    }

    // The scenario name and status badge, the paused badge after it while the viewer has
    // paused — the phase label after them when the section shows no timeline and the line
    // keeps its full width with it — with the compact logo right-aligned on the same row when
    // requested: the title is fitted to the columns left of the reserved logo area, so the two
    // can never overlap.
    private static RenderedLine RenderTitleLine(LiveMetricsSnapshot snapshot, bool isPaused, int width, ColorMode colorMode, string? spinnerGlyph, bool includeLogo, bool includePhaseLabel)
    {
        var titleWidth = includeLogo ? width - LogoWidget.CompactWidth - ColumnGap : width;
        var titleMarkup = "[bold]" + MarkupParser.Escape(snapshot.ScenarioName) + "[/]  " + StatusBadgeMarkup(snapshot, spinnerGlyph);
        if (isPaused)
            titleMarkup += " [" + TerminalPalette.SecondaryStyle + "]·[/] [" + TerminalPalette.PausedStyle + "]" + PausedBadgeText + "[/]";

        if (includePhaseLabel && snapshot.PhaseLabel.Length > 0)
        {
            var withPhase = titleMarkup + " [" + TerminalPalette.SecondaryStyle + "]·[/] [" + TerminalPalette.PhaseStyle + "]" + MarkupParser.Escape(snapshot.PhaseLabel) + "[/]";
            if (MarkupText.Measure(withPhase) <= titleWidth)
                titleMarkup = withPhase;
        }

        if (!includeLogo)
            return MarkupText.RenderTruncated(titleMarkup, width, colorMode);

        var pieces = new[]
        {
            new[] { MarkupText.RenderFitted(titleMarkup, titleWidth, colorMode) },
            LogoWidget.Render(LogoWidget.CompactWidth, LogoWidget.CompactHeight, colorMode)
        };
        return JoinSideBySide(pieces, new[] { titleWidth, LogoWidget.CompactWidth }, 0);
    }

    // The status badge; only the running badge animates — its dot gives way to the caller's
    // spinner glyph when one is given.
    private static string StatusBadgeMarkup(LiveMetricsSnapshot snapshot, string? spinnerGlyph)
    {
        if (snapshot.Status == TestStatus.Failed)
            return "[" + TerminalPalette.FailedStyle + "]● Failed[/]";

        if (snapshot.Status == TestStatus.Skipped)
            return "[" + TerminalPalette.SecondaryStyle + "]● Skipped[/]";

        if (snapshot.IsCompleted)
            return "[" + TerminalPalette.OkStyle + "]● Passed[/]";

        var runningGlyph = "●";
        if (spinnerGlyph != null)
            runningGlyph = MarkupParser.Escape(spinnerGlyph);

        return "[" + TerminalPalette.RunningStyle + "]" + runningGlyph + " Running[/]";
    }

    // The tile row: the five standard tiles and one per added threshold on one row while every
    // box can be MinimumTileBoxWidth wide, else on two rows with the first taking the larger
    // half; each tile is built for the inner width the widget will give its box.
    private static void AddTileRows(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, DeclaredThresholds thresholds, int width, int height, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var options = new TileOptions(
            includeTrends: width >= MinimumWidthForTileTrends && HasRowsFor(height, MinimumHeightForTileTrends),
            includeGauges: HasRowsFor(height, MinimumHeightForTileGauges));
        var count = StandardTileCount + thresholds.Added.Count;

        if (width >= MinimumWidthForSingleTileRow(count))
        {
            AddTileRow(lines, snapshot, thresholds, options, 0, count, width, colorMode, sparklineGlyphSet);
            return;
        }

        var firstRowCount = (count + 1) / 2;
        AddTileRow(lines, snapshot, thresholds, options, 0, firstRowCount, width, colorMode, sparklineGlyphSet);
        AddTileRow(lines, snapshot, thresholds, options, firstRowCount, count - firstRowCount, width, colorMode, sparklineGlyphSet);
    }

    // Each tile is built for the inner width the widget will give its box — the boxes' equal
    // share of the width less the gaps, the leftover columns widening the first boxes by one,
    // less the borders and padding — mirrored here so a tile's texts can be sized to the box
    // they land in. Too small to fit anything at degenerate widths, where the widget drops
    // tiles anyway.
    private static void AddTileRow(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, DeclaredThresholds thresholds, TileOptions options, int firstTile, int count, int width, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var boxWidths = ShareWidth(count, width, TileRowWidget.Gap);
        var tiles = new StatTile[count];
        for (var index = 0; index < count; index++)
            tiles[index] = BuildTile(snapshot, thresholds, options, firstTile + index, boxWidths[index] - PanelWidget.ContentOverhead);

        lines.AddRange(TileRowWidget.Render(tiles, width, colorMode, sparklineGlyphSet));
    }

    private static StatTile BuildTile(LiveMetricsSnapshot snapshot, DeclaredThresholds thresholds, TileOptions options, int index, int innerWidth)
    {
        switch (index)
        {
            case 0:
                return RateTile(snapshot, thresholds.RequestsPerSecond, options);
            case 1:
                return ResponseTimeTile(snapshot, thresholds.ResponseTimePercentile95, options);
            case 2:
                return ErrorsTile(snapshot, thresholds.ErrorRate, options);
            case 3:
                return RequestsTile(snapshot, innerWidth);
            case 4:
                return ElapsedTile(snapshot, options, innerWidth);
            default:
                return ThresholdTile(snapshot, thresholds.Added[index - StandardTileCount], options);
        }
    }

    // The newest interval's rate — no data before the first sample or when it is not finite,
    // else the shared rate format — with its delta and trend, in the declared rps threshold's
    // state and gauge, else Neutral.
    private static StatTile RateTile(LiveMetricsSnapshot snapshot, LiveThreshold? threshold, TileOptions options)
    {
        var series = snapshot.RequestsPerSecondSeries;
        var value = NoDataText;
        if (series.Count > 0 && double.IsFinite(series[series.Count - 1]))
            value = FormatFiniteRate(series[series.Count - 1]);

        return new StatTile(RateTileLabel, value)
        {
            State = ThresholdTileState(threshold),
            Delta = RateDelta(series),
            Gauge = ThresholdGauge(threshold, options),
            Trend = options.IncludeTrends ? Trailing(series, TrendSampleCount) : null
        };
    }

    // The signed change of the newest rate against the one DeltaSampleDistance samples earlier,
    // rounded to the rate format's finest step so the arrow and the text never disagree; none
    // while the series is too short, when either endpoint (or the change) is not finite, and
    // when the rounded change — the number the tile would show — is zero: a steady rate
    // carries no arrow, never a permanent ▲ 0.0.
    private static StatTileDelta? RateDelta(IReadOnlyList<double> series)
    {
        if (series.Count <= DeltaSampleDistance)
            return null;

        var newest = series[series.Count - 1];
        var earlier = series[series.Count - 1 - DeltaSampleDistance];
        if (!double.IsFinite(newest) || !double.IsFinite(earlier))
            return null;

        var change = Math.Round(newest - earlier, 1, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(change) || change == 0)
            return null;

        return new StatTileDelta(change, FormatFiniteRate(Math.Abs(change)), upIsGood: true);
    }

    // The newest interval's Ok p95 with its delta and trend, in the declared p95 threshold's
    // state and gauge, else the spike heuristic's state — computed only then: it sorts the
    // trailing window, and a declared threshold would discard it. An idle interval records a
    // p95 of zero: that is no data here, never "0 ms".
    private static StatTile ResponseTimeTile(LiveMetricsSnapshot snapshot, LiveThreshold? threshold, TileOptions options)
    {
        var series = snapshot.ResponseTimePercentile95Series;
        var newest = NewestResponseTimeSample(series);

        StatTileState state;
        if (threshold != null)
            state = ThresholdTileState(threshold);
        else
            state = ResponseTimeHeuristicState(series, newest);

        return new StatTile(ResponseTimeTileLabel, ResponseTimeValue(newest))
        {
            Unit = newest == null ? string.Empty : ResponseTimeUnit,
            State = state,
            Delta = ResponseTimeDelta(series),
            Gauge = ThresholdGauge(threshold, options),
            Trend = options.IncludeTrends ? ResponseTimeTrend(series) : null
        };
    }

    // A series value that is a response time: finite, within what a TimeSpan can hold, and
    // positive — an idle interval records zero, which is no latency at all.
    private static bool IsResponseTimeSample(double milliseconds)
    {
        return double.IsFinite(milliseconds) && milliseconds > 0 && milliseconds <= MaxResponseTimeMilliseconds;
    }

    private static double? NewestResponseTimeSample(IReadOnlyList<double> series)
    {
        if (series.Count == 0 || !IsResponseTimeSample(series[series.Count - 1]))
            return null;

        return series[series.Count - 1];
    }

    private static string ResponseTimeValue(double? milliseconds)
    {
        if (milliseconds == null)
            return NoDataText;

        return FormatResponseTimeValue(TimeSpan.FromMilliseconds(milliseconds.Value));
    }

    // The signed change of the newest p95 against the one DeltaSampleDistance samples earlier,
    // in whole milliseconds; none while the series is too short, when either endpoint is no
    // data, and when the rounded change — the number the tile would show — is zero: a steady
    // p95 carries no arrow, never a permanent ▲ 0 ms.
    private static StatTileDelta? ResponseTimeDelta(IReadOnlyList<double> series)
    {
        if (series.Count <= DeltaSampleDistance)
            return null;

        var newest = series[series.Count - 1];
        var earlier = series[series.Count - 1 - DeltaSampleDistance];
        if (!IsResponseTimeSample(newest) || !IsResponseTimeSample(earlier))
            return null;

        var change = Math.Round(newest - earlier, MidpointRounding.AwayFromZero);
        if (change == 0)
            return null;

        return new StatTileDelta(change, FormatCount((long)Math.Abs(change)) + " " + ResponseTimeUnit, upIsGood: false);
    }

    // The trailing samples with every non-latency as a gap, so the trend never dips to a fake zero.
    private static double[] ResponseTimeTrend(IReadOnlyList<double> series)
    {
        return ResponseTimeGaps(Trailing(series, TrendSampleCount));
    }

    // The series with every value that is not a latency (an idle interval's zero, a value no
    // TimeSpan holds) as a gap, as a fresh array.
    private static double[] ResponseTimeGaps(IReadOnlyList<double> series)
    {
        var values = new double[series.Count];
        for (var index = 0; index < values.Length; index++)
            values[index] = IsResponseTimeSample(series[index]) ? series[index] : double.NaN;

        return values;
    }

    // Warning when the newest p95 is above ResponseTimeSpikeFactor times the median of the
    // trailing window's latencies (idle intervals left out), once the window holds at least
    // ResponseTimeHeuristicMinimumSampleCount of them; Neutral otherwise, no data included.
    private static StatTileState ResponseTimeHeuristicState(IReadOnlyList<double> series, double? newest)
    {
        if (newest == null)
            return StatTileState.Neutral;

        var samples = new List<double>();
        for (var index = Math.Max(0, series.Count - TrendSampleCount); index < series.Count; index++)
        {
            if (IsResponseTimeSample(series[index]))
                samples.Add(series[index]);
        }

        if (samples.Count < ResponseTimeHeuristicMinimumSampleCount)
            return StatTileState.Neutral;

        samples.Sort();
        var middle = samples.Count / 2;
        var median = samples[middle];
        if (samples.Count % 2 == 0)
            median = (samples[middle - 1] + samples[middle]) / 2;

        if (newest.Value > ResponseTimeSpikeFactor * median)
            return StatTileState.Warning;

        return StatTileState.Neutral;
    }

    // The newest interval's failed share as a percentage — no data before the first sample and
    // for an interval without requests (IntervalRequestCount zero: a stalled second, the
    // drained final sample), which has no share to take, never a green 0.0 % — in the declared
    // error-rate threshold's state and gauge, both as the threshold reports them, else the
    // error-rate heuristic's state, computed only then.
    private static StatTile ErrorsTile(LiveMetricsSnapshot snapshot, LiveThreshold? threshold, TileOptions options)
    {
        var hasReading = snapshot.Samples.Count > 0 && snapshot.IntervalRequestCount > 0 && double.IsFinite(snapshot.ErrorRate);

        StatTileState state;
        if (threshold != null)
            state = ThresholdTileState(threshold);
        else
            state = ErrorRateHeuristicState(hasReading, snapshot.ErrorRate);

        return new StatTile(ErrorsTileLabel, hasReading ? FormatPercentNumber(snapshot.ErrorRate) : NoDataText)
        {
            Unit = hasReading ? PercentUnit : string.Empty,
            State = state,
            Gauge = ThresholdGauge(threshold, options)
        };
    }

    // Zero is Ok, up to ErrorRateWarningLimit Warning, above it Critical; no reading is Neutral.
    private static StatTileState ErrorRateHeuristicState(bool hasReading, double errorRate)
    {
        if (!hasReading)
            return StatTileState.Neutral;

        if (errorRate <= 0)
            return StatTileState.Ok;

        if (errorRate <= ErrorRateWarningLimit)
            return StatTileState.Warning;

        return StatTileState.Critical;
    }

    // The measurement phase's ok + failed count, the warmup count on the unit line once warmup
    // requests exist and the line fits the box.
    private static StatTile RequestsTile(LiveMetricsSnapshot snapshot, int innerWidth)
    {
        var value = FormatCount((long)snapshot.RequestCountOk + snapshot.RequestCountFailed);

        var unit = string.Empty;
        var warmupCount = (long)snapshot.WarmupRequestCountOk + snapshot.WarmupRequestCountFailed;
        if (warmupCount > 0)
            unit = FitUnit(value, WarmupUnitPrefix + FormatCount(warmupCount), innerWidth);

        return new StatTile(RequestsTileLabel, value) { Unit = unit };
    }

    // The run clock, the planned total on the unit line when the plan has one and the line
    // fits the box, and a gauge of the progress fraction — no warning band, since nothing is
    // judged — ending in the time remaining, whenever the plan gives both. An indeterminate
    // plan has neither, so it renders the clock alone with no fake progress.
    private static StatTile ElapsedTile(LiveMetricsSnapshot snapshot, TileOptions options, int innerWidth)
    {
        var value = FormatClock(snapshot.Duration);

        var unit = string.Empty;
        if (snapshot.PlannedDuration != null)
            unit = FitUnit(value, PlannedUnitPrefix + SimulationPlan.FormatDuration(snapshot.PlannedDuration.Value), innerWidth);

        StatTileGauge? gauge = null;
        if (options.IncludeGauges && snapshot.ProgressFraction != null && snapshot.EstimatedTimeRemaining != null)
            gauge = new StatTileGauge(snapshot.ProgressFraction.Value, 1, SimulationPlan.FormatDuration(snapshot.EstimatedTimeRemaining.Value)) { WarningFraction = 1 };

        return new StatTile(ElapsedTileLabel, value) { Unit = unit, Gauge = gauge };
    }

    // The tile a declared p99 or mean threshold adds: the newest interval's reading of its
    // metric — no data for an idle interval's zero, as on the p95 tile — in the threshold's
    // state, with its gauge.
    private static StatTile ThresholdTile(LiveMetricsSnapshot snapshot, LiveThreshold threshold, TileOptions options)
    {
        var metric = threshold.Threshold.Metric;
        var reading = IntervalReadingOf(snapshot.IntervalLatency, metric);
        var hasReading = reading > TimeSpan.Zero;

        return new StatTile(ThresholdFormat.Label(metric), hasReading ? FormatResponseTimeValue(reading) : NoDataText)
        {
            Unit = hasReading ? ResponseTimeUnit : string.Empty,
            State = ThresholdTileState(threshold),
            Gauge = ThresholdGauge(threshold, options)
        };
    }

    private static TimeSpan IntervalReadingOf(IntervalLatency intervalLatency, ThresholdMetric metric)
    {
        if (intervalLatency == null)
            return TimeSpan.Zero;

        switch (metric)
        {
            case ThresholdMetric.ResponseTimeMean:
                return intervalLatency.ResponseTimeMean;
            case ThresholdMetric.ResponseTimePercentile95:
                return intervalLatency.ResponseTimePercentile95;
            case ThresholdMetric.ResponseTimePercentile99:
                return intervalLatency.ResponseTimePercentile99;
            default:
                throw new ArgumentOutOfRangeException(nameof(metric), metric, "The metric has no interval response time.");
        }
    }

    // A unit only when the value line — the value, a space and the unit — fits the box's inner
    // width: the widget never leaves a unit out on its own, it leaves the whole tile out. The
    // texts are the layout's own ASCII, one column per character.
    private static string FitUnit(string value, string unit, int innerWidth)
    {
        if (value.Length + 1 + unit.Length > innerWidth)
            return string.Empty;

        return unit;
    }

    // The live state of a declared threshold as the tile shows it; Neutral without one.
    private static StatTileState ThresholdTileState(LiveThreshold? threshold)
    {
        if (threshold == null)
            return StatTileState.Neutral;

        switch (threshold.State)
        {
            case ThresholdState.Ok:
                return StatTileState.Ok;
            case ThresholdState.Warning:
                return StatTileState.Warning;
            case ThresholdState.Breached:
                return StatTileState.Critical;
            default:
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold.State, "Unknown threshold state.");
        }
    }

    // The declared threshold's gauge: the live reading against the limit in the metric's unit,
    // the comparison passed straight through so a minimum's band lands on the evaluator's zone.
    private static StatTileGauge? ThresholdGauge(LiveThreshold? threshold, TileOptions options)
    {
        if (threshold == null || !options.IncludeGauges)
            return null;

        var declared = threshold.Threshold;
        return new StatTileGauge(threshold.Current, declared.Limit, ThresholdLimitText(declared)) { Comparison = declared.Comparison };
    }

    // The limit as the thresholds format it, the bare request rate given its unit.
    private static string ThresholdLimitText(Threshold threshold)
    {
        var text = ThresholdFormat.FormatValue(threshold.Metric, threshold.Limit);
        if (threshold.Metric == ThresholdMetric.RequestsPerSecond)
            text += " " + RateTileLabel;

        return text;
    }

    // The newest count samples of a series, oldest first, as a fresh array.
    private static double[] Trailing(IReadOnlyList<double> series, int count)
    {
        var start = Math.Max(0, series.Count - count);
        var trailing = new double[series.Count - start];
        for (var index = 0; index < trailing.Length; index++)
            trailing[index] = series[start + index];

        return trailing;
    }

    // The plan's timeline: the entries as segments one to one, the progress fraction as the
    // position. When the plan gives the marker no position the phase label follows the bar,
    // ColumnGap columns after it, as long as the bar keeps MinimumTimelineWidth columns; the
    // legend line, when the widget adds one, stays under the bar.
    private static void AddTimeline(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, int width, ColorMode colorMode)
    {
        var entries = snapshot.PlanEntries;
        var segments = new TimelineSegment[entries.Count];
        for (var index = 0; index < entries.Count; index++)
            segments[index] = new TimelineSegment(entries[index].Label, entries[index].Duration, entries[index].IsWarmup);

        var barWidth = width;
        var phaseLabel = BlankLine;
        var hasPhaseLabel = false;
        if (snapshot.ProgressFraction == null && snapshot.PhaseLabel.Length > 0)
        {
            var phaseMarkup = "[" + TerminalPalette.PhaseStyle + "]" + MarkupParser.Escape(snapshot.PhaseLabel) + "[/]";
            var phaseWidth = MarkupText.Measure(phaseMarkup);
            if (width - ColumnGap - phaseWidth >= MinimumTimelineWidth)
            {
                barWidth = width - ColumnGap - phaseWidth;
                phaseLabel = MarkupText.RenderTruncated(phaseMarkup, phaseWidth, colorMode);
                hasPhaseLabel = true;
            }
        }

        var timeline = TimelineWidget.Render(segments, snapshot.ProgressFraction, barWidth, colorMode);
        for (var row = 0; row < timeline.Count; row++)
        {
            if (row == 0 && hasPhaseLabel)
                lines.Add(new RenderedLine(timeline[row].Text + new string(' ', ColumnGap) + phaseLabel.Text, barWidth + ColumnGap + phaseLabel.Width));
            else
                lines.Add(timeline[row]);
        }
    }

    // The chart panels the plan kept: side by side, the requests chart on the left and the
    // latency chart on the right with the panel widths summing to the full width less the gap
    // — the column over, at an odd width, going to the latency chart, unlike the tile boxes
    // and the scenario columns, which widen the first — so the joined rows are exactly the
    // frame width; stacked, each at the full width, requests first.
    private static void AddChartPanels(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, DeclaredThresholds thresholds, SectionPlan plan, int? timeWindow, int width, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        if (plan.SideBySideCharts)
        {
            var leftWidth = (width - ColumnGap) / 2;
            var rightWidth = width - ColumnGap - leftWidth;
            var panels = new[]
            {
                RenderRequestsChartPanel(snapshot, timeWindow, leftWidth, plan.ChartBodyHeight, colorMode, sparklineGlyphSet),
                RenderLatencyChartPanel(snapshot, thresholds, timeWindow, rightWidth, plan.ChartBodyHeight, colorMode, sparklineGlyphSet)
            };
            var panelWidths = new[] { leftWidth, rightWidth };

            for (var row = 0; row < panels[0].Count; row++)
                lines.Add(JoinSideBySide(panels, panelWidths, row));

            return;
        }

        if (plan.IncludeRequestsChart)
            lines.AddRange(RenderRequestsChartPanel(snapshot, timeWindow, width, plan.ChartBodyHeight, colorMode, sparklineGlyphSet));

        if (plan.IncludeLatencyChart)
            lines.AddRange(RenderLatencyChartPanel(snapshot, thresholds, timeWindow, width, plan.ChartBodyHeight, colorMode, sparklineGlyphSet));
    }

    // A chart panel's options over the view state's time window: the shared defaults as they
    // are without a window, else a copy of them carrying the window.
    private static ChartOptions ChartOptionsFor(ChartOptions defaults, int? timeWindow)
    {
        if (timeWindow == null)
            return defaults;

        return new ChartOptions { ValueFormatter = defaults.ValueFormatter, AxisStyle = defaults.AxisStyle, TimeWindow = timeWindow };
    }

    // The heatmap panel's options over the view state's time window, the same way.
    private static HeatmapOptions HeatmapOptionsFor(int? timeWindow)
    {
        if (timeWindow == null)
            return LatencyHeatmapOptions;

        return new HeatmapOptions { LabelStyle = LatencyHeatmapOptions.LabelStyle, TimeWindow = timeWindow };
    }

    // The per-interval ok and failed counts as two areas on one count scale, failed painted
    // over ok so a failed count shows at the bottom in its own colour; the annotation is the
    // first series' — the newest ok count.
    private static IReadOnlyList<RenderedLine> RenderRequestsChartPanel(LiveMetricsSnapshot snapshot, int? timeWindow, int panelWidth, int bodyHeight, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var series = new[]
        {
            new ChartSeries(snapshot.OkDeltaSeries) { Style = TerminalPalette.OkStyle },
            new ChartSeries(snapshot.FailedDeltaSeries) { Style = TerminalPalette.FailedStyle }
        };

        var chart = ChartWidget.Render(series, panelWidth - PanelWidget.ContentOverhead, bodyHeight, colorMode, sparklineGlyphSet, ChartOptionsFor(RequestsChartOptions, timeWindow));
        return PanelWidget.Render(RequestsChartHeader, chart, panelWidth, colorMode);
    }

    // The per-interval p99 and p95 as two areas, tallest first so each band shows where it
    // owns the cell, and the median as a line painted last, so it stays visible over both —
    // a cell the line passes shows only the line's dots — every non-latency reading a gap; a
    // declared p95 or p99 limit as a flat line over the p95 series' samples, so it spans the
    // same columns and shares the scale, in the style the title's legend gives it.
    private static IReadOnlyList<RenderedLine> RenderLatencyChartPanel(LiveMetricsSnapshot snapshot, DeclaredThresholds thresholds, int? timeWindow, int panelWidth, int bodyHeight, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var series = new List<ChartSeries>
        {
            new ChartSeries(ResponseTimeGaps(snapshot.ResponseTimePercentile99Series)) { Style = TerminalPalette.ResponseTimePercentile99Style },
            new ChartSeries(ResponseTimeGaps(snapshot.ResponseTimePercentile95Series)) { Style = TerminalPalette.ResponseTimePercentile95Style },
            new ChartSeries(ResponseTimeGaps(snapshot.ResponseTimeMedianSeries)) { Style = TerminalPalette.ResponseTimeMedianStyle, Kind = ChartSeriesKind.Line }
        };

        var sampleCount = snapshot.ResponseTimePercentile95Series.Count;
        if (thresholds.ResponseTimePercentile95 != null)
            series.Add(LimitLine(thresholds.ResponseTimePercentile95, sampleCount, TerminalPalette.WarningStyle));

        if (thresholds.ResponseTimePercentile99 != null)
            series.Add(LimitLine(thresholds.ResponseTimePercentile99, sampleCount, TerminalPalette.FailedStyle));

        var chart = ChartWidget.Render(series, panelWidth - PanelWidget.ContentOverhead, bodyHeight, colorMode, sparklineGlyphSet, ChartOptionsFor(LatencyChartOptions, timeWindow));
        return PanelWidget.Render(LatencyChartHeader(thresholds), chart, panelWidth, colorMode);
    }

    private static ChartSeries LimitLine(LiveThreshold threshold, int sampleCount, string style)
    {
        var values = new double[sampleCount];
        Array.Fill(values, threshold.Threshold.Limit);
        return new ChartSeries(values) { Style = style, Kind = ChartSeriesKind.Line };
    }

    // The latency chart's header: the bands' legend, then the declared p95 and p99 limits as
    // the thresholds format them, each in its line's style.
    private static string LatencyChartHeader(DeclaredThresholds thresholds)
    {
        var percentile95 = thresholds.ResponseTimePercentile95;
        var percentile99 = thresholds.ResponseTimePercentile99;
        if (percentile95 == null && percentile99 == null)
            return LatencyChartHeaderPrefix;

        var header = LatencyChartHeaderPrefix + " [" + TerminalPalette.SecondaryStyle + "]· " + (percentile95 != null && percentile99 != null ? "limits" : "limit") + "[/]";
        if (percentile95 != null)
            header += " [" + TerminalPalette.WarningStyle + "]" + ThresholdFormat.FormatValue(percentile95.Threshold.Metric, percentile95.Threshold.Limit) + "[/]";

        if (percentile95 != null && percentile99 != null)
            header += " [" + TerminalPalette.SecondaryStyle + "]/[/]";

        if (percentile99 != null)
            header += " [" + TerminalPalette.FailedStyle + "]" + ThresholdFormat.FormatValue(percentile99.Threshold.Metric, percentile99.Threshold.Limit) + "[/]";

        return header;
    }

    // The latency bucket counts per sample as a heatmap over the charts' time window, the
    // fifteen buckets labelled by their bounds and merged into the plan's bodyHeight rows by
    // the widget, in a full-width panel.
    private static IReadOnlyList<RenderedLine> RenderHeatmapPanel(LiveMetricsSnapshot snapshot, int? timeWindow, int width, int bodyHeight, ColorMode colorMode)
    {
        var heatmap = HeatmapWidget.Render(snapshot.LatencyBucketSeries, LatencyBucketLabels, width - PanelWidget.ContentOverhead, bodyHeight, colorMode, HeatmapOptionsFor(timeWindow));
        return PanelWidget.Render(HeatmapHeader, heatmap, width, colorMode);
    }

    private static string[] BuildLatencyBucketLabels()
    {
        var bounds = LatencyBuckets.UpperBounds;
        var labels = new string[bounds.Count + 1];
        for (var index = 0; index < bounds.Count; index++)
            labels[index] = "≤ " + FormatBucketBound(bounds[index]);

        labels[bounds.Count] = "> " + FormatBucketBound(bounds[bounds.Count - 1]);
        return labels;
    }

    // A bucket bound in whole units where the bounds are whole: milliseconds below a second,
    // seconds from one up.
    private static string FormatBucketBound(TimeSpan bound)
    {
        if (bound < TimeSpan.FromSeconds(1))
            return bound.TotalMilliseconds.ToString("0.#", CultureInfo.InvariantCulture) + " " + ResponseTimeUnit;

        return bound.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s";
    }

    // The measurement totals as a table — Ok and Failed rows with the count, the current rate
    // and the response-time spread — prefixed by a warmup headline once any warmup requests
    // exist. The column set is the widest tier whose natural width fits the panel; only when
    // even the narrowest tier cannot fit does the table widget shrink columns.
    private static IReadOnlyList<RenderedLine> RenderRequestsPanel(LiveMetricsSnapshot snapshot, int width, ColorMode colorMode)
    {
        var innerWidth = width - PanelWidget.ContentOverhead;
        var content = new List<RenderedLine>();

        if ((long)snapshot.WarmupRequestCountOk + snapshot.WarmupRequestCountFailed > 0)
        {
            var warmupMarkup = "[" + TerminalPalette.SecondaryStyle + "]warmup[/] " + FormatCount(snapshot.WarmupRequestCountOk)
                + " [" + TerminalPalette.OkStyle + "]ok[/] [" + TerminalPalette.SecondaryStyle + "]·[/] " + FormatCount(snapshot.WarmupRequestCountFailed)
                + " [" + TerminalPalette.FailedStyle + "]failed[/]";
            content.Add(MarkupText.RenderTruncated(warmupMarkup, innerWidth, colorMode));
        }

        var rates = CurrentRateLabels(snapshot.Samples);
        var rows = new[]
        {
            RequestsRow("ok", TerminalPalette.OkStyle, snapshot.Ok, rates.Ok),
            RequestsRow("failed", TerminalPalette.FailedStyle, snapshot.Failed, rates.Failed)
        };

        for (var tier = 0; tier < RequestsColumnTiers.Length; tier++)
        {
            var columns = SelectColumns(RequestsColumns, RequestsColumnTiers[tier]);
            var tierRows = SelectCells(rows, RequestsColumnTiers[tier]);
            var isNarrowestTier = tier == RequestsColumnTiers.Length - 1;
            if (isNarrowestTier || TableWidget.MeasureNaturalWidth(columns, tierRows) <= innerWidth)
            {
                content.AddRange(TableWidget.Render(columns, tierRows, innerWidth, colorMode));
                break;
            }
        }

        return PanelWidget.Render("[" + TerminalPalette.PanelHeaderStyle + "]Requests[/]", content, width, colorMode);
    }

    private static TableColumn[] SelectColumns(TableColumn[] columns, int[] columnIndexes)
    {
        var selected = new TableColumn[columnIndexes.Length];
        for (var index = 0; index < columnIndexes.Length; index++)
            selected[index] = columns[columnIndexes[index]];

        return selected;
    }

    private static IReadOnlyList<string?>[] SelectCells(string?[][] rows, int[] columnIndexes)
    {
        var selected = new IReadOnlyList<string?>[rows.Length];
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var cells = new string?[columnIndexes.Length];
            for (var index = 0; index < columnIndexes.Length; index++)
                cells[index] = rows[rowIndex][columnIndexes[index]];

            selected[rowIndex] = cells;
        }

        return selected;
    }

    // The current-interval ok and failed rates from the newest sample: its combined rate (the
    // rps tile's value) split in proportion to its deltas. The sample's rate is
    // (ok + failed) / interval, so ok / interval is ok × rate / (ok + failed) — exact without
    // the interval length, which the sample does not carry, and the two always sum to the
    // tile's rate. No sample yet renders no data.
    private static (string Ok, string Failed) CurrentRateLabels(IReadOnlyList<LiveMetricsSample> samples)
    {
        if (samples.Count == 0)
            return (NoDataMarkup, NoDataMarkup);

        var sample = samples[samples.Count - 1];
        var total = (double)sample.OkDelta + sample.FailedDelta;
        if (total == 0)
            return (RateMarkup(0), RateMarkup(0));

        return (RateMarkup(sample.RequestsPerSecond * sample.OkDelta / total), RateMarkup(sample.RequestsPerSecond * sample.FailedDelta / total));
    }

    private static string?[] RequestsRow(string label, string style, LiveStats stats, string rateMarkup)
    {
        return new[]
        {
            "[" + style + "]" + label + "[/]",
            FormatCount(stats.RequestCount),
            rateMarkup,
            stats.ResponseTimeMin.ToTestFuznResponseTime(),
            stats.ResponseTimeMean.ToTestFuznResponseTime(),
            stats.ResponseTimeMedian.ToTestFuznResponseTime(),
            stats.ResponseTimePercentile75.ToTestFuznResponseTime(),
            stats.ResponseTimePercentile95.ToTestFuznResponseTime(),
            stats.ResponseTimePercentile99.ToTestFuznResponseTime(),
            stats.ResponseTimeMax.ToTestFuznResponseTime()
        };
    }

    // The steps by pain: the failure share first, then the newest interval p95, both
    // descending — a step without a p95 reading after every step with one — and declaration
    // order on a tie (the sort is stable), so the rows a shrinking budget keeps are the ones
    // that hurt.
    private static List<LiveStepMetrics> SortStepsByPain(IReadOnlyList<LiveStepMetrics> steps)
    {
        return steps.OrderByDescending(FailureFraction).ThenByDescending(PainResponseTime).ToList();
    }

    // The step's failure share, 0..1: failed over ok + failed, clamped so counts that do not
    // add up (a negative or wrapped counter) read as no failures or all failures; zero
    // without a request.
    private static double FailureFraction(LiveStepMetrics step)
    {
        var total = (long)step.RequestCountOk + step.RequestCountFailed;
        if (total == 0)
            return 0;

        var fraction = (double)step.RequestCountFailed / total;
        if (fraction < 0)
            return 0;
        if (fraction > 1)
            return 1;

        return fraction;
    }

    // The step's newest interval p95 as a sort key: the reading, or below every reading
    // without one.
    private static double PainResponseTime(LiveStepMetrics step)
    {
        var newest = NewestResponseTimeSample(step.ResponseTimePercentile95Series);
        if (newest == null)
            return double.NegativeInfinity;

        return newest.Value;
    }

    // The first rowCount of the pain-sorted steps as a table under the column header — every
    // step when the budget allows, fewer with the more line when the drop order took rows —
    // in the widest column tier whose natural width fits the panel. The selected row is
    // painted whole in reverse video: the table is rendered once more without colour for the
    // row's plain text, which is then re-rendered as one reverse span padded to the inner
    // width, so the bar is uniform where the cells' own styles would break it.
    private static void AddStepsPanel(List<RenderedLine> lines, IReadOnlyList<LiveStepMetrics> steps, int rowCount, int? selectedRow, int width, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var innerWidth = width - PanelWidget.ContentOverhead;
        var rows = new string?[rowCount][];
        for (var index = 0; index < rowCount; index++)
            rows[index] = StepRow(steps[index], isSelected: selectedRow != null && selectedRow.Value == index, sparklineGlyphSet);

        var content = new List<RenderedLine>();
        for (var tier = 0; tier < StepColumnTiers.Length; tier++)
        {
            var columns = SelectColumns(StepColumns, StepColumnTiers[tier]);
            var tierRows = SelectCells(rows, StepColumnTiers[tier]);
            var isNarrowestTier = tier == StepColumnTiers.Length - 1;
            if (!isNarrowestTier && TableWidget.MeasureNaturalWidth(columns, tierRows) > innerWidth)
                continue;

            content.AddRange(TableWidget.Render(columns, tierRows, innerWidth, colorMode));
            if (selectedRow != null && selectedRow.Value + 1 < content.Count)
            {
                var plain = TableWidget.Render(columns, tierRows, innerWidth, ColorMode.None);
                content[selectedRow.Value + 1] = ReverseVideoRow(plain[selectedRow.Value + 1].Text, innerWidth, colorMode);
            }

            break;
        }

        if (rowCount < steps.Count)
            content.Add(MarkupText.RenderTruncated(PointerColumnBlank + MoreLineMarkup(steps.Count - rowCount), innerWidth, colorMode));

        lines.AddRange(PanelWidget.Render(StepsHeader, content, width, colorMode));
    }

    // One step's cells in StepColumns order: the pointer column and the escaped name, the
    // counts, the current rate, the cumulative mean, the newest interval p95, the fail% bar
    // and the trend.
    private static string?[] StepRow(LiveStepMetrics step, bool isSelected, SparklineGlyphSet sparklineGlyphSet)
    {
        return new[]
        {
            (isSelected ? Pointer : PointerColumnBlank) + MarkupParser.Escape(step.Name),
            FormatCount((long)step.RequestCountOk + step.RequestCountFailed),
            RateMarkup(step.RequestsPerSecond),
            step.ResponseTimeMean.ToTestFuznResponseTime(),
            StepResponseTimeMarkup(step.ResponseTimePercentile95Series),
            FormatCount(step.RequestCountFailed),
            FailureBarMarkup(step.RequestCountOk, step.RequestCountFailed),
            StepTrendMarkup(step.RequestsPerSecondSeries, sparklineGlyphSet)
        };
    }

    // The newest interval p95 through the shared response-time formatter, no data under the
    // p95 tile's rule.
    private static string StepResponseTimeMarkup(IReadOnlyList<double> series)
    {
        var newest = NewestResponseTimeSample(series);
        if (newest == null)
            return NoDataMarkup;

        return TimeSpan.FromMilliseconds(newest.Value).ToTestFuznResponseTime();
    }

    // The step's rate sparkline as a table cell: the widget's glyphs — plain text without a
    // style, a bracket-free run of braille or block glyphs and spaces — in the secondary
    // style, the way a Neutral tile's trend renders.
    private static string StepTrendMarkup(IReadOnlyList<double> series, SparklineGlyphSet sparklineGlyphSet)
    {
        var glyphs = SparklineWidget.Render(series, StepTrendWidth, ColorMode.None, sparklineGlyphSet)[0].Text;
        return "[" + TerminalPalette.SecondaryStyle + "]" + glyphs + "[/]";
    }

    // A table row's plain text as one reverse-video span padded to the inner width: the
    // escaped text carries no markup of its own, so the whole bar is one style.
    private static RenderedLine ReverseVideoRow(string plainText, int innerWidth, ColorMode colorMode)
    {
        return MarkupText.RenderTruncated("[reverse]" + MarkupParser.Escape(plainText.PadRight(innerWidth)) + "[/]", innerWidth, colorMode);
    }

    // The line that announces the rows a panel does not show.
    private static string MoreLineMarkup(int hiddenCount)
    {
        return "[" + TerminalPalette.SecondaryStyle + "]+" + FormatCount(hiddenCount) + " more[/]";
    }

    // The first entryCount ticker entries, each on its laid-out lines, and the more line for
    // the hiddenCount errors not shown.
    private static void AddErrorsPanel(List<RenderedLine> lines, IReadOnlyList<IReadOnlyList<RenderedLine>> entries, int entryCount, int hiddenCount, int width, ColorMode colorMode)
    {
        var content = new List<RenderedLine>();
        for (var index = 0; index < entryCount; index++)
            content.AddRange(entries[index]);

        if (hiddenCount > 0)
            content.Add(MarkupText.RenderTruncated(MoreLineMarkup(hiddenCount), width - PanelWidget.ContentOverhead, colorMode));

        lines.AddRange(PanelWidget.Render(ErrorsHeader, content, width, colorMode));
    }

    // Every ticker entry laid out for the panel, most recently active first: the count and
    // the rate right-aligned across all the entries (so the layout does not depend on which
    // the budget keeps), the step name, the message and the ages as styled runs of sanitized
    // text, wrapped to the panel's inner width — the pieces the width drops left out.
    private static List<IReadOnlyList<RenderedLine>> RenderErrorEntries(LiveMetricsSnapshot snapshot, int width, ColorMode colorMode)
    {
        var entries = new List<IReadOnlyList<RenderedLine>>();
        if (snapshot.Errors.Count == 0)
            return entries;

        var errors = snapshot.Errors.OrderByDescending(error => error.LastSeen).ThenByDescending(error => error.Count).ToList();
        var includeRates = width >= MinimumWidthForErrorRates;
        var includeAges = width >= MinimumWidthForErrorAges && snapshot.Samples.Count > 0;

        // The instant the ages are measured from: the newest sample's timestamp, which the
        // ages are shown only when the snapshot has.
        var reference = DateTime.MinValue;
        if (includeAges)
            reference = snapshot.Samples[snapshot.Samples.Count - 1].Timestamp;

        var countWidth = 0;
        var rateWidth = 0;
        foreach (var error in errors)
        {
            countWidth = Math.Max(countWidth, FormatCount(error.Count).Length);
            rateWidth = Math.Max(rateWidth, ErrorRateText(error.RatePerSecond).Length);
        }

        // Continuation lines sit under the step name: past the count and its ×, then either
        // the rate between a space and two spaces, or one space.
        var innerWidth = width - PanelWidget.ContentOverhead;
        var nameSeparator = includeRates ? "  " : " ";
        var indent = countWidth + 1 + nameSeparator.Length;
        if (includeRates)
            indent += 1 + rateWidth;
        if (indent * 2 > innerWidth)
            indent = 0;

        foreach (var error in errors)
        {
            var runs = new List<TextRun>();
            runs.Add(new TextRun(FormatCount(error.Count).PadLeft(countWidth) + "×", TerminalPalette.FailedStyle));
            if (includeRates)
            {
                runs.Add(new TextRun(" ", null));
                runs.Add(new TextRun(ErrorRateText(error.RatePerSecond).PadLeft(rateWidth), TerminalPalette.SecondaryStyle));
            }

            runs.Add(new TextRun(nameSeparator, null));
            runs.Add(new TextRun(MarkupText.SanitizeControlCharacters(error.StepName), "bold"));
            runs.Add(new TextRun(" ", null));
            runs.Add(new TextRun("·", TerminalPalette.SecondaryStyle));
            runs.Add(new TextRun(" ", null));
            runs.Add(new TextRun(MarkupText.SanitizeControlCharacters(error.Message), null));
            if (includeAges)
            {
                runs.Add(new TextRun("   ", null));
                runs.Add(new TextRun("first " + FormatAge(reference - error.FirstSeen) + " ago · last " + FormatAge(reference - error.LastSeen) + " ago", TerminalPalette.SecondaryStyle));
            }

            entries.Add(WrapRuns(runs, innerWidth, indent, colorMode));
        }

        return entries;
    }

    // An error's current rate for the ticker: the rate format per second in parentheses, no
    // data when it is not finite.
    private static string ErrorRateText(double rate)
    {
        var number = NoDataText;
        if (double.IsFinite(rate))
            number = FormatFiniteRate(rate);

        return "(" + number + "/s)";
    }

    // How long ago, in the plan's duration format; a negative age (an entry stamped after the
    // reference) is floored at zero by the format.
    private static string FormatAge(TimeSpan age)
    {
        return SimulationPlan.FormatDuration(age);
    }

    // The runs' text wrapped to the width as lines of at most MaximumErrorEntryLines: the first
    // line at the full width, the rest indented and narrowed by the indent; each line's markup
    // is rebuilt from the runs it covers, so no line break can split a tag or an escape. The
    // last allowed line takes all the remaining text and is truncated with an ellipsis when
    // that is too wide. A degenerate panel (an inner width below 1) lays the text out as if
    // one column wide, so the loop always advances, and renders every line empty.
    private static IReadOnlyList<RenderedLine> WrapRuns(List<TextRun> runs, int innerWidth, int indent, ColorMode colorMode)
    {
        var text = new StringBuilder();
        foreach (var run in runs)
            text.Append(run.Text);

        var continuationWidth = innerWidth - indent;
        var ranges = WrapRanges(text.ToString(), Math.Max(1, innerWidth), Math.Max(1, continuationWidth), MaximumErrorEntryLines);

        var lines = new List<RenderedLine>(ranges.Count);
        for (var index = 0; index < ranges.Count; index++)
        {
            var markup = RunsMarkup(runs, ranges[index].Start, ranges[index].Length);
            if (index == 0)
            {
                lines.Add(MarkupText.RenderTruncated(markup, innerWidth, colorMode));
                continue;
            }

            var rendered = MarkupText.RenderTruncated(markup, continuationWidth, colorMode);
            if (rendered.Width == 0)
                lines.Add(rendered);
            else
                lines.Add(new RenderedLine(new string(' ', indent) + rendered.Text, indent + rendered.Width));
        }

        return lines;
    }

    // Greedy word wrapping over plain text: a line breaks at the last space that lets it fit
    // its width (the space is consumed), or at the width when it holds none — never between
    // a high surrogate and its low half. The last allowed line takes the rest.
    private static List<(int Start, int Length)> WrapRanges(string text, int firstWidth, int continuationWidth, int maximumLines)
    {
        var ranges = new List<(int Start, int Length)>();
        var position = 0;
        while (position < text.Length)
        {
            var width = ranges.Count == 0 ? firstWidth : continuationWidth;
            var remaining = text.Length - position;
            if (ranges.Count == maximumLines - 1 || remaining <= width)
            {
                ranges.Add((position, remaining));
                break;
            }

            var end = position + width;
            var space = text.LastIndexOf(' ', end, width);
            if (space >= 0)
            {
                ranges.Add((position, space - position));
                position = space + 1;
                continue;
            }

            if (end - 1 > position && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end]))
                end--;

            ranges.Add((position, end - position));
            position = end;
        }

        if (ranges.Count == 0)
            ranges.Add((0, 0));

        return ranges;
    }

    // The markup of the runs' text from start for length characters: each run's part escaped
    // and wrapped in the run's style, so the styling is complete on every line.
    private static string RunsMarkup(List<TextRun> runs, int start, int length)
    {
        var markup = new StringBuilder();
        var end = start + length;
        var runStart = 0;
        foreach (var run in runs)
        {
            var runEnd = runStart + run.Text.Length;
            var partStart = Math.Max(start, runStart);
            var partEnd = Math.Min(end, runEnd);
            if (partStart < partEnd)
            {
                var part = MarkupParser.Escape(run.Text.Substring(partStart - runStart, partEnd - partStart));
                if (run.Style == null)
                    markup.Append(part);
                else
                    markup.Append('[').Append(run.Style).Append(']').Append(part).Append("[/]");
            }

            runStart = runEnd;
            if (runStart >= end)
                break;
        }

        return markup.ToString();
    }

    // A fixed-width bar for a step's failure share in the share's state — the errors tile's
    // heuristic: zero Ok, up to ErrorRateWarningLimit Warning, above it Critical — the filled
    // cells and the percentage in the state's style over a dim track; no requests renders the
    // empty track alone, Neutral. A nonzero share fills at least one cell, and the share is
    // clamped to 0..1, so counts that do not add up (a negative or wrapped counter) can only
    // render an empty or a full bar, never one of impossible length.
    private static string FailureBarMarkup(int okCount, int failedCount)
    {
        var emptyTrack = "[" + TerminalPalette.SecondaryStyle + "]" + new string('░', FailureBarCellCount) + "[/]";
        var total = (long)okCount + failedCount;
        if (total == 0)
            return emptyTrack;

        var fraction = (double)failedCount / total;
        if (fraction < 0)
            fraction = 0;
        else if (fraction > 1)
            fraction = 1;

        var style = TerminalPalette.StateStyle(ErrorRateHeuristicState(hasReading: true, fraction));
        if (fraction == 0)
            return emptyTrack + " " + Styled(style, "0" + PercentUnit);

        var filledCount = (int)Math.Round(fraction * FailureBarCellCount, MidpointRounding.AwayFromZero);
        if (filledCount < 1)
            filledCount = 1;

        return Styled(style, new string('█', filledCount)) + "[" + TerminalPalette.SecondaryStyle + "]"
            + new string('░', FailureBarCellCount - filledCount) + "[/] " + Styled(style, FormatPercent(fraction));
    }

    // Text in a style, or as it is without one.
    private static string Styled(string? style, string text)
    {
        if (style == null)
            return text;

        return "[" + style + "]" + text + "[/]";
    }

    // The number formatters are shared with the plain stats lines (LiveStatsWriter), so a value
    // reads the same on the dashboard and in a redirected log.

    /// <summary>
    /// A clock value as hh:mm:ss with unbounded hours, so runs past 24 hours keep counting
    /// instead of wrapping; a negative duration displays as zero instead of as negative fields.
    /// </summary>
    internal static string FormatClock(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        return ((int)duration.TotalHours).ToString("00", CultureInfo.InvariantCulture)
            + ":" + duration.Minutes.ToString("00", CultureInfo.InvariantCulture)
            + ":" + duration.Seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A finite rate with one decimal below 10, so slow-step rates stay readable, and as a whole
    /// number from 10 up. The precondition is in the name: there is no guard here (a rate that is
    /// not finite would format as NaN or Infinity), and whether a rate is data at all is the
    /// caller's call — the dashboard (<see cref="RateMarkup"/>) and the stats writer both show a
    /// rate that is not finite as no data, never as a number.
    /// </summary>
    internal static string FormatFiniteRate(double rate)
    {
        if (rate < 10)
            return rate.ToString("0.0", CultureInfo.InvariantCulture);

        return rate.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>A request count, plain and culture-invariant.</summary>
    internal static string FormatCount(long count)
    {
        return count.ToString(CultureInfo.InvariantCulture);
    }

    // A rate as the dashboard shows it: no data when it is not finite, not a fake zero.
    private static string RateMarkup(double rate)
    {
        if (!double.IsFinite(rate))
            return NoDataMarkup;

        return FormatFiniteRate(rate);
    }

    // The requests chart's axis and annotation format: a count, whole — the scale's midpoint can
    // land between two counts — rounded half away from zero, and past what a long holds (a
    // series value that never was a count) printed as it is rather than wrapped.
    private static string FormatCountLabel(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded) < long.MaxValue)
            return FormatCount((long)rounded);

        return rounded.ToString("0", CultureInfo.InvariantCulture);
    }

    // The latency chart's axis and annotation format: the p95 tile's number with its unit for a
    // value a TimeSpan holds, a plain whole number of milliseconds for one it does not (a
    // declared limit past the tick range) — the widget formats only finite values.
    private static string FormatResponseTimeLabel(double milliseconds)
    {
        if (Math.Abs(milliseconds) <= MaxResponseTimeMilliseconds)
            return FormatResponseTimeValue(TimeSpan.FromMilliseconds(milliseconds)) + " " + ResponseTimeUnit;

        return FormatCountLabel(milliseconds) + " " + ResponseTimeUnit;
    }

    // The number of a positive response time as the shared formatter (ToTestFuznResponseTime)
    // prints it — whole milliseconds, "<1" below one — for a tile that shows the unit on its own.
    private static string FormatResponseTimeValue(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1)
            return "<1";

        return duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);
    }

    // A share as a percentage with its sign, for the step rows.
    private static string FormatPercent(double fraction)
    {
        return FormatPercentNumber(fraction) + PercentUnit;
    }

    // The number of a percentage: one decimal below 10% and from 99.5% up, whole numbers
    // between, with both ends kept honest — a nonzero share never reads as 0 and a share below
    // 100% never reads as 100. The errors tile shows the sign as its unit.
    private static string FormatPercentNumber(double fraction)
    {
        var percent = fraction * 100;
        if (percent > 0 && percent < 0.1)
            return "<0.1";

        if (percent > 99.9 && percent < 100)
            return ">99.9";

        if (percent < 10 || (percent >= 99.5 && percent < 100))
            return percent.ToString("0.0", CultureInfo.InvariantCulture);

        return percent.ToString("0", CultureInfo.InvariantCulture);
    }

    // Which optional parts the tiles carry at this window size: the trends need the width and
    // the height, the gauges the height (see the class summary).
    private readonly struct TileOptions
    {
        public bool IncludeTrends { get; }
        public bool IncludeGauges { get; }

        public TileOptions(bool includeTrends, bool includeGauges)
        {
            IncludeTrends = includeTrends;
            IncludeGauges = includeGauges;
        }
    }

    // A run of the error ticker's text in one markup style (null for the default), sanitized
    // so every character is one column: what the ticker wraps and re-marks-up per line.
    private readonly struct TextRun
    {
        public string Text { get; }
        public string? Style { get; }

        public TextRun(string text, string? style)
        {
            Text = text;
            Style = style;
        }
    }

    // The pieces of a section past its header block and requests panel, as the drop order
    // settled them for the window and the section's budget (see PlanSection): which panels
    // render, how tall the chart and heatmap bodies are, whether the charts share a row, and
    // how many step rows and error entries their tables keep.
    private sealed class SectionPlan
    {
        public bool SideBySideCharts { get; }
        public int ChartBodyHeight { get; }
        public bool IncludeRequestsChart { get; }
        public bool IncludeLatencyChart { get; }
        public int HeatmapHeight { get; }
        public bool IncludeHeatmap => HeatmapHeight > 0;
        public bool IncludeSteps { get; }
        public int StepRowCount { get; }
        public bool IncludeErrors { get; }
        public int ErrorEntryCount { get; }

        public SectionPlan(bool sideBySideCharts, int chartBodyHeight, bool includeRequestsChart, bool includeLatencyChart, int heatmapHeight, bool includeSteps, int stepRowCount, bool includeErrors, int errorEntryCount)
        {
            SideBySideCharts = sideBySideCharts && includeRequestsChart && includeLatencyChart;
            ChartBodyHeight = chartBodyHeight;
            IncludeRequestsChart = includeRequestsChart;
            IncludeLatencyChart = includeLatencyChart;
            HeatmapHeight = heatmapHeight;
            IncludeSteps = includeSteps;
            StepRowCount = stepRowCount;
            IncludeErrors = includeErrors;
            ErrorEntryCount = errorEntryCount;
        }
    }

    // The scenario's declared thresholds sorted onto the tiles and the latency chart: the one
    // on each standard tile's metric (the last declared, should a metric repeat), the p99 one
    // for the chart's limit line, and, in declaration order, those that add a tile of their own.
    private sealed class DeclaredThresholds
    {
        public LiveThreshold? RequestsPerSecond { get; }
        public LiveThreshold? ResponseTimePercentile95 { get; }
        public LiveThreshold? ResponseTimePercentile99 { get; }
        public LiveThreshold? ErrorRate { get; }
        public IReadOnlyList<LiveThreshold> Added { get; }

        public DeclaredThresholds(IReadOnlyList<LiveThreshold> thresholds)
        {
            var added = new List<LiveThreshold>();
            foreach (var threshold in thresholds)
            {
                switch (threshold.Threshold.Metric)
                {
                    case ThresholdMetric.RequestsPerSecond:
                        RequestsPerSecond = threshold;
                        break;
                    case ThresholdMetric.ResponseTimePercentile95:
                        ResponseTimePercentile95 = threshold;
                        break;
                    case ThresholdMetric.ResponseTimePercentile99:
                        ResponseTimePercentile99 = threshold;
                        added.Add(threshold);
                        break;
                    case ThresholdMetric.ErrorRate:
                        ErrorRate = threshold;
                        break;
                    default:
                        added.Add(threshold);
                        break;
                }
            }

            Added = added;
        }
    }
}
