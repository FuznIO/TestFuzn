using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Utils;

namespace Fuzn.TestFuzn.Internals.Terminal;

internal static class LoadSummaryLayout
{
    internal const string SimulationsLabel = "Simulations";

    internal const string ReportLabel = "Report";
    private const char BookendRuleGlyph = '═';
    private const string ErrorIndent = "  ";
    private const string ErrorContinuationIndent = "    ";

    private static readonly RenderedLine BlankLine = new RenderedLine(string.Empty, 0);

    public static IReadOnlyList<RenderedLine> Render(IEnumerable<KeyValuePair<Scenario, ScenarioLoadResult>> scenarioLoadResults, int width, ColorMode colorMode, string? reportPath = null, LoadViewHeader? header = null)
    {
        var results = Materialize(scenarioLoadResults);
        var contentWidth = Math.Max(width - LiveDashboardLayout.Gutter.Length, MinimumWidthOf(results));

        var lines = new List<RenderedLine>();
        if (header != null)
        {
            lines.AddRange(header.Render(contentWidth, colorMode));
            lines.Add(BlankLine);
        }

        for (var index = 0; index < results.Count; index++)
        {
            if (index > 0)
                lines.Add(BlankLine);


            AddScenario(lines, results[index].Key, results[index].Value, index + 1, results.Count, contentWidth, colorMode);
        }

        if (!string.IsNullOrEmpty(reportPath))
        {
            lines.Add(LiveDashboardLayout.SectionHeaderLine(ReportLabel, contentWidth, colorMode));
            lines.Add(MarkupText.RenderTruncated(MarkupParser.Escape(reportPath), int.MaxValue, colorMode));
        }

        return Bookend(LiveDashboardLayout.Indent(lines, contentWidth), contentWidth);
    }

    private static IReadOnlyList<RenderedLine> Bookend(IReadOnlyList<RenderedLine> lines, int contentWidth)
    {
        var ruleWidth = contentWidth + LiveDashboardLayout.Gutter.Length;
        var rule = new RenderedLine(new string(BookendRuleGlyph, ruleWidth), ruleWidth);

        var bookended = new List<RenderedLine>(lines.Count + 4);
        bookended.Add(BlankLine);
        bookended.Add(rule);
        bookended.AddRange(lines);
        bookended.Add(rule);
        bookended.Add(BlankLine);
        return bookended;
    }

    public static int MeasureMinimumWidth(IEnumerable<KeyValuePair<Scenario, ScenarioLoadResult>> scenarioLoadResults)
    {
        return MinimumWidthOf(Materialize(scenarioLoadResults));
    }

    private static int MinimumWidthOf(List<KeyValuePair<Scenario, ScenarioLoadResult>> results)
    {
        var width = 0;
        foreach (var result in results)
        {
            width = Math.Max(width, TestPhasesLayout.MeasureNaturalWidth(PhaseRows(result.Value)));
            width = Math.Max(width, StepPerformanceLayout.MeasureMinimumWidth(StepRows(result.Value)));
        }

        return width;
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

    private static void AddScenario(List<RenderedLine> lines, Scenario scenario, ScenarioLoadResult result, int scenarioNumber, int scenarioCount, int width, ColorMode colorMode)
    {
        lines.Add(TitleLine(scenario, result, scenarioNumber, scenarioCount, width, colorMode));
        lines.Add(BlankLine);

        var infoLines = InfoLines(result, width, colorMode);
        if (infoLines.Count > 0)
        {
            lines.AddRange(infoLines);
            lines.Add(BlankLine);
        }

        AddSection(lines, TestPhasesLayout.Header, TestPhasesLayout.Render(PhaseRows(result), width, colorMode, SimulationsOf(scenario)), width, colorMode);

        AddSection(lines, LiveDashboardLayout.RequestsHeader, TotalsLines(result, width, colorMode), width, colorMode);

        AddSection(lines, StepPerformanceLayout.Header, StepPerformanceLayout.Render(StepRows(result), width, colorMode), width, colorMode);

        var failureLines = FailureDetailsLayout.Lines(result.AssertWhileWarmingUpException, result.AssertWhileRunningException,
            result.AssertWhenDoneException, result.Status, LiveDashboardLayout.ScenarioFailedText, width);
        if (failureLines.Count > 0)
            AddSection(lines, FailureDetailsLayout.Header, RenderMarkupLines(failureLines, width, colorMode), width, colorMode);

        var errorLines = ErrorLines(result, width);
        if (errorLines.Count > 0)
            AddSection(lines, LiveDashboardLayout.StepErrorsHeader, RenderMarkupLines(errorLines, width, colorMode), width, colorMode);
    }

    private static RenderedLine TitleLine(Scenario scenario, ScenarioLoadResult result, int scenarioNumber, int scenarioCount, int width, ColorMode colorMode)
    {
        var title = scenarioCount == 1
            ? LiveDashboardLayout.ScenarioTitlePrefix + NameOf(scenario.Name)
            : "Scenario " + scenarioNumber + " - " + NameOf(scenario.Name);

        return LiveDashboardLayout.TitleLine(title, LiveDashboardLayout.StatusMarkup(result.Status, isCompleted: true, spinnerGlyph: null),
            result.TestRunTotalDuration(), width, colorMode);
    }

    private static IReadOnlyList<RenderedLine> InfoLines(ScenarioLoadResult result, int width, ColorMode colorMode)
    {
        if (string.IsNullOrEmpty(result.Description))
            return Array.Empty<RenderedLine>();

        return LiveDashboardLayout.DescriptionLines(result.Description, width, colorMode);
    }

    private static IReadOnlyList<SimulationInfo> SimulationsOf(Scenario scenario)
    {
        var simulations = new List<SimulationInfo>();
        if (scenario.SimulationsInternal == null)
            return simulations;

        foreach (var simulation in scenario.SimulationsInternal)
        {
            if (simulation == null)
                continue;

            simulations.Add(new SimulationInfo { Description = simulation.GetDescription(), IsWarmup = simulation.IsWarmup });
        }

        return simulations;
    }

    private static List<IReadOnlyList<string?>> PhaseRows(ScenarioLoadResult result)
    {
        var times = new TestPhasesLayout.PhaseTimes
        {
            InitStart = result.InitStartTime,
            InitEnd = result.InitEndTime,
            WarmupStart = result.WarmupStartTime,
            WarmupEnd = result.WarmupEndTime,
            MeasurementStart = result.MeasurementStartTime,
            MeasurementEnd = result.MeasurementEndTime,
            CleanupStart = result.CleanupStartTime,
            CleanupEnd = result.CleanupEndTime
        };

        return TestPhasesLayout.Rows(times, result.HasWarmupStep(), result.Created);
    }

    private static List<IReadOnlyList<string?>> StepRows(ScenarioLoadResult result)
    {
        var rows = new List<IReadOnlyList<string?>>();
        AddStatsRows(rows, StepPerformanceLayout.SummaryStepName, result.Ok, result.Failed, alwaysShowFailed: true);
        if (result.Steps != null)
            AddStepRows(rows, result.Steps.Values, level: 1);

        return rows;
    }

    private static void AddStepRows(List<IReadOnlyList<string?>> rows, IEnumerable<StepLoadResult> steps, int level)
    {
        foreach (var step in steps)
        {
            if (step == null)
                continue;

            var indent = level > 1 ? new string(' ', (level - 1) * 2) + StepPerformanceLayout.SubStepPrefix : string.Empty;
            AddStatsRows(rows, indent + NameOf(step.Name), step.Ok, step.Failed, alwaysShowFailed: false);
            if (step.Steps != null)
                AddStepRows(rows, step.Steps, level + 1);
        }
    }

    private static void AddStatsRows(List<IReadOnlyList<string?>> rows, string name, Stats ok, Stats failed, bool alwaysShowFailed)
    {
        rows.Add(StatsRow(name, isOk: true, ok));
        if (alwaysShowFailed || failed != null && failed.RequestCount > 0)
            rows.Add(StatsRow(string.Empty, isOk: false, failed));
    }

    private static string?[] StatsRow(string name, bool isOk, Stats stats)
    {
        if (stats == null)
            stats = new Stats();

        return StepPerformanceLayout.Row(name, isOk, stats.RequestCount, stats.RequestsPerSecond, stats.ResponseTimeMean, stats.ResponseTimeMedian,
            stats.ResponseTimePercentile75, stats.ResponseTimePercentile95, stats.ResponseTimePercentile99,
            stats.ResponseTimeMin, stats.ResponseTimeMax, stats.ResponseTimeStandardDeviation);
    }

    private static IReadOnlyList<RenderedLine> TotalsLines(ScenarioLoadResult result, int width, ColorMode colorMode)
    {
        return LiveDashboardLayout.TotalsLines(result.RequestCount, result.Ok.RequestCount, result.Failed.RequestCount,
            result.Ok.RequestsPerSecond, result.HasWarmupStep(), (long)result.WarmupRequestCountOk + result.WarmupRequestCountFailed, width, colorMode);
    }

    private static List<string?> ErrorLines(ScenarioLoadResult result, int width)
    {
        var lines = new List<string?>();
        if (result.Steps == null)
            return lines;

        foreach (var step in result.Steps)
        {
            if (step.Value == null || step.Value.Errors == null || step.Value.Errors.Count == 0)
                continue;

            lines.Add(MetricsTableLayout.Styled(LiveDashboardLayout.FailedStyle, MarkupParser.Escape(NameOf(step.Key)) + ":"));
            foreach (var error in step.Value.Errors)
            {
                var count = 0;
                if (error.Value != null)
                    count = error.Value.Count;

                var text = MarkupText.SanitizeControlCharacters(NameOf(error.Key)) + " (Count: " + LiveDashboardLayout.FormatCount(count) + ")";
                var wrapped = WrapText(text, width - ErrorContinuationIndent.Length);
                for (var index = 0; index < wrapped.Count; index++)
                {
                    var indent = index == 0 ? ErrorIndent : ErrorContinuationIndent;
                    lines.Add(MetricsTableLayout.Styled(LiveDashboardLayout.FailedStyle, indent + MarkupParser.Escape(wrapped[index])));
                }
            }
        }

        return lines;
    }

    private static void AddSection(List<RenderedLine> lines, string header, IReadOnlyList<RenderedLine> content, int width, ColorMode colorMode)
    {
        lines.Add(LiveDashboardLayout.SectionHeaderLine(header, width, colorMode));
        lines.AddRange(content);
        lines.Add(BlankLine);
    }

    private static IReadOnlyList<RenderedLine> RenderMarkupLines(IReadOnlyList<string?> markupLines, int width, ColorMode colorMode)
    {
        var lines = new List<RenderedLine>(markupLines.Count);
        foreach (var markupLine in markupLines)
            lines.Add(MarkupText.RenderTruncated(markupLine, width, colorMode));

        return lines;
    }

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

    private static string NameOf(string? text)
    {
        if (text == null)
            return string.Empty;

        return text;
    }
}
