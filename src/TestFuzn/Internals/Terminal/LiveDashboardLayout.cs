using System.Globalization;
using System.Text;
using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Lays out the standalone runner's full-screen live load dashboard as one section per scenario
/// — a header (scenario name, status, elapsed/planned with progress bar and ETA, simulation
/// phase, the compact logo top-right when the width allows), RPS and interval-p95 sparkline
/// panels, a requests panel with Ok/Failed rows (count, current rate and the response-time
/// spread), a per-step live table, and an error ticker — closed by a key-hint footer that owns
/// the window's last row. Pure composition of the widgets in this namespace: everything shown
/// comes from the passed <see cref="LiveMetricsSnapshot"/>s (no console, no clock — elapsed and
/// ETA are snapshot values), so identical inputs render an identical frame, which the
/// <see cref="FrameRenderer"/> diff depends on. Every rate on screen is one measure — the
/// current-interval rate, in the sparkline header, the requests rows (the newest sample's ok
/// and failed deltas over its interval) and the step rows alike — never a lifetime average, and
/// a value that is absent or not finite renders as an em dash instead of a fake zero. No emitted
/// line is ever wider than the given width at any width: the logo drops below
/// <see cref="MinimumWidthForLogo"/> columns, the sparkline panels below
/// <see cref="MinimumWidthForSparklines"/>, the requests table narrows its column set by the
/// columns' actual content widths (min/p75/p99 go first, then p50/max) so a number is never cut
/// short, the timing line drops its least important pieces whole rather than cutting a value,
/// and the remaining pieces degrade through the widgets' own narrow-width behavior down to
/// rendering nothing at degenerate widths. An indeterminate plan (null planned duration,
/// progress and ETA) renders an elapsed-only header with no progress bar, and a Failed status
/// is never reason-less on screen — <see cref="LiveMetricsSnapshot.StatusDetail"/> renders as
/// its own styled header line. The frame is fitted to the height: content is clipped to the
/// rows above the footer (a frame taller than the window loses its tail, never the quit hint —
/// each section leads with its most important lines) and padded down so the footer lands on
/// the last row; a height below 1 leaves the frame unclipped and unpadded. Alternate-screen
/// entry/exit and the render loop are the caller's job. Stateless and thread-safe.
/// </summary>
internal static class LiveDashboardLayout
{
    /// <summary>Below this width the compact logo is dropped from the first section's title line.</summary>
    public const int MinimumWidthForLogo = 80;

    /// <summary>Below this width the sparkline panels are dropped.</summary>
    public const int MinimumWidthForSparklines = 60;

    // The dashboard's palette as markup style constants, so retheming is a one-place edit. The
    // warm accents stay inside the logo gradient family (#FF5C00 → #FFCF6B).
    private const string PanelHeaderStyle = "bold #ff9d3d";
    private const string ProgressBarStyle = "#ff9d3d";
    private const string RequestsSparklineStyle = "#ff9d3d";
    private const string ResponseTimeSparklineStyle = "#ffcf6b";
    private const string PhaseStyle = "#ffcf6b";
    private const string OkStyle = "green";
    private const string FailedStyle = "red";
    private const string RunningStyle = "yellow";
    private const string WarningStyle = "yellow";
    private const string SecondaryStyle = "dim";

    // What every place with no number to show renders: a value that is absent (no sample yet)
    // or not finite.
    private const string NoDataMarkup = "[" + SecondaryStyle + "]—[/]";

    // Columns between horizontally adjacent pieces: title and logo, the two sparkline panels,
    // and the timing line's elapsed part and progress bar.
    private const int ColumnGap = 2;

    // The progress bar's fixed width, percent label included; the bar is dropped when the
    // timing line has no room for it after the elapsed/planned part.
    private const int ProgressBarTotalWidth = 24;

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
        new TableColumn("[" + SecondaryStyle + "]count[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]rps[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]min[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]mean[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]p50[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]p75[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]p95[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]p99[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]max[/]") { Alignment = TextAlignment.Right }
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
        new TableColumn("[" + SecondaryStyle + "]step[/]") { MaxWidth = 32 },
        new TableColumn("[" + SecondaryStyle + "]count[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]rps[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]mean[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]p95[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]failed[/]") { Alignment = TextAlignment.Right },
        new TableColumn("[" + SecondaryStyle + "]fail%[/]")
    };

    /// <summary>
    /// Renders the full dashboard frame for the given scenario snapshots, in order, at the
    /// given window size. Returns one <see cref="RenderedLine"/> per terminal row, ready for
    /// <see cref="FrameBuffer.AddLines(IEnumerable{RenderedLine})"/>: exactly
    /// <paramref name="height"/> rows for a height of 1 or more, the content clipped or padded
    /// to the rows above the footer on the last row; the unclipped content plus the footer for
    /// a smaller height. A width below 1 renders nothing. The glyph set is passed through to the
    /// sparkline panels so the caller can match it to the terminal's font support. The spinner
    /// glyph, when given, is drawn in place of the dot on a running scenario's status badge — a
    /// single-column glyph the caller's render loop advances per frame; null keeps the dot, and
    /// finished badges (passed, failed, skipped) always keep theirs.
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

            AddScenarioSection(lines, snapshots[index], width, colorMode, sparklineGlyphSet, spinnerGlyph, includeLogo: index == 0 && width >= MinimumWidthForLogo);
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

    private static void AddScenarioSection(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, int width, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet, string? spinnerGlyph, bool includeLogo)
    {
        lines.Add(RenderTitleLine(snapshot, width, colorMode, spinnerGlyph, includeLogo));
        lines.Add(RenderTimingLine(snapshot, width, colorMode));

        if (snapshot.StatusDetail != null)
            lines.Add(MarkupText.RenderTruncated("[" + FailedStyle + "]✗ " + MarkupParser.Escape(snapshot.StatusDetail) + "[/]", width, colorMode));

        lines.Add(BlankLine);

        if (width >= MinimumWidthForSparklines)
            AddSparklinePanels(lines, snapshot, width, colorMode, sparklineGlyphSet);

        AddRequestsPanel(lines, snapshot, width, colorMode);

        if (snapshot.Steps.Count > 0)
            AddStepsPanel(lines, snapshot.Steps, width, colorMode);

        if (snapshot.Errors.Count > 0)
            AddErrorsPanel(lines, snapshot.Errors, width, colorMode);
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
            return "[" + FailedStyle + "]● Failed[/]";

        if (snapshot.Status == TestStatus.Skipped)
            return "[" + SecondaryStyle + "]● Skipped[/]";

        if (snapshot.IsCompleted)
            return "[" + OkStyle + "]● Passed[/]";

        var runningGlyph = "●";
        if (spinnerGlyph != null)
            runningGlyph = MarkupParser.Escape(spinnerGlyph);

        return "[" + RunningStyle + "]" + runningGlyph + " Running[/]";
    }

    // Elapsed first (truncated only at degenerate widths), then in importance order the planned
    // total, the progress bar, the ETA and the phase label. The value pieces are appended only
    // while each fits whole — the first that does not fit ends the line, so the least important
    // go first (phase label, then ETA, then planned total) and no value is ever cut mid-way. The
    // bar is decoration for the ETA beside it, so a bar that does not fit is skipped instead of
    // ending the line. An indeterminate plan has no planned total, bar or ETA, so it renders an
    // elapsed-only line with no fake progress.
    private static RenderedLine RenderTimingLine(LiveMetricsSnapshot snapshot, int width, ColorMode colorMode)
    {
        var elapsed = MarkupText.RenderTruncated("[" + SecondaryStyle + "]elapsed[/] " + FormatClock(snapshot.Duration), width, colorMode);
        var line = new StringBuilder(elapsed.Text);
        var usedWidth = elapsed.Width;

        var fits = true;
        if (snapshot.PlannedDuration != null)
            fits = AppendIfFits(line, ref usedWidth, " [" + SecondaryStyle + "]/[/] " + FormatClock(snapshot.PlannedDuration.Value), width, colorMode);

        if (snapshot.ProgressFraction != null)
        {
            var bar = ProgressBarWidget.Render(snapshot.ProgressFraction.Value, ProgressBarTotalWidth, colorMode, showPercentLabel: true, barStyle: ProgressBarStyle);
            if (usedWidth + ColumnGap + bar[0].Width <= width)
            {
                line.Append(' ', ColumnGap).Append(bar[0].Text);
                usedWidth += ColumnGap + bar[0].Width;
            }
        }

        if (fits && snapshot.EstimatedTimeRemaining != null)
            fits = AppendIfFits(line, ref usedWidth, "  [" + SecondaryStyle + "]eta[/] " + FormatClock(snapshot.EstimatedTimeRemaining.Value), width, colorMode);

        if (fits && snapshot.PhaseLabel.Length > 0)
            AppendIfFits(line, ref usedWidth, "  [" + SecondaryStyle + "]·[/] [" + PhaseStyle + "]" + MarkupParser.Escape(snapshot.PhaseLabel) + "[/]", width, colorMode);

        return new RenderedLine(line.ToString(), usedWidth);
    }

    // Appends a markup piece only when it fits whole in the width left on the line.
    private static bool AppendIfFits(StringBuilder line, ref int usedWidth, string markup, int width, ColorMode colorMode)
    {
        var pieceWidth = MarkupText.Measure(markup);
        if (usedWidth + pieceWidth > width)
            return false;

        var piece = MarkupText.RenderTruncated(markup, pieceWidth, colorMode);
        line.Append(piece.Text);
        usedWidth += piece.Width;
        return true;
    }

    // Two side-by-side panels: current-interval RPS on the left, per-interval Ok p95 on the
    // right, each headed by the series' newest value. The panel widths sum to the full width
    // with the gap, and each sparkline renders at its panel's inner width, so the joined rows
    // are exactly the frame width.
    private static void AddSparklinePanels(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, int width, ColorMode colorMode, SparklineGlyphSet sparklineGlyphSet)
    {
        var leftWidth = (width - ColumnGap) / 2;
        var rightWidth = width - ColumnGap - leftWidth;

        var requestsHeader = "[" + PanelHeaderStyle + "]rps[/] " + CurrentRateLabel(snapshot.RequestsPerSecondSeries);
        var responseTimeHeader = "[" + PanelHeaderStyle + "]p95[/] " + CurrentResponseTimeLabel(snapshot.ResponseTimePercentile95Series);

        var left = RenderSparklinePanel(requestsHeader, snapshot.RequestsPerSecondSeries, RequestsSparklineStyle, leftWidth, colorMode, sparklineGlyphSet);
        var right = RenderSparklinePanel(responseTimeHeader, snapshot.ResponseTimePercentile95Series, ResponseTimeSparklineStyle, rightWidth, colorMode, sparklineGlyphSet);

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

        return FormatRate(series[series.Count - 1]);
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
            var warmupMarkup = "[" + SecondaryStyle + "]warmup[/] " + FormatCount(snapshot.WarmupRequestCountOk)
                + " [" + OkStyle + "]ok[/] [" + SecondaryStyle + "]·[/] " + FormatCount(snapshot.WarmupRequestCountFailed)
                + " [" + FailedStyle + "]failed[/]";
            content.Add(MarkupText.RenderTruncated(warmupMarkup, innerWidth, colorMode));
        }

        var rates = CurrentRateLabels(snapshot.Samples);
        var rows = new[]
        {
            RequestsRow("ok", OkStyle, snapshot.Ok, rates.Ok),
            RequestsRow("failed", FailedStyle, snapshot.Failed, rates.Failed)
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

        lines.AddRange(PanelWidget.Render("[" + PanelHeaderStyle + "]Requests[/]", content, width, colorMode));
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
            return (FormatRate(0), FormatRate(0));

        return (FormatRate(sample.RequestsPerSecond * sample.OkDelta / total), FormatRate(sample.RequestsPerSecond * sample.FailedDelta / total));
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
                FormatRate(step.RequestsPerSecond),
                step.ResponseTimeMean.ToTestFuznResponseTime(),
                step.ResponseTimePercentile95.ToTestFuznResponseTime(),
                FormatCount(step.RequestCountFailed),
                FailureBarMarkup(step.RequestCountOk, step.RequestCountFailed)
            });
        }

        var table = TableWidget.Render(StepColumns, rows, width - PanelWidget.ContentOverhead, colorMode);
        lines.AddRange(PanelWidget.Render("[" + PanelHeaderStyle + "]Steps[/]", table, width, colorMode));
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
            content.Add("[" + FailedStyle + "]" + FormatCount(error.Count).PadLeft(countWidth) + "×[/] [bold]"
                + MarkupParser.Escape(error.StepName) + "[/] [" + SecondaryStyle + "]·[/] " + MarkupParser.Escape(error.Message));
        }

        lines.AddRange(PanelWidget.Render("[" + PanelHeaderStyle + "]Errors[/]", content, width, colorMode));
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
            return "[" + SecondaryStyle + "]" + emptyTrack + "[/]";

        if (failedCount == 0)
            return "[" + SecondaryStyle + "]" + emptyTrack + "[/] 0%";

        var fraction = (double)failedCount / total;
        if (fraction < 0)
            fraction = 0;
        else if (fraction > 1)
            fraction = 1;

        var filledCount = (int)Math.Round(fraction * FailureBarCellCount, MidpointRounding.AwayFromZero);
        if (filledCount < 1)
            filledCount = 1;

        var style = fraction >= SevereFailureFraction ? FailedStyle : WarningStyle;
        return "[" + style + "]" + new string('█', filledCount) + "[/][" + SecondaryStyle + "]"
            + new string('░', FailureBarCellCount - filledCount) + "[/] " + FormatPercent(fraction);
    }

    // hh:mm:ss with unbounded hours, so runs past 24 hours keep counting instead of wrapping;
    // a negative duration displays as zero instead of as negative fields.
    private static string FormatClock(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        return ((int)duration.TotalHours).ToString("00", CultureInfo.InvariantCulture)
            + ":" + duration.Minutes.ToString("00", CultureInfo.InvariantCulture)
            + ":" + duration.Seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    // One decimal below 10 so slow-step rates stay readable, whole numbers above; a rate that
    // is not finite is no data, not zero.
    private static string FormatRate(double rate)
    {
        if (!double.IsFinite(rate))
            return NoDataMarkup;

        if (rate < 10)
            return rate.ToString("0.0", CultureInfo.InvariantCulture);

        return rate.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatCount(long count)
    {
        return count.ToString(CultureInfo.InvariantCulture);
    }

    // One decimal below 10% and from 99.5% up, whole numbers between, with both ends kept
    // honest: a nonzero share never reads as 0% and a share below 100% never reads as 100%.
    private static string FormatPercent(double fraction)
    {
        var percent = fraction * 100;
        if (percent > 0 && percent < 0.1)
            return "<0.1%";

        if (percent > 99.9 && percent < 100)
            return ">99.9%";

        if (percent < 10 || (percent >= 99.5 && percent < 100))
            return percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";

        return percent.ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
