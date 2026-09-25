using System.Globalization;
using System.Text;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class LiveDashboardLayout
{
    internal const string ScenarioTitlePrefix = "Scenario - ";
    internal const string RunningText = "Running";
    internal const string PassedText = "Passed";
    internal const string FailedText = "Failed";
    internal const string SkippedText = "Skipped";
    internal const string ScenarioFailedText = "Scenario Failed";
    internal const string DescriptionLabel = "Description";
    internal const string ScenariosTitle = "Scenarios";
    internal const string ScenarioPerformanceHeader = "Scenario Performance";
    internal const string StepErrorsHeader = "Step Errors";
    internal const string RequestsHeader = "Requests";
    internal const string TotalRequestsLabel = "Total Requests";
    internal const string SuccessfulLabel = "Successful";
    internal const string FailedLabel = "Failed";
    internal const string RequestsPerSecondLabel = "Requests/sec";

    internal const string SectionHeaderStyle = "bold #ff9d3d";
    internal const string PanelHeaderStyle = SectionHeaderStyle;
    internal const string OkStyle = "green";
    internal const string FailedStyle = "red";
    private const string RunningStyle = "yellow";
    internal const string WarningStyle = "yellow";
    internal const string SecondaryStyle = "dim";

    internal const string Gutter = "  ";
    internal const char TopRuleGlyph = '═';
    private const string LabelSeparator = "    ";
    private const int InfoLabelWidth = 14;
    private const string ErrorIndent = "  ";
    private const string ErrorContinuationIndent = "    ";

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    public const char QuitKey = 'q';

    private static readonly KeyHint[] FooterHints = { new KeyHint(QuitKey.ToString(), "quit") };

    private static readonly TableColumn[] ScenarioColumns =
    {
        MetricsTableLayout.HeaderColumn("#"),
        MetricsTableLayout.HeaderColumn("Scenario"),
        MetricsTableLayout.HeaderColumn("Phase"),
        MetricsTableLayout.HeaderColumn("Status"),
        MetricsTableLayout.NumberColumn("Requests"),
        MetricsTableLayout.NumberColumn("RPS"),
        MetricsTableLayout.NumberColumn("Mean"),
        MetricsTableLayout.NumberColumn("Median"),
        MetricsTableLayout.NumberColumn("P95"),
        MetricsTableLayout.NumberColumn("Failed")
    };

    private static readonly int[][] ScenarioColumnTiers =
    {
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
        new[] { 0, 1, 2, 3, 4, 5, 6, 8, 9 },
        new[] { 0, 1, 2, 3, 4, 5, 8, 9 },
        new[] { 0, 1, 3, 4, 8, 9 },
        new[] { 1, 3, 4, 9 }
    };

    public static IReadOnlyList<RenderedLine> Render(IReadOnlyList<LiveMetricsSnapshot> snapshots, int width, int height, ColorMode colorMode, string? spinnerGlyph = null, LoadViewHeader? header = null)
    {
        if (snapshots == null)
            throw new ArgumentNullException(nameof(snapshots), "Snapshots cannot be null.");

        if (width < 1)
            return Array.Empty<RenderedLine>();

        var contentWidth = width - Gutter.Length;
        if (contentWidth < 1)
            return Array.Empty<RenderedLine>();

        var content = new List<RenderedLine>();
        if (header != null)
        {
            content.AddRange(header.Render(contentWidth, colorMode));
            content.Add(BlankLine);
        }

        if (snapshots.Count == 1)
            AddSingleScenario(content, snapshots[0], contentWidth, colorMode, spinnerGlyph);
        else if (snapshots.Count > 1)
            AddScenarioOverview(content, snapshots, contentWidth, colorMode, spinnerGlyph);

        var lines = new List<RenderedLine> { new RenderedLine(new string(TopRuleGlyph, width), width) };
        lines.AddRange(Indent(content, contentWidth));

        if (height >= 1)
        {
            var contentHeight = height - 1;
            if (lines.Count > contentHeight)
                lines.RemoveRange(contentHeight, lines.Count - contentHeight);

            while (lines.Count < contentHeight)
                lines.Add(BlankLine);
        }

        lines.AddRange(Indent(KeyHintBarWidget.Render(FooterHints, contentWidth, colorMode), contentWidth));
        return lines;
    }

    internal static IReadOnlyList<RenderedLine> Indent(IReadOnlyList<RenderedLine> lines, int contentWidth)
    {
        var indented = new List<RenderedLine>(lines.Count);
        foreach (var line in lines)
        {
            if (line.Width == 0)
            {
                indented.Add(line);
                continue;
            }

            indented.Add(new RenderedLine(Gutter + line.Text, Gutter.Length + line.Width));
        }

        return indented;
    }

    private static void AddSingleScenario(List<RenderedLine> lines, LiveMetricsSnapshot snapshot, int width, ColorMode colorMode, string? spinnerGlyph)
    {
        lines.Add(TitleLine(ScenarioTitlePrefix + snapshot.ScenarioName, StatusMarkup(snapshot, spinnerGlyph), snapshot.Duration, width, colorMode));
        lines.Add(BlankLine);

        if (snapshot.Description.Length > 0)
        {
            lines.AddRange(DescriptionLines(snapshot.Description, width, colorMode));
            lines.Add(BlankLine);
        }

        var phaseRows = TestPhasesLayout.Rows(snapshot.PhaseTimes, snapshot.HasWarmup, snapshot.Timestamp);
        AddSection(lines, TestPhasesLayout.Header, TestPhasesLayout.Render(phaseRows, width, colorMode, snapshot.Simulations), width, colorMode);

        AddSection(lines, RequestsHeader, TotalsLines(snapshot, width, colorMode), width, colorMode);

        AddSection(lines, StepPerformanceLayout.Header, StepPerformanceLayout.Render(StepRows(snapshot), width, colorMode), width, colorMode);

        var failureLines = FailureDetailsLayout.Lines(snapshot.AssertWhileWarmingUpException, snapshot.AssertWhileRunningException,
            snapshot.AssertWhenDoneException, snapshot.Status, ScenarioFailedText, width);
        if (failureLines.Count > 0)
            AddSection(lines, FailureDetailsLayout.Header, RenderErrorLines(failureLines, width, colorMode), width, colorMode);

        var errorLines = ErrorLines(snapshot.Errors, width, scenarioName: null);
        if (errorLines.Count > 0)
            AddSection(lines, StepErrorsHeader, RenderErrorLines(errorLines, width, colorMode), width, colorMode);
    }

    internal static IReadOnlyList<RenderedLine> DescriptionLines(string description, int width, ColorMode colorMode)
    {
        var lines = new List<RenderedLine>();
        var text = MarkupText.SanitizeControlCharacters(description).TrimEnd();
        var wrapped = LoadSummaryLayout.WrapText(text, width - InfoLabelWidth);
        for (var index = 0; index < wrapped.Count; index++)
        {
            var labelMarkup = index == 0
                ? "[" + SecondaryStyle + "]" + DescriptionLabel.PadRight(InfoLabelWidth) + "[/]"
                : new string(' ', InfoLabelWidth);

            lines.Add(MarkupText.RenderTruncated(labelMarkup + MarkupParser.Escape(wrapped[index]), width, colorMode));
        }

        return lines;
    }

    private static void AddScenarioOverview(List<RenderedLine> lines, IReadOnlyList<LiveMetricsSnapshot> snapshots, int width, ColorMode colorMode, string? spinnerGlyph)
    {
        var elapsed = TimeSpan.Zero;
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Duration > elapsed)
                elapsed = snapshot.Duration;
        }

        lines.Add(TitleLine(ScenariosTitle + " " + snapshots.Count, OverallStatusMarkup(snapshots, spinnerGlyph), elapsed, width, colorMode));
        lines.Add(BlankLine);

        AddSection(lines, ScenarioPerformanceHeader, RenderScenarioTable(snapshots, width, colorMode, spinnerGlyph), width, colorMode);

        var errorLines = new List<string?>();
        foreach (var snapshot in snapshots)
            errorLines.AddRange(ErrorLines(snapshot.Errors, width, snapshot.ScenarioName));

        if (errorLines.Count > 0)
            AddSection(lines, StepErrorsHeader, RenderErrorLines(errorLines, width, colorMode), width, colorMode);
    }

    private static IReadOnlyList<RenderedLine> RenderScenarioTable(IReadOnlyList<LiveMetricsSnapshot> snapshots, int width, ColorMode colorMode, string? spinnerGlyph)
    {
        var rows = new List<IReadOnlyList<string?>>(snapshots.Count);
        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            var failedCount = FormatCount(snapshot.Failed.RequestCount);
            if (snapshot.Failed.RequestCount > 0)
                failedCount = MetricsTableLayout.Styled(FailedStyle, failedCount);

            rows.Add(new[]
            {
                FormatCount(index + 1),
                MarkupParser.Escape(snapshot.ScenarioName),
                PhaseName(snapshot),
                StatusMarkup(snapshot, spinnerGlyph),
                FormatCount((long)snapshot.Ok.RequestCount + snapshot.Failed.RequestCount),
                FormatCount(snapshot.RequestsPerSecond),
                snapshot.Ok.ResponseTimeMean.ToTestFuznResponseTime(),
                snapshot.Ok.ResponseTimeMedian.ToTestFuznResponseTime(),
                snapshot.Ok.ResponseTimePercentile95.ToTestFuznResponseTime(),
                failedCount
            });
        }

        for (var tier = 0; tier < ScenarioColumnTiers.Length; tier++)
        {
            var columns = SelectColumns(ScenarioColumns, ScenarioColumnTiers[tier]);
            var tierRows = SelectCells(rows, ScenarioColumnTiers[tier]);
            var isNarrowestTier = tier == ScenarioColumnTiers.Length - 1;
            if (isNarrowestTier || TableWidget.MeasureNaturalWidth(columns, tierRows) <= width)
                return TableWidget.Render(columns, tierRows, width, colorMode);
        }

        return Array.Empty<RenderedLine>();
    }

    private static List<IReadOnlyList<string?>> StepRows(LiveMetricsSnapshot snapshot)
    {
        var rows = new List<IReadOnlyList<string?>>();
        AddStatsRows(rows, StepPerformanceLayout.SummaryStepName, snapshot.Ok, snapshot.Failed, alwaysShowFailed: true);
        AddStepRows(rows, snapshot.Steps, level: 1);
        return rows;
    }

    private static void AddStepRows(List<IReadOnlyList<string?>> rows, IReadOnlyList<LiveStepMetrics> steps, int level)
    {
        foreach (var step in steps)
        {
            var indent = level > 1 ? new string(' ', (level - 1) * 2) + StepPerformanceLayout.SubStepPrefix : string.Empty;
            AddStatsRows(rows, indent + step.Name, step.Ok, step.Failed, alwaysShowFailed: false);
            AddStepRows(rows, step.Steps, level + 1);
        }
    }

    private static void AddStatsRows(List<IReadOnlyList<string?>> rows, string name, LiveStats ok, LiveStats failed, bool alwaysShowFailed)
    {
        rows.Add(StatsRow(name, isOk: true, ok));
        if (alwaysShowFailed || failed.RequestCount > 0)
            rows.Add(StatsRow(string.Empty, isOk: false, failed));
    }

    private static string?[] StatsRow(string name, bool isOk, LiveStats stats)
    {
        return StepPerformanceLayout.Row(name, isOk, stats.RequestCount, stats.RequestsPerSecond, stats.ResponseTimeMean, stats.ResponseTimeMedian,
            stats.ResponseTimePercentile75, stats.ResponseTimePercentile95, stats.ResponseTimePercentile99,
            stats.ResponseTimeMin, stats.ResponseTimeMax, stats.ResponseTimeStandardDeviation);
    }

    private static IReadOnlyList<RenderedLine> TotalsLines(LiveMetricsSnapshot snapshot, int width, ColorMode colorMode)
    {
        return TotalsLines((long)snapshot.Ok.RequestCount + snapshot.Failed.RequestCount, snapshot.Ok.RequestCount, snapshot.Failed.RequestCount,
            snapshot.RequestsPerSecond, snapshot.HasWarmup, (long)snapshot.WarmupRequestCountOk + snapshot.WarmupRequestCountFailed, width, colorMode);
    }

    internal static IReadOnlyList<RenderedLine> TotalsLines(long total, int okCount, int failedCount, int requestsPerSecond, bool hasWarmup, long warmupCount, int width, ColorMode colorMode)
    {
        var parts = new List<string>
        {
            "[" + SecondaryStyle + "]" + TotalRequestsLabel + "[/] " + FormatCount(total),
            "[" + SecondaryStyle + "]" + SuccessfulLabel + "[/] [" + OkStyle + "]" + FormatCount(okCount) + "[/] " + PercentageMarkup(okCount, total),
            "[" + SecondaryStyle + "]" + FailedLabel + "[/] [" + FailedStyle + "]" + FormatCount(failedCount) + "[/] " + PercentageMarkup(failedCount, total),
            "[" + SecondaryStyle + "]" + RequestsPerSecondLabel + "[/] " + FormatCount(requestsPerSecond)
        };

        if (hasWarmup)
            parts.Add("[" + SecondaryStyle + "]" + TestPhasesLayout.WarmupPhase + "[/] " + FormatCount(warmupCount));

        var lines = new List<RenderedLine>();
        var line = new StringBuilder();
        var lineWidth = 0;
        foreach (var part in parts)
        {
            var partWidth = MarkupText.Measure(part);
            if (lineWidth > 0 && lineWidth + LabelSeparator.Length + partWidth > width)
            {
                lines.Add(new RenderedLine(line.ToString(), lineWidth));
                line.Clear();
                lineWidth = 0;
            }

            if (lineWidth > 0)
            {
                line.Append(LabelSeparator);
                lineWidth += LabelSeparator.Length;
            }

            line.Append(MarkupText.RenderTruncated(part, width, colorMode).Text);
            lineWidth += Math.Min(partWidth, width);
        }

        if (lineWidth > 0)
            lines.Add(new RenderedLine(line.ToString(), lineWidth));

        return lines;
    }

    private static string PercentageMarkup(int count, long total)
    {
        if (total <= 0)
            return "[" + SecondaryStyle + "](0%)[/]";

        var percentage = Math.Round(100.0 * count / total, 1);
        return "[" + SecondaryStyle + "](" + percentage.ToString("0.#", CultureInfo.InvariantCulture) + "%)[/]";
    }

    internal static RenderedLine TitleLine(string title, string statusMarkup, TimeSpan elapsed, int width, ColorMode colorMode)
    {
        return TitleLine(title, statusMarkup, elapsed.ToTestFuznReadableString(), width, colorMode);
    }

    private static RenderedLine TitleLine(string title, string statusMarkup, string elapsed, int width, ColorMode colorMode)
    {
        var left = MarkupText.RenderTruncated("[bold]" + MarkupParser.Escape(title) + "[/]", width, colorMode);
        var rightMarkup = statusMarkup + "   [" + SecondaryStyle + "]" + elapsed + "[/]";
        var rightWidth = MarkupText.Measure(rightMarkup);
        if (left.Width + 3 + rightWidth > width)
            return left;

        var right = MarkupText.RenderTruncated(rightMarkup, rightWidth, colorMode);
        return new RenderedLine(left.Text + new string(' ', width - left.Width - right.Width) + right.Text, width);
    }

    private static void AddSection(List<RenderedLine> lines, string header, IReadOnlyList<RenderedLine> content, int width, ColorMode colorMode)
    {
        lines.Add(SectionHeaderLine(header, width, colorMode));
        lines.AddRange(content);
        lines.Add(BlankLine);
    }

    internal static RenderedLine SectionHeaderLine(string header, int width, ColorMode colorMode)
    {
        var title = MarkupText.RenderTruncated("[" + SectionHeaderStyle + "]" + header + "[/]", width, colorMode);
        var ruleWidth = width - title.Width - 1;
        if (ruleWidth < 1)
            return title;

        var rule = MarkupText.RenderTruncated("[" + SecondaryStyle + "]" + new string('─', ruleWidth) + "[/]", ruleWidth, colorMode);
        return new RenderedLine(title.Text + " " + rule.Text, width);
    }

    private static string StatusMarkup(LiveMetricsSnapshot snapshot, string? spinnerGlyph)
    {
        return StatusMarkup(snapshot.Status, snapshot.IsCompleted, spinnerGlyph);
    }

    internal static string StatusMarkup(TestStatus status, bool isCompleted, string? spinnerGlyph)
    {
        if (status == TestStatus.Failed)
            return "[" + FailedStyle + "]" + FailedText + "[/]";

        if (status == TestStatus.Skipped)
            return "[" + SecondaryStyle + "]" + SkippedText + "[/]";

        if (isCompleted)
            return "[" + OkStyle + "]" + PassedText + "[/]";

        var runningGlyph = "●";
        if (spinnerGlyph != null)
            runningGlyph = MarkupParser.Escape(spinnerGlyph);

        return "[" + RunningStyle + "]" + runningGlyph + " " + RunningText + "[/]";
    }

    private static string OverallStatusMarkup(IReadOnlyList<LiveMetricsSnapshot> snapshots, string? spinnerGlyph)
    {
        foreach (var snapshot in snapshots)
        {
            if (!snapshot.IsCompleted && snapshot.Status != TestStatus.Failed && snapshot.Status != TestStatus.Skipped)
                return StatusMarkup(snapshot, spinnerGlyph);
        }

        foreach (var snapshot in snapshots)
        {
            if (snapshot.Status == TestStatus.Failed)
                return "[" + FailedStyle + "]Failed[/]";
        }

        return "[" + OkStyle + "]Passed[/]";
    }

    internal static string PhaseName(LiveMetricsSnapshot snapshot)
    {
        switch (snapshot.Phase)
        {
            case LoadTestPhase.Cleanup:
                if (snapshot.IsCompleted)
                    return TestPhasesLayout.TotalPhase;
                return TestPhasesLayout.CleanupPhase;
            case LoadTestPhase.Measurement:
                return TestPhasesLayout.ExecutionPhase;
            case LoadTestPhase.Warmup:
                return TestPhasesLayout.WarmupPhase;
            default:
                return TestPhasesLayout.InitPhase;
        }
    }

    private static TableColumn[] SelectColumns(TableColumn[] columns, int[] columnIndexes)
    {
        var selected = new TableColumn[columnIndexes.Length];
        for (var index = 0; index < columnIndexes.Length; index++)
            selected[index] = columns[columnIndexes[index]];

        return selected;
    }

    private static IReadOnlyList<string?>[] SelectCells(IReadOnlyList<IReadOnlyList<string?>> rows, int[] columnIndexes)
    {
        var selected = new IReadOnlyList<string?>[rows.Count];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = new string?[columnIndexes.Length];
            for (var index = 0; index < columnIndexes.Length; index++)
                cells[index] = rows[rowIndex][columnIndexes[index]];

            selected[rowIndex] = cells;
        }

        return selected;
    }

    private static List<string?> ErrorLines(IReadOnlyList<LiveErrorEntry> errors, int width, string? scenarioName)
    {
        var lines = new List<string?>();
        foreach (var group in errors.GroupBy(error => error.StepName))
        {
            var groupName = group.Key;
            if (scenarioName != null)
                groupName = scenarioName + " · " + groupName;

            lines.Add(MetricsTableLayout.Styled(FailedStyle, MarkupParser.Escape(groupName) + ":"));
            foreach (var error in group)
            {
                var text = MarkupText.SanitizeControlCharacters(error.Message) + " (Count: " + FormatCount(error.Count) + ")";
                var wrapped = LoadSummaryLayout.WrapText(text, width - ErrorContinuationIndent.Length);
                for (var index = 0; index < wrapped.Count; index++)
                {
                    var indent = index == 0 ? ErrorIndent : ErrorContinuationIndent;
                    lines.Add(MetricsTableLayout.Styled(FailedStyle, indent + MarkupParser.Escape(wrapped[index])));
                }
            }
        }

        return lines;
    }

    private static IReadOnlyList<RenderedLine> RenderErrorLines(IReadOnlyList<string?> errorLines, int width, ColorMode colorMode)
    {
        var lines = new List<RenderedLine>(errorLines.Count);
        foreach (var line in errorLines)
            lines.Add(MarkupText.RenderTruncated(line, width, colorMode));

        return lines;
    }

    internal static string FormatClock(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        return ((int)duration.TotalHours).ToString("00", CultureInfo.InvariantCulture)
            + ":" + duration.Minutes.ToString("00", CultureInfo.InvariantCulture)
            + ":" + duration.Seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    internal static string FormatFiniteRate(double rate)
    {
        if (rate < 10)
            return rate.ToString("0.0", CultureInfo.InvariantCulture);

        return rate.ToString("0", CultureInfo.InvariantCulture);
    }

    internal static string FormatCount(long count)
    {
        return count.ToString(CultureInfo.InvariantCulture);
    }
}
