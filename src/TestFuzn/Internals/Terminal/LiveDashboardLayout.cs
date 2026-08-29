using System.Globalization;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Thresholds;
using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Lays out the standalone runner's full-screen live load dashboard as one section per scenario
/// — a title line (scenario name, status badge, the compact logo top-right when the width
/// allows), a KPI tile row, the plan's timeline, RPS and interval-p95 sparkline panels, a
/// requests panel with Ok/Failed rows (count, current rate and the response-time spread), a
/// per-step live table, and an error ticker — closed by a key-hint footer that owns the
/// window's last row. Pure composition of the widgets in this namespace: everything shown
/// comes from the passed <see cref="LiveMetricsSnapshot"/>s (no console, no clock — elapsed and
/// ETA are snapshot values), so identical inputs render an identical frame, which the
/// <see cref="FrameRenderer"/> diff depends on. Every rate on screen is one measure — the
/// current-interval rate, on the rps tile, in the sparkline header, the requests rows (the
/// newest sample's ok and failed deltas over its interval) and the step rows alike — never a
/// lifetime average, and a value that is absent or not finite renders as an em dash instead of
/// a fake zero.
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
/// <b>Timeline.</b> One <see cref="TimelineWidget"/> line (two with its legend) built from the
/// snapshot's plan entries one to one with the progress fraction as the position; only a
/// snapshot that carries a plan has one. The marker's segment is the phase, so the phase
/// label is written after the bar only when the plan gives the marker no position (an
/// indeterminate plan), and only while the bar keeps <see cref="MinimumTimelineWidth"/>
/// columns beside it. A Failed status is never reason-less on screen —
/// <see cref="LiveMetricsSnapshot.StatusDetail"/> renders as its own styled line under the
/// timeline.
/// </para>
/// <para>
/// <b>Width.</b> No emitted line is ever wider than the given width at any width: the logo
/// drops below <see cref="MinimumWidthForLogo"/> columns and the tile trends below
/// <see cref="MinimumWidthForTileTrends"/>; the tiles wrap onto two rows, the first taking the
/// larger half, when the row cannot give every box <see cref="MinimumTileBoxWidth"/> columns
/// (<see cref="MinimumWidthForSingleTileRow"/> — 64 for the five standard tiles: the
/// eight-column clock or the eight-letter requests label plus the box's borders and padding;
/// a wider value, a nine-digit count, drops its tile from the right as the widget does); the
/// sparkline panels drop below <see cref="MinimumWidthForSparklines"/>; the requests table
/// narrows its column set by the columns' actual content widths (min/p75/p99 go first, then
/// p50/max) so a number is never cut short; and the remaining pieces degrade through the
/// widgets' own narrow-width behavior down to rendering nothing at degenerate widths.
/// </para>
/// <para>
/// <b>Height.</b> The frame is fitted to the height: content is clipped to the rows above the
/// footer (a frame taller than the window loses its tail, never the quit hint — each section
/// leads with its most important lines) and padded down so the footer lands on the last row;
/// a height below 1 leaves the frame unclipped and unpadded with every optional row in. The
/// header gives rows back as the window shrinks, cheapest first: the tile trends go below
/// <see cref="MinimumHeightForTileTrends"/> rows, the gauges (the threshold bars and the
/// elapsed tile's progress with its time remaining) below <see cref="MinimumHeightForTileGauges"/>,
/// and the timeline below <see cref="MinimumHeightForTimeline"/>.
/// </para>
/// Alternate-screen entry/exit and the render loop are the caller's job. Stateless and
/// thread-safe.
/// </summary>
internal static class LiveDashboardLayout
{
    /// <summary>Below this width the compact logo is dropped from the first section's title line.</summary>
    public const int MinimumWidthForLogo = 80;

    /// <summary>Below this width the sparkline panels are dropped.</summary>
    public const int MinimumWidthForSparklines = 60;

    /// <summary>Below this width the tiles carry no trend sparkline.</summary>
    public const int MinimumWidthForTileTrends = 80;

    /// <summary>
    /// The narrowest box the tiles share one row at: the eight-column clock or requests label
    /// plus the box's borders and padding. A row that cannot give every box this many columns
    /// wraps onto two rows (see <see cref="MinimumWidthForSingleTileRow"/>).
    /// </summary>
    public const int MinimumTileBoxWidth = 12;

    /// <summary>Below this height the tiles carry no trend sparkline.</summary>
    public const int MinimumHeightForTileTrends = 30;

    /// <summary>Below this height the tiles carry no gauge — neither the threshold bars nor the elapsed tile's progress and time remaining.</summary>
    public const int MinimumHeightForTileGauges = 20;

    /// <summary>Below this height the timeline is dropped.</summary>
    public const int MinimumHeightForTimeline = 15;

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

    // The tiles every scenario has, in row order: rps, p95, errors, requests, elapsed; a
    // declared p99 or mean threshold adds its tile after them.
    private const int StandardTileCount = 5;

    // Columns between horizontally adjacent pieces: title and logo, the two sparkline panels,
    // and the timeline bar and the phase label after it.
    private const int ColumnGap = 2;

    // Cells in a step row's fail% mini-bar; failure fractions at or above
    // SevereFailureFraction color it red, smaller nonzero fractions yellow.
    private const int FailureBarCellCount = 5;
    private const double SevereFailureFraction = 0.05;

    // The largest magnitude in milliseconds a TimeSpan can hold (long.MaxValue ticks), for
    // guarding series values that never went through a TimeSpan.
    private const double MaxResponseTimeMilliseconds = long.MaxValue / TimeSpan.TicksPerMillisecond;

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    /// <summary>The key the footer advertises for a graceful stop of the run; the console manager acts on it in either case.</summary>
    public const char QuitKey = 'q';

    private static readonly KeyHint[] FooterHints = { new KeyHint(QuitKey.ToString(), "quit") };

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

    private static readonly TableColumn[] StepColumns =
    {
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]step[/]") { MaxWidth = 32 },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]count[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]rps[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]mean[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]p95[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]failed[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + TerminalPalette.SecondaryStyle + "]fail%[/]")
    };

    /// <summary>
    /// Renders the full dashboard frame for the given scenario snapshots, in order, at the
    /// given window size. Returns one <see cref="RenderedLine"/> per terminal row, ready for
    /// <see cref="FrameBuffer.AddLines(IEnumerable{RenderedLine})"/>: exactly
    /// <paramref name="height"/> rows for a height of 1 or more, the content clipped or padded
    /// to the rows above the footer on the last row, the header's optional rows given up as
    /// the class summary describes; the unclipped content plus the footer, every optional row
    /// in, for a smaller height. A width below 1 renders nothing. The glyph set is passed
    /// through to the tile trends and the sparkline panels so the caller can match it to the
    /// terminal's font support. The spinner glyph, when given, is drawn in place of the dot on
    /// a running scenario's status badge — a single-column glyph the caller's render loop
    /// advances per frame; null keeps the dot, and finished badges (passed, failed, skipped)
    /// always keep theirs.
    /// </summary>
    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<LiveMetricsSnapshot> snapshots, int width, int height, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet = SparklineGlyphSet.Braille, string? spinnerGlyph = null)
    {
        if (snapshots == null)
            throw new ArgumentNullException(nameof(snapshots), "Snapshots cannot be null.");

        if (width < 1)
            return Array.Empty<RenderedLine>();

        var lines = new List<RenderedLine>();
        for (var index = 0; index < snapshots.Count; index++)
        {
            if (index > 0)
                lines.Add(BlankLine);

            AddScenarioSection(lines, snapshots[index], width, height, colorMode, sparklineGlyphSet, spinnerGlyph, includeLogo: index == 0 && width >= MinimumWidthForLogo);
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

        lines.AddRange(KeyHintBarWidget.Render(FooterHints, width, colorMode));
        return lines;
    }

    /// <summary>
    /// The narrowest width at which <paramref name="tileCount"/> tiles share one row:
    /// <see cref="MinimumTileBoxWidth"/> per box and the widget's gap between boxes.
    /// </summary>
    public static int MinimumWidthForSingleTileRow(int tileCount)
    {
        return (tileCount * MinimumTileBoxWidth) + ((tileCount - 1) * TileRowWidget.Gap);
    }

    private static void AddScenarioSection(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, int width, int height, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet, string? spinnerGlyph, bool includeLogo)
    {
        lines.Add(RenderTitleLine(snapshot, width, colorMode, spinnerGlyph, includeLogo));
        AddTileRows(lines, snapshot, width, height, colorMode, sparklineGlyphSet);

        if (snapshot.PlanEntries.Count > 0 && HasRowsFor(height, MinimumHeightForTimeline))
            AddTimeline(lines, snapshot, width, colorMode);

        if (snapshot.StatusDetail != null)
            lines.Add(MarkupText.RenderTruncated("[" + TerminalPalette.FailedStyle + "]✗ " + MarkupParser.Escape(snapshot.StatusDetail) + "[/]", width, colorMode));

        lines.Add(BlankLine);

        if (width >= MinimumWidthForSparklines)
            AddSparklinePanels(lines, snapshot, width, colorMode, sparklineGlyphSet);

        AddRequestsPanel(lines, snapshot, width, colorMode);

        if (snapshot.Steps.Count > 0)
            AddStepsPanel(lines, snapshot.Steps, width, colorMode);

        if (snapshot.Errors.Count > 0)
            AddErrorsPanel(lines, snapshot.Errors, width, colorMode);
    }

    // Whether the window has the rows for an optional piece; an unbounded height (below 1)
    // has them all.
    private static bool HasRowsFor(int height, int minimumHeight)
    {
        return height < 1 || height >= minimumHeight;
    }

    // The scenario name and status badge, with the compact logo right-aligned on the same row
    // when requested — the title is fitted to the columns left of the reserved logo area, so
    // the two can never overlap.
    private static RenderedLine RenderTitleLine(LiveMetricsSnapshot snapshot, int width, ColorMode colorMode, string? spinnerGlyph, bool includeLogo)
    {
        var titleMarkup = "[bold]" + MarkupParser.Escape(snapshot.ScenarioName) + "[/]  " + StatusBadgeMarkup(snapshot, spinnerGlyph);
        if (!includeLogo)
            return MarkupText.RenderTruncated(titleMarkup, width, colorMode);

        var logo = LogoWidget.Render(LogoWidget.CompactWidth, LogoWidget.CompactHeight, colorMode);
        var title = MarkupText.RenderFitted(titleMarkup, width - LogoWidget.CompactWidth - ColumnGap, colorMode);
        return new RenderedLine(title.Text + new string(' ', ColumnGap) + logo[0].Text, width);
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
    private static void AddTileRows(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, int width, int height, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var options = new TileOptions(
            includeTrends: width >= MinimumWidthForTileTrends && HasRowsFor(height, MinimumHeightForTileTrends),
            includeGauges: HasRowsFor(height, MinimumHeightForTileGauges));
        var thresholds = new DeclaredThresholds(snapshot.Thresholds);
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

    private static void AddTileRow(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, DeclaredThresholds thresholds, TileOptions options, int firstTile, int count, int width, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var tiles = new StatTile[count];
        for (var index = 0; index < count; index++)
            tiles[index] = BuildTile(snapshot, thresholds, options, firstTile + index, TileInnerWidth(index, count, width));

        lines.AddRange(TileRowWidget.Render(tiles, width, colorMode, sparklineGlyphSet));
    }

    // The inner width the widget gives box index of count at this width — its equal share of
    // the width less the gaps, the leftover columns widening the first boxes by one, less the
    // borders and padding — mirrored here so a tile's texts can be sized to the box they land
    // in. Too small to fit anything at degenerate widths, where the widget drops tiles anyway.
    private static int TileInnerWidth(int index, int count, int width)
    {
        var available = width - ((count - 1) * TileRowWidget.Gap);
        var baseWidth = Math.DivRem(available, count, out var leftover);
        return baseWidth + (index < leftover ? 1 : 0) - PanelWidget.ContentOverhead;
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

    // The trailing samples with every non-latency (an idle interval's zero, a value no TimeSpan
    // holds) as a gap, so the trend never dips to a fake zero.
    private static double[] ResponseTimeTrend(IReadOnlyList<double> series)
    {
        var trend = Trailing(series, TrendSampleCount);
        for (var index = 0; index < trend.Length; index++)
        {
            if (!IsResponseTimeSample(trend[index]))
                trend[index] = double.NaN;
        }

        return trend;
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

    // Two side-by-side panels: current-interval RPS on the left, per-interval Ok p95 on the
    // right, each headed by the series' newest value. The panel widths sum to the full width
    // with the gap, and each sparkline renders at its panel's inner width, so the joined rows
    // are exactly the frame width.
    private static void AddSparklinePanels(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, int width, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var leftWidth = (width - ColumnGap) / 2;
        var rightWidth = width - ColumnGap - leftWidth;

        var requestsHeader = "[" + TerminalPalette.PanelHeaderStyle + "]rps[/] " + CurrentRateLabel(snapshot.RequestsPerSecondSeries);
        var responseTimeHeader = "[" + TerminalPalette.PanelHeaderStyle + "]p95[/] " + CurrentResponseTimeLabel(snapshot.ResponseTimePercentile95Series);

        var left = RenderSparklinePanel(requestsHeader, snapshot.RequestsPerSecondSeries, TerminalPalette.RequestsSparklineStyle, leftWidth, colorMode, sparklineGlyphSet);
        var right = RenderSparklinePanel(responseTimeHeader, snapshot.ResponseTimePercentile95Series, TerminalPalette.ResponseTimeSparklineStyle, rightWidth, colorMode, sparklineGlyphSet);

        for (var row = 0; row < left.Count; row++)
            lines.Add(new RenderedLine(left[row].Text + new string(' ', ColumnGap) + right[row].Text, width));
    }

    private static IReadOnlyList<RenderedLine> RenderSparklinePanel(string header, IReadOnlyList<double> series, string style, int panelWidth, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var sparkline = SparklineWidget.Render(series, panelWidth - PanelWidget.ContentOverhead, colorMode, sparklineGlyphSet, style);
        return PanelWidget.Render(header, sparkline, panelWidth, colorMode);
    }

    private static string CurrentRateLabel(IReadOnlyList<double> series)
    {
        if (series.Count == 0)
            return NoDataMarkup;

        return RateMarkup(series[series.Count - 1]);
    }

    // The newest per-interval p95, guarded because the series holds raw doubles: a value no
    // TimeSpan can hold (not finite, or beyond the tick range) shows as no data instead of
    // throwing out of the render loop.
    private static string CurrentResponseTimeLabel(IReadOnlyList<double> series)
    {
        if (series.Count == 0)
            return NoDataMarkup;

        var milliseconds = series[series.Count - 1];
        if (!double.IsFinite(milliseconds) || Math.Abs(milliseconds) > MaxResponseTimeMilliseconds)
            return NoDataMarkup;

        return TimeSpan.FromMilliseconds(milliseconds).ToTestFuznResponseTime();
    }

    // The measurement totals as a table — Ok and Failed rows with the count, the current rate
    // and the response-time spread — prefixed by a warmup headline once any warmup requests
    // exist. The column set is the widest tier whose natural width fits the panel; only when
    // even the narrowest tier cannot fit does the table widget shrink columns.
    private static void AddRequestsPanel(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, int width, ColorMode colorMode)
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

        lines.AddRange(PanelWidget.Render("[" + TerminalPalette.PanelHeaderStyle + "]Requests[/]", content, width, colorMode));
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
    // sparkline header's value) split in proportion to its deltas. The sample's rate is
    // (ok + failed) / interval, so ok / interval is ok × rate / (ok + failed) — exact without
    // the interval length, which the sample does not carry, and the two always sum to the
    // header's rate. No sample yet renders no data.
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

    private static void AddStepsPanel(List<RenderedLine> lines, IReadOnlyList<LiveStepMetrics> steps, int width, ColorMode colorMode)
    {
        var rows = new List<IReadOnlyList<string?>>(steps.Count);
        foreach (var step in steps)
        {
            rows.Add(new[]
            {
                MarkupParser.Escape(step.Name),
                FormatCount((long)step.RequestCountOk + step.RequestCountFailed),
                RateMarkup(step.RequestsPerSecond),
                step.ResponseTimeMean.ToTestFuznResponseTime(),
                step.ResponseTimePercentile95.ToTestFuznResponseTime(),
                FormatCount(step.RequestCountFailed),
                FailureBarMarkup(step.RequestCountOk, step.RequestCountFailed)
            });
        }

        var table = TableWidget.Render(StepColumns, rows, width - PanelWidget.ContentOverhead, colorMode);
        lines.AddRange(PanelWidget.Render("[" + TerminalPalette.PanelHeaderStyle + "]Steps[/]", table, width, colorMode));
    }

    // The distinct errors, most recently active first, one truncating line each: the count
    // right-aligned across entries, the step name, and the message. The messages are exception
    // text — markup-escaped here, control characters sanitized by the markup pipeline.
    private static void AddErrorsPanel(List<RenderedLine> lines, IReadOnlyList<LiveErrorEntry> errors, int width, ColorMode colorMode)
    {
        var countWidth = 0;
        foreach (var error in errors)
        {
            var length = FormatCount(error.Count).Length;
            if (length > countWidth)
                countWidth = length;
        }

        var content = new List<string?>(errors.Count);
        foreach (var error in errors)
        {
            content.Add("[" + TerminalPalette.FailedStyle + "]" + FormatCount(error.Count).PadLeft(countWidth) + "×[/] [bold]"
                + MarkupParser.Escape(error.StepName) + "[/] [" + TerminalPalette.SecondaryStyle + "]·[/] " + MarkupParser.Escape(error.Message));
        }

        lines.AddRange(PanelWidget.Render("[" + TerminalPalette.PanelHeaderStyle + "]Errors[/]", content, width, colorMode));
    }

    // A fixed-width severity bar for a step's failure share: no requests renders the empty
    // track alone, zero failures adds 0%, and a nonzero share fills at least one cell. The
    // share is clamped to 0..1, so counts that do not add up (a negative or wrapped counter)
    // can only render an empty or a full bar, never one of impossible length.
    private static string FailureBarMarkup(int okCount, int failedCount)
    {
        var emptyTrack = new string('░', FailureBarCellCount);
        var total = (long)okCount + failedCount;
        if (total == 0)
            return "[" + TerminalPalette.SecondaryStyle + "]" + emptyTrack + "[/]";

        if (failedCount == 0)
            return "[" + TerminalPalette.SecondaryStyle + "]" + emptyTrack + "[/] 0%";

        var fraction = (double)failedCount / total;
        if (fraction < 0)
            fraction = 0;
        else if (fraction > 1)
            fraction = 1;

        var filledCount = (int)Math.Round(fraction * FailureBarCellCount, MidpointRounding.AwayFromZero);
        if (filledCount < 1)
            filledCount = 1;

        var style = fraction >= SevereFailureFraction ? TerminalPalette.FailedStyle : TerminalPalette.WarningStyle;
        return "[" + style + "]" + new string('█', filledCount) + "[/][" + TerminalPalette.SecondaryStyle + "]"
            + new string('░', FailureBarCellCount - filledCount) + "[/] " + FormatPercent(fraction);
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

    // The scenario's declared thresholds sorted onto the tiles: the one on each standard tile's
    // metric (the last declared, should a metric repeat) and, in declaration order, those that
    // add a tile of their own.
    private sealed class DeclaredThresholds
    {
        public LiveThreshold? RequestsPerSecond { get; }
        public LiveThreshold? ResponseTimePercentile95 { get; }
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
