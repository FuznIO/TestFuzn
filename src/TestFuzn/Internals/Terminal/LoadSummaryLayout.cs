using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Thresholds;
using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// Lays out the standalone runner's final load test summary — what the runner writes to the
/// normal screen buffer once a load test has finished and the live view has been left — as a
/// column of panels per scenario, on the widgets of this namespace and in the dashboard's
/// palette, so the summary in the scrollback reads as the dashboard's sibling. Per scenario:
/// a <c>Load Test Summary</c> panel (scenario, execution time, test run time, status — one
/// table row when it fits the panel, one label/value line each otherwise), a
/// <c>Load Simulations</c> panel (every configured simulation's description, word-wrapped), a
/// <c>Global Metrics</c> panel with the scenario's requests table — titled
/// <c>Scenario Requests</c>: total, ok and failed counts with their rates — and its
/// response-time table (min, mean, max, standard deviation, median, p75, p95, p99 for the ok
/// and the failed requests), a <c>Thresholds</c> panel right after it with the completion
/// verdict of every declared threshold in declaration order (its metric's label, the relation
/// and limit it was declared with, the metric's cumulative value at completion and <c>✓</c> or
/// <c>✗</c>) — rendered only for a scenario that has a verdict, so one that declared no
/// threshold, and one whose run was stopped before the verdict was taken, have no such panel —
/// one <c>Step … Details</c> panel per step with the same two tables
/// — the requests table titled <c>Step Requests</c> and carrying a <c>Skipped</c> row, since a
/// step's total counts the iterations that reached it (an earlier step's failure skips the
/// steps after it, so a later step's total legitimately falls short of the scenario's) — and,
/// when any step recorded errors, an <c>Errors by Step</c> panel with every distinct error
/// message and its count under its step, word-wrapped with a hanging indent.
/// A number is never cut short — a right-aligned number truncated from the right reads as a
/// smaller number — so the metric tables are only ever rendered at their natural width: the
/// two sit side by side when both fit inside the panel and stack otherwise, and a
/// response-time spread too wide for the panel is split into two tables of four columns, then
/// four tables of two. <see cref="MeasureMinimumWidth"/> is the width at which the narrowest
/// split, the requests tables, the summary's label/value lines and the thresholds table of a
/// scenario that has a verdict all fit — whichever of them is widest binds; below it the
/// summary is laid out at that minimum instead of the given width, so its lines then exceed
/// the width (the standalone adapter lays out at its default width in that case, and the
/// terminal wraps). Only text is ever truncated: a scenario name in the summary lines, a step
/// name in a panel header. Names, simulation descriptions and error messages are escaped so
/// brackets render literally and control-sanitized; the numbers are formatted as the reports
/// and the plain stats lines format them. Everything shown comes from the passed results — no
/// console, no clock — so identical inputs render identical lines. Stateless and thread-safe.
/// </summary>
internal static class LoadSummaryLayout
{
    internal const string SummaryHeader = "Load Test Summary";
    internal const string SimulationsHeader = "Load Simulations";
    internal const string GlobalMetricsHeader = "Global Metrics";
    internal const string ThresholdsHeader = "Thresholds";
    internal const string ErrorsHeader = "Errors by Step";
    internal const string ScenarioRequestsTitle = "Scenario Requests";
    internal const string StepRequestsTitle = "Step Requests";
    internal const string ResponseTimesTitle = "Response Times";

    /// <summary>The verdict glyph of a threshold that held.</summary>
    internal const string ThresholdPassedGlyph = "✓";

    /// <summary>The verdict glyph of a threshold that was violated.</summary>
    internal const string ThresholdBreachedGlyph = "✗";

    // The summary panel's labels: the table's column headers, and, when the row does not fit,
    // the label of each line, padded to the widest of them.
    private const string ScenarioLabel = "Scenario";
    private const string ExecutionTimeLabel = "Execution Time";
    private const string TestRunTimeLabel = "Test Run Time";
    private const string StatusLabel = "Status";
    private const int SummaryLabelWidth = 14;
    private const string SummaryLabelSeparator = "  ";

    // Columns between the requests table and the response-time table when side by side.
    private const int TableGap = 4;

    // The indent of a wrapped simulation description's continuation lines.
    private const string ContinuationIndent = "  ";

    // An error line's indent under its step, and the deeper indent of its wrapped continuation.
    private const string ErrorIndent = "  ";
    private const string ErrorContinuationIndent = "    ";

    private const string TitleStyle = "bold";

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    private static readonly string[] SummaryLabels = { ScenarioLabel, ExecutionTimeLabel, TestRunTimeLabel, StatusLabel };

    private static readonly TableColumn[] SummaryColumns =
    {
        HeaderColumn(ScenarioLabel),
        HeaderColumn(ExecutionTimeLabel),
        HeaderColumn(TestRunTimeLabel),
        HeaderColumn(StatusLabel)
    };

    private static readonly TableColumn[] RequestsColumns =
    {
        HeaderColumn("Metric"),
        NumberColumn("Count"),
        NumberColumn("RPS")
    };

    // The threshold verdict's columns: what was declared, then what was measured, then whether
    // it held. The limit and the measured value are numbers, right-aligned as the metric tables
    // align theirs; the verdict is one glyph.
    private static readonly TableColumn[] ThresholdColumns =
    {
        HeaderColumn("Metric"),
        NumberColumn("Limit"),
        NumberColumn("Actual"),
        HeaderColumn("Result")
    };

    private static readonly TableColumn[] ResponseTimeColumns =
    {
        HeaderColumn("Metric"),
        NumberColumn("Min"),
        NumberColumn("Mean"),
        NumberColumn("Max"),
        NumberColumn("StdDev"),
        NumberColumn("Median"),
        NumberColumn("P75"),
        NumberColumn("P95"),
        NumberColumn("P99")
    };

    // How the response-time spread splits when it is too wide, as index groups into
    // ResponseTimeColumns without the metric column, which every table keeps in front: one
    // table of all eight numeric columns, two tables of four, four tables of two. The first
    // tier whose widest table fits is the one rendered.
    private static readonly int[][][] ResponseTimeColumnTiers =
    {
        new[] { new[] { 1, 2, 3, 4, 5, 6, 7, 8 } },
        new[] { new[] { 1, 2, 3, 4 }, new[] { 5, 6, 7, 8 } },
        new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 }, new[] { 7, 8 } }
    };

    /// <summary>
    /// Renders the summary of every scenario, in the given order, at the given width — or at
    /// <see cref="MeasureMinimumWidth"/> when the width is below it, so that no number is ever
    /// cut. A width below a panel's minimum renders nothing.
    /// </summary>
    public static IReadOnlyList<RenderedLine> Render(IEnumerable<KeyValuePair<Scenario, ScenarioLoadResult>> scenarioLoadResults, int width, ColorMode colorMode)
    {
        var results = Materialize(scenarioLoadResults);
        var layoutWidth = Math.Max(width, MinimumWidthOf(results));

        var lines = new List<RenderedLine>();
        foreach (var result in results)
            AddScenario(lines, result.Key, result.Value, layoutWidth, colorMode);

        return lines;
    }

    /// <summary>
    /// The narrowest width at which the summary shows every number whole: the requests tables,
    /// the narrowest split of the response-time spreads, the summary's label/value lines and the
    /// thresholds table of every scenario and step all fit inside a panel. Any of the four can be
    /// the binding one — a scenario whose verdict is wider than its numbers is measured by its
    /// thresholds table — so the minimum is the widest of them all. Only text is truncated at or
    /// above it; below it <see cref="Render"/> lays out at this width instead of the given one.
    /// </summary>
    public static int MeasureMinimumWidth(IEnumerable<KeyValuePair<Scenario, ScenarioLoadResult>> scenarioLoadResults)
    {
        return MinimumWidthOf(Materialize(scenarioLoadResults));
    }

    private static int MinimumWidthOf(List<KeyValuePair<Scenario, ScenarioLoadResult>> results)
    {
        var contentWidth = 0;
        foreach (var result in results)
        {
            contentWidth = Math.Max(contentWidth, SummaryLinesMinimumWidth(result.Key, result.Value));
            contentWidth = Math.Max(contentWidth, MetricsMinimumWidth(ScenarioRequestsTitle, result.Value.RequestCount, result.Value.Ok, result.Value.Failed, null));
            contentWidth = Math.Max(contentWidth, ThresholdsMinimumWidth(result.Value.ThresholdResults));

            if (result.Value.Steps == null)
                continue;

            foreach (var step in result.Value.Steps)
            {
                if (step.Value != null)
                    contentWidth = Math.Max(contentWidth, MetricsMinimumWidth(StepRequestsTitle, step.Value.RequestCount, step.Value.Ok, step.Value.Failed, step.Value.SkippedCount));
            }
        }

        return contentWidth + PanelWidget.ContentOverhead;
    }

    private static List<KeyValuePair<Scenario, ScenarioLoadResult>> Materialize(IEnumerable<KeyValuePair<Scenario, ScenarioLoadResult>> scenarioLoadResults)
    {
        if (scenarioLoadResults == null)
            throw new ArgumentNullException(nameof(scenarioLoadResults), "Scenario load results cannot be null.");

        var results = new List<KeyValuePair<Scenario, ScenarioLoadResult>>();
        foreach (var scenarioLoadResult in scenarioLoadResults)
        {
            if (scenarioLoadResult.Key == null || scenarioLoadResult.Value == null)
                throw new ArgumentException("Scenario load results cannot contain a null scenario or result.", nameof(scenarioLoadResults));

            results.Add(scenarioLoadResult);
        }

        return results;
    }

    private static void AddScenario(List<RenderedLine> lines, Scenario scenario, ScenarioLoadResult result, int width, ColorMode colorMode)
    {
        var innerWidth = width - PanelWidget.ContentOverhead;

        lines.AddRange(PanelWidget.Render(HeaderMarkup(SummaryHeader), SummaryLines(scenario, result, innerWidth, colorMode), width, colorMode));
        lines.AddRange(PanelWidget.Render(HeaderMarkup(SimulationsHeader), SimulationLines(scenario, innerWidth), width, colorMode));
        lines.AddRange(PanelWidget.Render(HeaderMarkup(GlobalMetricsHeader), MetricsLines(ScenarioRequestsTitle, result.RequestCount, result.Ok, result.Failed, null, innerWidth, colorMode), width, colorMode));

        // The completion verdict, right after the metrics it was read from; a scenario that
        // declared no threshold — or one whose run was stopped before the verdict — has none.
        if (result.ThresholdResults != null && result.ThresholdResults.Count > 0)
            lines.AddRange(PanelWidget.Render(HeaderMarkup(ThresholdsHeader), ThresholdLines(result.ThresholdResults, innerWidth, colorMode), width, colorMode));

        if (result.Steps != null)
        {
            foreach (var step in result.Steps)
            {
                if (step.Value == null)
                    continue;

                var stepLines = MetricsLines(StepRequestsTitle, step.Value.RequestCount, step.Value.Ok, step.Value.Failed, step.Value.SkippedCount, innerWidth, colorMode);
                lines.AddRange(PanelWidget.Render(HeaderMarkup("Step " + MarkupParser.Escape(step.Key) + " Details"), stepLines, width, colorMode));
            }
        }

        var errorLines = ErrorLines(result, innerWidth);
        if (errorLines.Count > 0)
            lines.AddRange(PanelWidget.Render("[bold " + TerminalPalette.FailedStyle + "]" + ErrorsHeader + "[/]", errorLines, width, colorMode));
    }

    // The summary as one table row when it fits at its natural width — nothing shrinks then —
    // and as one label/value line each otherwise. A line is truncated from its end, so only
    // the scenario name, the first line's value, can be cut; the durations and the status fit
    // by the minimum-width contract.
    private static IReadOnlyList<RenderedLine> SummaryLines(Scenario scenario, ScenarioLoadResult result, int innerWidth, ColorMode colorMode)
    {
        var row = SummaryRow(scenario, result);
        var rows = new[] { row };
        if (TableWidget.MeasureNaturalWidth(SummaryColumns, rows) <= innerWidth)
            return TableWidget.Render(SummaryColumns, rows, innerWidth, colorMode);

        var lines = new List<RenderedLine>(SummaryLabels.Length);
        for (var index = 0; index < SummaryLabels.Length; index++)
            lines.Add(MarkupText.RenderTruncated(SummaryLineMarkup(SummaryLabels[index], row[index]), innerWidth, colorMode));

        return lines;
    }

    private static int SummaryLinesMinimumWidth(Scenario scenario, ScenarioLoadResult result)
    {
        var row = SummaryRow(scenario, result);
        var valueWidth = 0;
        for (var index = 1; index < row.Length; index++)
            valueWidth = Math.Max(valueWidth, MarkupText.Measure(row[index]));

        return SummaryLabelWidth + SummaryLabelSeparator.Length + valueWidth;
    }

    private static string SummaryLineMarkup(string label, string? value)
    {
        return "[" + TerminalPalette.SecondaryStyle + "]" + label.PadRight(SummaryLabelWidth) + "[/]" + SummaryLabelSeparator + value;
    }

    private static string?[] SummaryRow(Scenario scenario, ScenarioLoadResult result)
    {
        return new[]
        {
            MarkupParser.Escape(NameOf(scenario.Name)),
            result.TotalExecutionDuration.ToTestFuznFormattedDuration(),
            result.TestRunTotalDuration().ToTestFuznFormattedDuration(),
            StatusMarkup(result.Status)
        };
    }

    private static string StatusMarkup(TestStatus status)
    {
        if (status == TestStatus.Failed)
            return "[" + TerminalPalette.FailedStyle + "]Failed[/]";

        if (status == TestStatus.Skipped)
            return "[" + TerminalPalette.SecondaryStyle + "]Skipped[/]";

        return "[" + TerminalPalette.OkStyle + "]Passed[/]";
    }

    // A "Type" heading and every configured simulation's description as the simulation formats
    // it (the trailing space a non-warmup description carries dropped), word-wrapped to the
    // panel with its continuation lines indented instead of being cut.
    private static List<string?> SimulationLines(Scenario scenario, int innerWidth)
    {
        var lines = new List<string?>();
        lines.Add("[" + TerminalPalette.SecondaryStyle + "]Type[/]");
        if (scenario.SimulationsInternal == null)
            return lines;

        foreach (var simulation in scenario.SimulationsInternal)
        {
            if (simulation == null)
                continue;

            var description = MarkupText.SanitizeControlCharacters(NameOf(simulation.GetDescription()).TrimEnd());
            var wrapped = WrapText(description, innerWidth - ContinuationIndent.Length);
            for (var index = 0; index < wrapped.Count; index++)
            {
                var indent = index == 0 ? string.Empty : ContinuationIndent;
                lines.Add(indent + MarkupParser.Escape(wrapped[index]));
            }
        }

        return lines;
    }

    // The requests table and the response-time table: side by side when both fit at their
    // natural width with the gap between, each padded to its own width so the right one
    // aligns; stacked otherwise — the requests table, a blank line, the response-time title,
    // then the spread as the widest split that fits, every table at its natural width so no
    // number is ever cut.
    private static IReadOnlyList<RenderedLine> MetricsLines(string requestsTitle, int requestCount, Stats ok, Stats failed, int? skippedCount, int innerWidth, ColorMode colorMode)
    {
        var requestsRows = RequestsRows(requestCount, ok, failed, skippedCount);
        var responseTimeRows = ResponseTimeRows(ok, failed);

        var requestsWidth = BlockWidth(requestsTitle, RequestsColumns, requestsRows);
        var responseTimesWidth = BlockWidth(ResponseTimesTitle, ResponseTimeColumns, responseTimeRows);

        if (requestsWidth + TableGap + responseTimesWidth <= innerWidth)
        {
            var left = TitledTableLines(requestsTitle, RequestsColumns, requestsRows, requestsWidth, colorMode);
            var right = TitledTableLines(ResponseTimesTitle, ResponseTimeColumns, responseTimeRows, responseTimesWidth, colorMode);
            return SideBySide(left, right, requestsWidth);
        }

        var lines = new List<RenderedLine>();
        lines.AddRange(TitledTableLines(requestsTitle, RequestsColumns, requestsRows, requestsWidth, colorMode));
        lines.Add(BlankLine);
        lines.Add(MarkupText.RenderTruncated(TitleMarkup(ResponseTimesTitle), innerWidth, colorMode));
        foreach (var columnGroup in SelectResponseTimeTier(responseTimeRows, innerWidth))
        {
            var columns = SelectColumns(columnGroup);
            var rows = SelectCells(responseTimeRows, columnGroup);
            lines.AddRange(TableWidget.Render(columns, rows, Math.Max(innerWidth, TableWidget.MeasureNaturalWidth(columns, rows)), colorMode));
        }

        return lines;
    }

    // The content width the metrics of one scenario or step need at the least: the requests
    // block, the response-time title and the widest table of the narrowest split.
    private static int MetricsMinimumWidth(string requestsTitle, int requestCount, Stats ok, Stats failed, int? skippedCount)
    {
        var requestsRows = RequestsRows(requestCount, ok, failed, skippedCount);
        var responseTimeRows = ResponseTimeRows(ok, failed);

        var width = BlockWidth(requestsTitle, RequestsColumns, requestsRows);
        width = Math.Max(width, MarkupText.Measure(TitleMarkup(ResponseTimesTitle)));
        return Math.Max(width, TierWidth(ResponseTimeColumnTiers[ResponseTimeColumnTiers.Length - 1], responseTimeRows));
    }

    private static int BlockWidth(string title, TableColumn[] columns, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        return Math.Max(MarkupText.Measure(TitleMarkup(title)), TableWidget.MeasureNaturalWidth(columns, rows));
    }

    // The widest split of the response-time spread whose every table fits the width at its
    // natural width; the narrowest split when none does (the caller lays out at the minimum
    // width, where it does).
    private static int[][] SelectResponseTimeTier(IReadOnlyList<IReadOnlyList<string?>> responseTimeRows, int innerWidth)
    {
        foreach (var tier in ResponseTimeColumnTiers)
        {
            if (TierWidth(tier, responseTimeRows) <= innerWidth)
                return tier;
        }

        return ResponseTimeColumnTiers[ResponseTimeColumnTiers.Length - 1];
    }

    private static int TierWidth(int[][] tier, IReadOnlyList<IReadOnlyList<string?>> responseTimeRows)
    {
        var width = 0;
        foreach (var columnGroup in tier)
            width = Math.Max(width, TableWidget.MeasureNaturalWidth(SelectColumns(columnGroup), SelectCells(responseTimeRows, columnGroup)));

        return width;
    }

    private static TableColumn[] SelectColumns(int[] columnGroup)
    {
        var columns = new TableColumn[columnGroup.Length + 1];
        columns[0] = ResponseTimeColumns[0];
        for (var index = 0; index < columnGroup.Length; index++)
            columns[index + 1] = ResponseTimeColumns[columnGroup[index]];

        return columns;
    }

    private static IReadOnlyList<string?>[] SelectCells(IReadOnlyList<IReadOnlyList<string?>> rows, int[] columnGroup)
    {
        var selected = new IReadOnlyList<string?>[rows.Count];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = new string?[columnGroup.Length + 1];
            cells[0] = rows[rowIndex][0];
            for (var index = 0; index < columnGroup.Length; index++)
                cells[index + 1] = rows[rowIndex][columnGroup[index]];

            selected[rowIndex] = cells;
        }

        return selected;
    }

    // A bold title line over the table, every line padded to the block width; the block width
    // is never below the table's natural width, so nothing in the table shrinks.
    private static List<RenderedLine> TitledTableLines(string title, TableColumn[] columns, IReadOnlyList<IReadOnlyList<string?>> rows, int blockWidth, ColorMode colorMode)
    {
        var lines = new List<RenderedLine>();
        lines.Add(MarkupText.RenderFitted(TitleMarkup(title), blockWidth, colorMode));
        foreach (var line in TableWidget.Render(columns, rows, blockWidth, colorMode))
            lines.Add(PadRight(line, blockWidth));

        return lines;
    }

    private static List<RenderedLine> SideBySide(List<RenderedLine> left, List<RenderedLine> right, int leftWidth)
    {
        var lines = new List<RenderedLine>();
        var gap = new string(' ', TableGap);
        var rowCount = Math.Max(left.Count, right.Count);
        for (var index = 0; index < rowCount; index++)
        {
            if (index >= right.Count)
            {
                lines.Add(left[index]);
                continue;
            }

            RenderedLine leftLine;
            if (index < left.Count)
                leftLine = left[index];
            else
                leftLine = new RenderedLine(new string(' ', leftWidth), leftWidth);

            lines.Add(new RenderedLine(leftLine.Text + gap + right[index].Text, leftLine.Width + TableGap + right[index].Width));
        }

        return lines;
    }

    private static List<IReadOnlyList<string?>> RequestsRows(int requestCount, Stats ok, Stats failed, int? skippedCount)
    {
        var rows = new List<IReadOnlyList<string?>>();
        rows.Add(new[] { "Total", LiveDashboardLayout.FormatCount(requestCount), string.Empty });
        rows.Add(new[]
        {
            Styled(TerminalPalette.OkStyle, "OK"),
            LiveDashboardLayout.FormatCount(ok.RequestCount),
            LiveDashboardLayout.FormatCount(ok.RequestsPerSecond)
        });
        rows.Add(new[]
        {
            Styled(TerminalPalette.FailedStyle, "Failed"),
            LiveDashboardLayout.FormatCount(failed.RequestCount),
            LiveDashboardLayout.FormatCount(failed.RequestsPerSecond)
        });
        if (skippedCount != null)
        {
            rows.Add(new[]
            {
                Styled(TerminalPalette.SecondaryStyle, "Skipped"),
                LiveDashboardLayout.FormatCount(skippedCount.Value),
                string.Empty
            });
        }

        return rows;
    }

    private static List<IReadOnlyList<string?>> ResponseTimeRows(Stats ok, Stats failed)
    {
        return new List<IReadOnlyList<string?>>
        {
            ResponseTimeRow("Ok", TerminalPalette.OkStyle, ok),
            ResponseTimeRow("Failed", TerminalPalette.FailedStyle, failed)
        };
    }

    private static string?[] ResponseTimeRow(string label, string style, Stats stats)
    {
        return new[]
        {
            Styled(style, label),
            Styled(style, stats.ResponseTimeMin.ToTestFuznResponseTime()),
            Styled(style, stats.ResponseTimeMean.ToTestFuznResponseTime()),
            Styled(style, stats.ResponseTimeMax.ToTestFuznResponseTime()),
            Styled(style, stats.ResponseTimeStandardDeviation.ToTestFuznResponseTime()),
            Styled(style, stats.ResponseTimeMedian.ToTestFuznResponseTime()),
            Styled(style, stats.ResponseTimePercentile75.ToTestFuznResponseTime()),
            Styled(style, stats.ResponseTimePercentile95.ToTestFuznResponseTime()),
            Styled(style, stats.ResponseTimePercentile99.ToTestFuznResponseTime())
        };
    }

    // The verdict of every declared threshold in declaration order: what it was declared as —
    // its metric's label and the relation it required against its limit — the metric's
    // cumulative value at completion, and whether it held. The table is only ever rendered at
    // its natural width, as the metric tables are, so a value is never cut short.
    private static IReadOnlyList<RenderedLine> ThresholdLines(IReadOnlyList<ThresholdResult> thresholdResults, int innerWidth, ColorMode colorMode)
    {
        var rows = ThresholdRows(thresholdResults);
        return TableWidget.Render(ThresholdColumns, rows, Math.Max(innerWidth, TableWidget.MeasureNaturalWidth(ThresholdColumns, rows)), colorMode);
    }

    // The content width the verdict table needs to show every value whole; zero when the
    // scenario has no verdict, which renders no panel at all.
    private static int ThresholdsMinimumWidth(IReadOnlyList<ThresholdResult> thresholdResults)
    {
        if (thresholdResults == null || thresholdResults.Count == 0)
            return 0;

        return TableWidget.MeasureNaturalWidth(ThresholdColumns, ThresholdRows(thresholdResults));
    }

    private static List<IReadOnlyList<string?>> ThresholdRows(IReadOnlyList<ThresholdResult> thresholdResults)
    {
        var rows = new List<IReadOnlyList<string?>>(thresholdResults.Count);
        foreach (var thresholdResult in thresholdResults)
        {
            var threshold = thresholdResult.Threshold;
            var verdict = Styled(TerminalPalette.FailedStyle, ThresholdBreachedGlyph);
            if (thresholdResult.Passed)
                verdict = Styled(TerminalPalette.OkStyle, ThresholdPassedGlyph);

            // The metric's label and the formatted values come from the thresholds' own
            // formatter and carry no bracket, but they are escaped all the same: nothing the
            // summary renders reaches the markup parser unescaped.
            rows.Add(new[]
            {
                MarkupParser.Escape(ThresholdFormat.Label(threshold.Metric)),
                MarkupParser.Escape(ThresholdFormat.RequiredComparisonSymbol(threshold.Comparison) + " " + ThresholdFormat.FormatValue(threshold.Metric, threshold.Limit)),
                MarkupParser.Escape(ThresholdFormat.FormatValue(threshold.Metric, thresholdResult.Current)),
                verdict
            });
        }

        return rows;
    }

    // Every step's distinct errors with their counts, under the step's name; a message wider
    // than the panel wraps at word boundaries with a hanging indent instead of being cut.
    private static List<string?> ErrorLines(ScenarioLoadResult result, int innerWidth)
    {
        var lines = new List<string?>();
        if (result.Steps == null)
            return lines;

        foreach (var step in result.Steps)
        {
            if (step.Value == null || step.Value.Errors == null || step.Value.Errors.Count == 0)
                continue;

            lines.Add(Styled(TerminalPalette.FailedStyle, MarkupParser.Escape(NameOf(step.Key)) + ":"));
            foreach (var error in step.Value.Errors)
            {
                var count = 0;
                if (error.Value != null)
                    count = error.Value.Count;

                var text = MarkupText.SanitizeControlCharacters(NameOf(error.Key)) + " (Count: " + LiveDashboardLayout.FormatCount(count) + ")";
                var wrapped = WrapText(text, innerWidth - ErrorContinuationIndent.Length);
                for (var index = 0; index < wrapped.Count; index++)
                {
                    var indent = index == 0 ? ErrorIndent : ErrorContinuationIndent;
                    lines.Add(Styled(TerminalPalette.FailedStyle, indent + MarkupParser.Escape(wrapped[index])));
                }
            }
        }

        return lines;
    }

    /// <summary>
    /// Breaks the text into lines of at most <paramref name="width"/> characters at spaces,
    /// splitting a word longer than the width where it must; a width below 1 leaves the text
    /// as one line. Never returns an empty list.
    /// </summary>
    internal static IReadOnlyList<string> WrapText(string text, int width)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");

        var lines = new List<string>();
        if (width < 1 || text.Length <= width)
        {
            lines.Add(text);
            return lines;
        }

        var position = 0;
        while (position < text.Length)
        {
            var remaining = text.Length - position;
            if (remaining <= width)
            {
                lines.Add(text.Substring(position));
                break;
            }

            var breakAt = text.LastIndexOf(' ', position + width, width + 1);
            if (breakAt <= position)
            {
                lines.Add(text.Substring(position, width));
                position += width;
                continue;
            }

            lines.Add(text.Substring(position, breakAt - position));
            position = breakAt + 1;
        }

        return lines;
    }

    private static RenderedLine PadRight(RenderedLine line, int width)
    {
        if (line.Width >= width)
            return line;

        return new RenderedLine(line.Text + new string(' ', width - line.Width), width);
    }

    private static string NameOf(string? text)
    {
        if (text == null)
            return string.Empty;

        return text;
    }

    private static string HeaderMarkup(string header)
    {
        return "[" + TerminalPalette.PanelHeaderStyle + "]" + header + "[/]";
    }

    private static string TitleMarkup(string title)
    {
        return "[" + TitleStyle + "]" + title + "[/]";
    }

    private static string Styled(string style, string markup)
    {
        return "[" + style + "]" + markup + "[/]";
    }

    private static TableColumn HeaderColumn(string header)
    {
        return new TableColumn("[" + TerminalPalette.SecondaryStyle + "]" + header + "[/]");
    }

    private static TableColumn NumberColumn(string header)
    {
        return new TableColumn("[" + TerminalPalette.SecondaryStyle + "]" + header + "[/]") { Alignment = TextAlignment.Right };
    }
}
