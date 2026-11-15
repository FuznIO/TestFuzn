using System.Net;
using System.Text;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Reports.Load;

internal class LoadHtmlReportWriter : ILoadReport
{
    private readonly IFileSystem _fileSystem;
    private readonly TestExecutionState _testExecutionState;

    public LoadHtmlReportWriter(IFileSystem fileSystem,
        TestExecutionState testExecutionState)
    {
        _fileSystem = fileSystem;
        _testExecutionState = testExecutionState;
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public async Task WriteReport(LoadReportData loadReportData)
    {
        try
        {
            var reportName = FileNameHelper.MakeFilenameSafe($"{loadReportData.Test.Group.Name}-{loadReportData.Test.Name}");
            var directory = Path.Combine(loadReportData.TestsResultsDirectory, "Data");
            _fileSystem.CreateDirectory(directory);
            var filePath = Path.Combine(directory, $"{reportName}.html");

            var htmlContent = GenerateHtmlReport(loadReportData);

            await _fileSystem.WriteAllTextAsync(filePath, htmlContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write HTML load test report.", ex);
        }
    }

    private string GenerateHtmlReport(LoadReportData loadReportData)
    {
        var b = new StringBuilder();

        b.AppendLine("<!DOCTYPE html>");
        b.AppendLine("<html lang='en'>");
        b.AppendLine("<head>");
        b.AppendLine("<meta charset='UTF-8'>");
        b.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        b.AppendLine("<title>TestFuzn - Load Test Report</title>");
        b.AppendLine(BrandHtml.GoogleFontsLinks);
        b.AppendLine("<link rel='stylesheet' href='Assets/styles/testfuzn.css'>");
        b.AppendLine("<script src='Assets/scripts/testfuzn-charts.js'></script>");
        b.AppendLine("</head>");
        b.AppendLine("<body>");
        b.AppendLine(@"<div class=""page-container"">");

        BrandHtml.WriteMasthead(b, "Assets/", @"<a class=""masthead-link"" href=""../TestReport.html"">&larr; Back to report</a>");

        BrandHtml.WritePageTitle(b, "load", loadReportData.Test.Name, "Load test report");

        WriteRunInfo(loadReportData, b);

        WriteOverallTestStatus(loadReportData, b);

        WriteWorkers(loadReportData, b);

        // Write each scenario
        for (int i = 0; i < loadReportData.ScenarioResults.Count; i++)
        {
            var scenarioResult = loadReportData.ScenarioResults[i];
            WriteScenarioSection(loadReportData, scenarioResult, b, i + 1);
        }

        WriteBackLink(b);

        BrandHtml.WriteProductSignature(b);

        b.AppendLine(@"</div>");
        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
    }

    private static void WriteBackLink(StringBuilder b)
    {
        b.AppendLine(@"<a class=""masthead-link"" href=""../TestReport.html"">&larr; Back to report</a>");
    }

    private void WriteRunInfo(LoadReportData loadReportData, StringBuilder b)
    {
        var configuration = _testExecutionState.TestSession.Configuration;
        BrandHtml.CardStart(b, "doc", "Test", string.IsNullOrEmpty(loadReportData.Test.Description) ? null : loadReportData.Test.Description);
        b.AppendLine(@"<div class=""details"">");
        BrandHtml.DetailItem(b, "layers", "Suite", loadReportData.Suite.Name);
        BrandHtml.DetailItem(b, "folder", "Group", loadReportData.Test.Group.Name);
        BrandHtml.DetailItem(b, "hash", "Run id", loadReportData.TestRunId);
        BrandHtml.DetailItem(b, "server", "Execution environment", configuration?.ExecutionEnvironment);
        BrandHtml.DetailItem(b, "server", "Target environment", configuration?.TargetEnvironment);
        BrandHtml.DetailItem(b, "clock", "Duration", loadReportData.Test.TestRunDuration().ToTestFuznReadableString());
        BrandHtml.DetailItem(b, "play", "Started", loadReportData.Test.StartTime().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        BrandHtml.DetailItem(b, "flag", "Ended", loadReportData.Test.EndTime().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        if (loadReportData.Test.Tags != null && loadReportData.Test.Tags.Count > 0)
            BrandHtml.DetailItem(b, "tag", "Tags", string.Join(", ", loadReportData.Test.Tags));
        b.AppendLine("</div>");

        var hasSuiteMetadata = loadReportData.Suite.Metadata != null && loadReportData.Suite.Metadata.Count > 0;
        var hasTestMetadata = loadReportData.Test.Metadata != null && loadReportData.Test.Metadata.Count > 0;
        if (hasSuiteMetadata || hasTestMetadata)
        {
            b.AppendLine(@"<details class=""run-info-metadata"">");
            b.AppendLine(@"<summary><span class=""t-show"">Show metadata</span><span class=""t-hide"">Hide metadata</span></summary>");
            b.AppendLine(@"<div class=""details"">");
            if (hasSuiteMetadata)
            {
                foreach (var metadata in loadReportData.Suite.Metadata!)
                    BrandHtml.DetailItem(b, "tag", metadata.Key, metadata.Value);
            }
            if (hasTestMetadata)
            {
                foreach (var metadata in loadReportData.Test.Metadata!)
                    BrandHtml.DetailItem(b, "tag", metadata.Key, metadata.Value);
            }
            b.AppendLine("</div>");
            b.AppendLine("</details>");
        }

        BrandHtml.CardEnd(b);
    }

    private static void WriteOverallTestStatus(LoadReportData loadReportData, StringBuilder b)
    {
        var scenariosTotal = loadReportData.ScenarioResults.Count;
        
        if (scenariosTotal == 0)
        {
            b.AppendLine(@"<div class=""overall-status no-tests"">");
            b.AppendLine($@"<div class=""icon"">{BrandHtml.InfoIconLarge()}</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">No Scenarios Executed</div>");
            b.AppendLine("</div></div>");
            return;
        }

        if (scenariosTotal == 1)
            return;

        var allPassed = loadReportData.ScenarioResults.All(s => s.Status == TestStatus.Passed);
        var failedCount = loadReportData.ScenarioResults.Count(s => s.Status == TestStatus.Failed);

        if (allPassed)
        {
            b.AppendLine(@"<div class=""overall-status passed"">");
            b.AppendLine($@"<div class=""icon"">{BrandHtml.TestStatusIconLarge(TestStatus.Passed)}</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">Test Passed</div>");
            b.AppendLine("</div></div>");
        }
        else
        {
            b.AppendLine(@"<div class=""overall-status failed"">");
            b.AppendLine($@"<div class=""icon"">{BrandHtml.TestStatusIconLarge(TestStatus.Failed)}</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine($@"<div class=""title"">{failedCount} Scenario{(failedCount > 1 ? "s" : "")} Failed</div>");
            b.AppendLine("</div></div>");
        }
    }

    // Cluster runs: one row per load worker, so an overloaded load machine is visible next to the results.
    private static void WriteWorkers(LoadReportData loadReportData, StringBuilder b)
    {
        if (loadReportData.Warnings.Count > 0)
        {
            b.AppendLine(@"<div class=""warnings""><ul>");
            foreach (var warning in loadReportData.Warnings)
                b.AppendLine($"<li>{E(warning)}</li>");
            b.AppendLine("</ul></div>");
        }

        if (loadReportData.Workers.Count == 0)
            return;

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        BrandHtml.CardStart(b, "users", "Workers", "One row per load machine, so an overloaded machine shows next to the results.");
        b.AppendLine(@"<div class=""table-scroll""><table>");
        b.AppendLine("<tr><th>Worker</th><th>State</th><th>OS</th><th>Iterations</th><th>Errors</th><th>CPU avg</th><th>CPU max</th><th>Memory max</th><th>Start lag max</th><th>Log folder</th></tr>");
        foreach (var worker in loadReportData.Workers)
        {
            var cpuMax = worker.CpuMaxPercent.ToString("0", inv) + " %";
            if (worker.HasCpuWarning)
                cpuMax = BrandHtml.Pill("warn", $"{cpuMax} (above {WorkerSummary.CpuWarningPercent.ToString("0", inv)} %)");

            b.AppendLine("<tr>");
            b.AppendLine($"<td>{E(worker.Id)}</td><td>{E(worker.State)}</td><td>{E(worker.Os)} ({worker.CpuCores} cores)</td>");
            b.AppendLine($"<td>{worker.Iterations}</td><td>{worker.Errors}</td>");
            b.AppendLine($"<td>{worker.CpuAveragePercent.ToString("0", inv)} %</td><td>{cpuMax}</td>");
            b.AppendLine($"<td>{worker.MemoryMaxMb.ToString("0", inv)} MB</td><td>{worker.IterationStartLagMaxMs.ToString("0", inv)} ms</td>");
            b.AppendLine($"<td>{E(worker.LogFolder)}</td>");
            b.AppendLine("</tr>");
        }
        b.AppendLine("</table></div>");
        BrandHtml.CardEnd(b);
    }


    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

    private const int MaxListedErrorsPerStep = 10;

    private enum VerdictKind { Good, Problem, Info }

    private record Verdict(VerdictKind Kind, string Html, string? DetailsHtml = null);

    private static string Ms(double ms)
    {
        if (ms >= 1000)
            return (ms / 1000).ToString("#,0.##", Inv) + " s";
        if (ms > 0 && ms < 0.01)
            return "<0.01 ms";
        return ms.ToString("0.##", Inv) + " ms";
    }

    private static string Ms(TimeSpan duration) => Ms(duration.TotalMilliseconds);

    private static string Count(long value) => value.ToString("N0", Inv);

    private static string Rate(double value) => value.ToString("#,0.#", Inv);

    private static string Percent(double value) => value.ToString("0.0", Inv) + "%";

    private static IReadOnlyList<LoadTimelinePoint> Timeline(LoadReportData loadReportData, ScenarioLoadResult scenario)
    {
        if (loadReportData.Timelines.TryGetValue(scenario.ScenarioName, out var timeline) && timeline != null)
            return timeline;
        return Array.Empty<LoadTimelinePoint>();
    }

    private void WriteScenarioSection(LoadReportData loadReportData, ScenarioLoadResult scenarioResult, StringBuilder b, int scenarioIndex)
    {
        var timeline = Timeline(loadReportData, scenarioResult);

        var title = loadReportData.ScenarioResults.Count == 1
            ? $"Scenario – {E(scenarioResult.ScenarioName)}"
            : $"Scenario {scenarioIndex} – {E(scenarioResult.ScenarioName)}";
        b.AppendLine($@"<h2 class=""lr-scenario-heading"">{title} {BrandHtml.StatusPill(scenarioResult.Status)}</h2>");

        WriteVerdicts(scenarioResult, timeline, b);
        WriteIterationGroups(scenarioResult, b);
        WriteTimelineCharts(timeline, b, scenarioIndex);
        WriteDistribution(loadReportData, scenarioResult, b);
        WriteErrors(scenarioResult, b);

        WriteSteps(b, scenarioResult);
        WriteStatusCodes(b, scenarioResult);

        WriteSetup(scenarioResult, b);
        WriteSnapshotTable(loadReportData, b, scenarioResult);
    }

    private static void WriteVerdicts(ScenarioLoadResult scenario, IReadOnlyList<LoadTimelinePoint> timeline, StringBuilder b)
    {
        var verdicts = new List<Verdict>
        {
            LoadVerdict(scenario, timeline),
            SpeedVerdict(scenario, timeline),
            FailuresVerdict(scenario)
        };
        verdicts.AddRange(AssertVerdicts(scenario));

        BrandHtml.CardStart(b, "check", "Summary");
        b.AppendLine(@"<div class=""lr-verdicts"">");
        foreach (var verdict in verdicts)
        {
            var icon = verdict.Kind switch
            {
                VerdictKind.Good => $@"<span class=""status-icon passed"" title=""Good"">{BrandHtml.CheckCircleSvg}</span>",
                VerdictKind.Problem => $@"<span class=""status-icon warning"" title=""Problem"">{BrandHtml.AlertTriangleSvg}</span>",
                _ => $@"<span class=""status-icon info"" title=""Info"">{BrandHtml.InfoCircleSvg}</span>"
            };
            b.AppendLine($@"<div class=""lr-verdict"">{icon}<div><div class=""lr-verdict-text"">{verdict.Html}</div>{verdict.DetailsHtml}</div></div>");
        }
        b.AppendLine("</div>");
        BrandHtml.CardEnd(b);
    }

    private const double MinRateSeconds = 1;

    private static double? TargetPerSecond(IReadOnlyList<LoadTimelinePoint> timeline)
    {
        var measured = timeline.Where(point => point.Seconds > 0).ToList();
        if (measured.Count == 0 || measured.Any(point => point.TargetPerSecond == null))
            return null;

        var seconds = measured.Sum(point => point.Seconds);
        if (seconds <= 0)
            return null;

        return measured.Sum(point => point.TargetPerSecond!.Value * point.Seconds) / seconds;
    }

    private static double SentPerSecond(ScenarioLoadResult scenario, IReadOnlyList<LoadTimelinePoint> timeline)
    {
        var measured = timeline.Where(point => point.Seconds > 0).ToList();
        var seconds = measured.Sum(point => point.Seconds);
        if (seconds > 0)
            return measured.Sum(point => point.OkCount + point.FailedCount) / seconds;

        var ok = scenario.Ok.Count;
        var total = ok + scenario.Failed.Count;
        return ok > 0 ? scenario.Ok.PerSecond * total / ok : scenario.Ok.PerSecond;
    }

    private static bool TooShortForRate(ScenarioLoadResult scenario)
        => scenario.MeasurementTotalDuration().TotalSeconds < MinRateSeconds;

    private static Verdict LoadVerdict(ScenarioLoadResult scenario, IReadOnlyList<LoadTimelinePoint> timeline)
    {
        if (TooShortForRate(scenario))
        {
            var total = scenario.Ok.Count + scenario.Failed.Count;
            return new Verdict(VerdictKind.Info,
                $"Load: {Count(total)} iterations in {E(Ms(scenario.MeasurementTotalDuration()))}. Too short to measure iterations per second.");
        }

        var actual = SentPerSecond(scenario, timeline);
        var target = TargetPerSecond(timeline);
        if (target == null || target.Value <= 0)
            return new Verdict(VerdictKind.Info, $"Load: {Rate(actual)} iterations per second.");

        var ratio = actual / target.Value;
        var text = $"{Rate(actual)} of {Rate(target.Value)} iterations per second ({Percent(ratio * 100)}).";
        return ratio >= 0.9
            ? new Verdict(VerdictKind.Good, "Load held: " + text)
            : new Verdict(VerdictKind.Problem, "Load not held: " + text + " The system or the load machine could not keep up.");
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.OrderBy(value => value).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static Verdict SpeedVerdict(ScenarioLoadResult scenario, IReadOnlyList<LoadTimelinePoint> timeline)
    {
        if (scenario.Ok.Count == 0)
            return new Verdict(VerdictKind.Info, "No ok iterations, so no durations.");

        var p95 = timeline.Where(point => point.P95Ms != null).Select(point => point.P95Ms!.Value).ToList();
        if (p95.Count >= 4)
        {
            var half = p95.Count / 2;
            var first = Median(p95.Take(half).ToList());
            var second = Median(p95.Skip(half).ToList());
            if (second >= 1.5 * first && second - first >= 1)
                return new Verdict(VerdictKind.Problem, $"Got slower during the run: P95 rose from {E(Ms(first))} to {E(Ms(second))}.");
        }

        return new Verdict(VerdictKind.Good,
            $"Steady speed: 95 of 100 iterations took under {E(Ms(scenario.Ok.DurationPercentile95))}. The slowest 1% took up to {E(Ms(scenario.Ok.DurationPercentile99))}.");
    }

    private static Verdict FailuresVerdict(ScenarioLoadResult scenario)
    {
        var failed = scenario.Failed.Count;
        var total = scenario.Ok.Count + failed;
        if (failed == 0)
            return new Verdict(VerdictKind.Good, "No failures.");

        return new Verdict(VerdictKind.Problem, $"Failures: {Count(failed)} of {Count(total)} iterations failed ({Percent(100.0 * failed / total)}).");
    }

    private static IEnumerable<Verdict> AssertVerdicts(ScenarioLoadResult scenario)
    {
        var asserts = new (string Phase, Exception? Exception)[]
        {
            ("AssertWhileWarmingUp", scenario.AssertWhileWarmingUpException),
            ("AssertWhileRunning", scenario.AssertWhileRunningException),
            ("AssertWhenDone", scenario.AssertWhenDoneException)
        };

        foreach (var (phase, exception) in asserts)
        {
            if (exception == null)
                continue;

            var text = $"{phase} failed: {E(exception.Message)}";
            if (phase == "AssertWhileRunning")
                text += " It stopped the test early.";

            yield return new Verdict(VerdictKind.Problem, text, ExceptionDetails(exception));
        }
    }

    private static string ExceptionDetails(Exception exception)
    {
        var b = new StringBuilder();
        b.Append(@"<details><summary>Show details</summary>");
        b.Append($@"<pre class=""failure-trace""><strong>{E(exception.GetType().FullName)}</strong>: {E(exception.Message)}");
        if (exception.StackTrace != null)
            b.Append("\n").Append(E(exception.StackTrace));
        if (exception.InnerException != null)
        {
            b.Append($"\n\nInner exception: <strong>{E(exception.InnerException.GetType().FullName)}</strong>: {E(exception.InnerException.Message)}");
            if (exception.InnerException.StackTrace != null)
                b.Append("\n").Append(E(exception.InnerException.StackTrace));
        }
        b.Append("</pre></details>");
        return b.ToString();
    }

    private record StatBox(string Label, string Value, string? Unit = null, string? Hint = null, string? Title = null);

    private static (string Value, string Unit) MsParts(double ms)
    {
        if (ms > 0 && ms < 0.01) return ("<0.01", "ms");
        if (ms < 1) return (ms.ToString("#,0.##", Inv), "ms");
        if (ms < 10) return (ms.ToString("#,0.#", Inv), "ms");
        if (ms < 1000) return (ms.ToString("#,0", Inv), "ms");
        if (ms < 10000) return ((ms / 1000).ToString("#,0.##", Inv), "s");
        if (ms < 60000) return ((ms / 1000).ToString("#,0.#", Inv), "s");
        var minutes = (long)Math.Floor(ms / 60000);
        var seconds = (long)Math.Round(ms % 60000 / 1000);
        return seconds == 60 ? ($"{minutes + 1}:00", "min") : ($"{minutes}:{seconds:00}", "min");
    }

    private static List<StatBox> BoxesOf(Stats stats, bool tooShort)
    {
        StatBox Ms(string label, TimeSpan value)
        {
            var (shown, unit) = MsParts(value.TotalMilliseconds);
            return new StatBox(label, shown, unit);
        }

        return
        [
            new StatBox("Iterations", Count(stats.Count)),
            tooShort
                ? new StatBox("Iterations/s", "—", Hint: "Run under a second", Title: "The run was under a second, too short to measure")
                : new StatBox("Iterations/s", Rate(stats.PerSecond), "/s"),
            Ms("Mean", stats.DurationMean),
            Ms("Median", stats.DurationMedian),
            Ms("P75", stats.DurationPercentile75),
            Ms("P90", stats.DurationPercentile90),
            Ms("P95", stats.DurationPercentile95),
            Ms("P99", stats.DurationPercentile99),
            Ms("Min", stats.DurationMin),
            Ms("Max", stats.DurationMax),
            Ms("Std dev", stats.DurationStandardDeviation)
        ];
    }

    private static void WriteIterationGroups(ScenarioLoadResult scenario, StringBuilder b)
    {
        var ok = scenario.Ok;
        var failed = scenario.Failed;
        var total = ok.Count + failed.Count;
        var tooShort = TooShortForRate(scenario);
        string Share(int n) => $"{Count(n)} · {(total > 0 ? Math.Round(100.0 * n / total) : 0).ToString(Inv)}%";

        WriteIterationGroup(b, "ok", "Ok iterations", BrandHtml.Pill("ok", Share(ok.Count)), BrandHtml.CheckCircleSvg,
            ok.Count > 0 ? BoxesOf(ok, tooShort) : null, "No ok iterations.");

        List<StatBox>? failedBoxes = null;
        if (failed.Count > 0)
        {
            failedBoxes = BoxesOf(failed, tooShort);
            failedBoxes[0] = failedBoxes[0] with { Hint = $"{Percent(100.0 * failed.Count / total)} of iterations" };
        }
        WriteIterationGroup(b, "bad", "Failed iterations", BrandHtml.Pill("bad", Share(failed.Count)), BrandHtml.XCircleSvg,
            failedBoxes, "No failed iterations.");
    }

    private static void WriteIterationGroup(StringBuilder b, string tone, string title, string pill, string markSvg, List<StatBox>? boxes, string none)
    {
        b.AppendLine($@"<section class=""card sgroup {(boxes == null ? "none" : tone)}"">");
        b.AppendLine(@"<div class=""card-head"">");
        b.AppendLine($@"<span class=""card-icon"">{BrandHtml.Icon("pulse", 20)}</span>");
        b.AppendLine($@"<div class=""card-title""><h3>{E(title)} {pill}</h3></div>");
        b.AppendLine($@"<span class=""sgmark {tone}"" aria-hidden=""true"">{markSvg}</span>");
        b.AppendLine("</div>");
        b.AppendLine(@"<div class=""card-body"">");
        if (boxes == null)
        {
            b.AppendLine($@"<p class=""snone"">{E(none)}</p>");
        }
        else
        {
            b.AppendLine(@"<div class=""sboxes"">");
            foreach (var box in boxes)
            {
                b.Append($@"<div class=""sbox""{(box.Title == null ? "" : $@" title=""{E(box.Title)}""")}><span class=""sk"">{E(box.Label)}</span>");
                b.Append($@"<span class=""sv"">{E(box.Value)}{(box.Unit == null ? "" : $@"<span class=""su"">{E(box.Unit)}</span>")}</span>");
                if (box.Hint != null)
                    b.Append($@"<span class=""sh"">{E(box.Hint)}</span>");
                b.AppendLine("</div>");
            }
            b.AppendLine("</div>");
        }
        BrandHtml.CardEnd(b);
    }

    private static readonly System.Text.Json.JsonSerializerOptions ChartJson = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static void WriteChart(StringBuilder b, object data)
    {
        b.AppendLine(@"<div class=""tf-chart"">");
        b.Append(@"<script type=""application/json"">").Append(System.Text.Json.JsonSerializer.Serialize(data, ChartJson)).AppendLine("</script>");
        b.AppendLine("</div>");
    }

    private static double?[][] Line(IReadOnlyList<LoadTimelinePoint> timeline, Func<LoadTimelinePoint, double?> value)
        => timeline.Select(point => new double?[] { point.ElapsedMs, Round(value(point)) }).ToArray();

    private static double? Round(double? value) => value.HasValue ? Math.Round(value.Value, 3) : null;

    private record ChartSeries(string Label, string Tone, double?[][] Points, bool? Dash = null, string? Weight = null);

    private static void WriteTimelineCharts(IReadOnlyList<LoadTimelinePoint> timeline, StringBuilder b, int scenarioIndex)
    {
        if (timeline.Count < 2)
        {
            b.AppendLine($@"<div class=""nocharts"">{BrandHtml.Icon("info", 16)}<span>The run was too short for charts. They need at least two intervals.</span></div>");
            return;
        }

        var sync = $"scenario{scenarioIndex}";
        var anyFailed = timeline.Any(point => point.FailedCount > 0);
        var hasTarget = timeline.Any(point => point.TargetPerSecond != null);

        var rate = new List<ChartSeries> { new("Ok", "ok", Line(timeline, point => point.OkPerSecond)) };
        if (anyFailed)
        {
            rate.Add(new ChartSeries("Failed", "bad", Line(timeline, point => point.FailedPerSecond)));
            rate.Add(new ChartSeries("Total", "accent", Line(timeline, point => Math.Round((point.OkPerSecond + point.FailedPerSecond) * 10) / 10)));
        }
        if (hasTarget)
            rate.Add(new ChartSeries("Target", "neu", Line(timeline, point => point.TargetPerSecond), Dash: true));

        b.AppendLine(@"<div class=""lr-charts"">");

        BrandHtml.CardStart(b, "pulse", "Iterations per second",
            hasTarget
                ? anyFailed
                    ? "Total should follow the dashed line. A drop means the system could not keep up."
                    : "Should follow the dashed line. A drop means the system could not keep up."
                : "Ok and failed iterations finished per second.");
        WriteChart(b, new { type = "line", title = "Iterations per second", unit = "per second", height = 200, sync, series = rate });
        BrandHtml.CardEnd(b);

        BrandHtml.CardStart(b, "clock", "Duration", "P95: 95 of 100 iterations were faster than this line. It should be flat. A climb means the system slowed down.");
        WriteChart(b, new
        {
            type = "line", title = "Duration", unit = "ms", height = 200, sync,
            series = new[]
            {
                new ChartSeries("P95", "accent", Line(timeline, point => point.P95Ms), Weight: "strong"),
                new ChartSeries("Mean", "neu", Line(timeline, point => point.MeanMs), Dash: true, Weight: "muted"),
                new ChartSeries("P50", "neu", Line(timeline, point => point.P50Ms), Weight: "muted"),
                new ChartSeries("P90", "neu", Line(timeline, point => point.P90Ms), Weight: "muted"),
                new ChartSeries("P99", "neu", Line(timeline, point => point.P99Ms), Weight: "muted")
            }
        });
        BrandHtml.CardEnd(b);

        if (timeline.Any(point => point.VirtualUsers != null))
        {
            BrandHtml.CardStart(b, "users", "Virtual users", "Iterations running at the same time, on average. A climb means the system slowed down.");
            WriteChart(b, new
            {
                type = "line", title = "Virtual users", unit = "users", height = 200, sync, legend = true,
                series = new[] { new ChartSeries("Virtual users", "accent", Line(timeline, point => point.VirtualUsers)) }
            });
            BrandHtml.CardEnd(b);
        }

        var loadPoints = timeline
            .Where(point => point.P95Ms != null && point.OkPerSecond + point.FailedPerSecond > 0)
            .Select(point => new double[] { Math.Round(point.OkPerSecond + point.FailedPerSecond, 3), Math.Round(point.P95Ms!.Value, 3), point.ElapsedMs })
            .ToArray();
        if (LoadSpread(loadPoints))
        {
            BrandHtml.CardStart(b, "clock", "Response time against load", "Each dot is one moment of the run. Dots that climb to the right mean the system slows down as the load grows.");
            WriteChart(b, new { type = "points", title = "P95 against iterations per second", xTitle = "Iterations per second", height = 220, points = loadPoints });
            BrandHtml.CardEnd(b);
        }

        b.AppendLine("</div>");
    }

    private static bool LoadSpread(double[][] points)
    {
        if (points.Length < 6)
            return false;
        var top = points.Max(point => point[0]);
        return top > 0 && (top - points.Min(point => point[0])) / top >= 0.25;
    }

    private static void WriteDistribution(LoadReportData loadReportData, ScenarioLoadResult scenario, StringBuilder b)
    {
        if (!loadReportData.Distributions.TryGetValue(scenario.ScenarioName, out var buckets) || buckets == null || buckets.Count == 0)
            return;

        var markers = scenario.Ok.Count > 0
            ? new[] { new { ms = Math.Round(scenario.Ok.DurationPercentile95.TotalMilliseconds, 3), label = "P95" } }
            : [];

        BrandHtml.CardStart(b, "groups", "Duration distribution", "How many iterations took how long. A second hump or a long tail to the right means some iterations are much slower than the rest.");
        WriteChart(b, new
        {
            type = "bars", height = 220, markers,
            buckets = buckets.Select(bucket => new double[] { Math.Round(bucket.FromMs, 3), Math.Round(bucket.ToMs, 3), bucket.OkCount, bucket.FailedCount }).ToArray()
        });
        BrandHtml.CardEnd(b);
    }

    private static void CollectErrors(IEnumerable<StepLoadResult>? steps, string? parentPath, List<(string Step, ErrorEntry Error)> errors)
    {
        if (steps == null)
            return;

        foreach (var step in steps)
        {
            var path = parentPath == null ? step.Name : $"{parentPath} → {step.Name}";
            if (step.Errors != null)
            {
                foreach (var error in step.Errors.Values)
                    errors.Add((path, error));
            }
            CollectErrors(step.Steps, path, errors);
        }
    }

    private static void WriteErrors(ScenarioLoadResult scenario, StringBuilder b)
    {
        var errors = new List<(string Step, ErrorEntry Error)>();
        CollectErrors(scenario.Hooks, null, errors);
        CollectErrors(scenario.Steps.Values, null, errors);
        if (errors.Count == 0)
            return;

        var failed = scenario.Failed.Count;

        BrandHtml.CardStart(b, "alert", "Errors", "Failed iterations, grouped by message.", null, "errors-card");
        b.AppendLine(@"<div class=""table-scroll""><table>");
        b.AppendLine(@"<thead><tr><th>Error</th><th>Step</th><th class=""lr-num"">Count</th><th class=""lr-num"" title=""Share of failed iterations"">Share</th></tr></thead>");
        b.AppendLine("<tbody>");
        foreach (var (step, error) in errors.OrderByDescending(entry => entry.Error.Count))
        {
            b.AppendLine("<tr>");
            b.Append("<td>").Append(E(error.Message));
            if (!string.IsNullOrWhiteSpace(error.Details) && error.Details != error.Message)
                b.Append($@"<details class=""lr-error-details""><summary>Show details</summary><pre>{E(error.Details)}</pre></details>");
            b.AppendLine("</td>");
            b.AppendLine($"<td>{E(step)}</td>");
            b.AppendLine($@"<td class=""lr-num"">{Count(error.Count)}</td>");
            b.AppendLine($@"<td class=""lr-num"">{(failed > 0 ? Percent(100.0 * error.Count / failed) : "–")}</td>");
            b.AppendLine("</tr>");
        }
        b.AppendLine("</tbody>");
        b.AppendLine("</table></div>");

        if (errors.GroupBy(entry => entry.Step).Any(group => group.Count() >= MaxListedErrorsPerStep))
            b.AppendLine($@"<p class=""lr-note"">Each step lists at most {MaxListedErrorsPerStep} different errors. Failures with other errors are counted, but not listed.</p>");
        BrandHtml.CardEnd(b);
    }

    private static void WriteSetup(ScenarioLoadResult scenario, StringBuilder b)
    {
        BrandHtml.CardStart(b, "settings", "Setup", "How the load was produced, and how long each phase took.");

        if (!string.IsNullOrEmpty(scenario.Description))
            b.AppendLine($@"<div class=""lr-setup-row"">{E(scenario.Description)}</div>");

        if (scenario.Simulations != null && scenario.Simulations.Count > 0)
        {
            b.AppendLine(@"<div class=""lr-setup-row""><div class=""info-item""><span class=""detail-label"">Simulations</span><div class=""lr-chips"">");
            foreach (var simulation in scenario.Simulations)
                b.AppendLine($@"<span class=""lr-chip"">{E(simulation)}</span>");
            b.AppendLine("</div></div></div>");
        }

        b.AppendLine(@"<div class=""lr-scroll""><table class=""lr-steps"">");
        b.AppendLine(@"<thead><tr><th>Phase</th><th>Started</th><th>Ended</th><th>Duration</th></tr></thead><tbody>");
        WritePhaseRow(b, "Init", scenario.InitStartTime, scenario.InitEndTime, scenario.InitTotalDuration());
        if (scenario.HasWarmupStep())
            WritePhaseRow(b, "Warmup", scenario.WarmupStartTime, scenario.WarmupEndTime, scenario.WarmupTotalDuration());
        WritePhaseRow(b, "Execution", scenario.MeasurementStartTime, scenario.MeasurementEndTime, scenario.MeasurementTotalDuration());
        WritePhaseRow(b, "Cleanup", scenario.CleanupStartTime, scenario.CleanupEndTime, scenario.CleanupTotalDuration());
        WritePhaseRow(b, "Total", scenario.StartTime(), scenario.EndTime(), scenario.TestRunTotalDuration());
        b.AppendLine("</tbody></table></div>");

        BrandHtml.CardEnd(b);
    }

    private static void WritePhaseRow(StringBuilder b, string phase, DateTime start, DateTime end, TimeSpan duration)
    {
        b.AppendLine($@"<tr><td>{E(phase)}</td><td>{Stamp(start)}</td><td>{Stamp(end)}</td><td>{E(duration.ToTestFuznReadableString())}</td></tr>");
    }

    private static string Stamp(DateTime time)
    {
        return time == default ? "—" : time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void WriteStatsHeader(StringBuilder b)
    {
        b.AppendLine("<th>Step</th>");
        b.AppendLine("<th>Type</th>");
        b.AppendLine(@"<th class=""lr-num"">Count</th>");
        b.AppendLine(@"<th class=""lr-num"">Per second</th>");
        b.AppendLine(@"<th class=""lr-num"">Mean</th>");
        b.AppendLine(@"<th class=""lr-num"">Median</th>");
        b.AppendLine(@"<th class=""lr-num"">P75</th>");
        b.AppendLine(@"<th class=""lr-num"">P90</th>");
        b.AppendLine(@"<th class=""lr-num"">P95</th>");
        b.AppendLine(@"<th class=""lr-num"">P99</th>");
        b.AppendLine(@"<th class=""lr-num"">Min</th>");
        b.AppendLine(@"<th class=""lr-num"">Max</th>");
        b.AppendLine(@"<th class=""lr-num"">Std dev</th>");
    }

    private void WriteSteps(StringBuilder b, ScenarioLoadResult scenario)
    {
        BrandHtml.CardStart(b, "list", "Step performance", "Durations per step. Failed rows show the failed iterations of that step.");
        b.AppendLine(@"<div class=""lr-scroll""><table class=""lr-steps"">");
        b.AppendLine("<thead>");
        b.AppendLine("<tr>");
        WriteStatsHeader(b);
        b.AppendLine("</tr>");
        b.AppendLine("</thead>");
        b.AppendLine("<tbody>");

        b.AppendLine(@"<tr class=""summary"">");
        Cols(b, "All steps", true, scenario.Ok, scenario.Failed, 1);
        b.AppendLine("</tr>");

        if (scenario.Failed.Count > 0)
        {
            b.AppendLine(@"<tr class=""summary"">");
            Cols(b, "", false, scenario.Ok, scenario.Failed, 1);
            b.AppendLine("</tr>");
        }

        foreach (var step in scenario.Steps)
        {
            WriteStepRow(step.Value, 1, b);
        }

        b.AppendLine("</tbody>");
        b.AppendLine("</table></div>");
        BrandHtml.CardEnd(b);
    }

    private static void WriteStatusCodes(StringBuilder b, ScenarioLoadResult scenario)
    {
        var rows = new List<(string Step, Dictionary<int, long> Codes)>();
        CollectStatusCodes(scenario.Steps.Values, null, rows);
        if (rows.Count == 0)
            return;

        BrandHtml.CardStart(b, "code", "Status codes", "HTTP responses per step.");
        b.AppendLine(@"<div class=""lr-scroll""><table>");
        b.AppendLine("<thead><tr><th>Step</th><th>HTTP responses</th></tr></thead><tbody>");
        foreach (var row in rows)
        {
            var codes = row.Codes.OrderBy(code => code.Key)
                .Select(code => BrandHtml.Pill(code.Key >= 400 ? "bad" : code.Key >= 300 ? "neu" : "ok", $"{code.Key} · {code.Value:N0}"));
            b.AppendLine($@"<tr><td>{E(row.Step)}</td><td><span class=""tags"">{string.Join("", codes)}</span></td></tr>");
        }
        b.AppendLine("</tbody></table></div>");
        BrandHtml.CardEnd(b);
    }

    private static void CollectStatusCodes(IEnumerable<StepLoadResult>? steps, string? parentPath, List<(string Step, Dictionary<int, long> Codes)> rows)
    {
        if (steps == null)
            return;

        foreach (var step in steps)
        {
            var path = parentPath == null ? step.Name : $"{parentPath} → {step.Name}";
            if (step.StatusCodes != null && step.StatusCodes.Count > 0)
                rows.Add((path, step.StatusCodes));
            CollectStatusCodes(step.Steps, path, rows);
        }
    }

    private void WriteStepRow(StepLoadResult step, int level, StringBuilder b)
    {
        b.AppendLine(@"<tr>");
        Cols(b, step.Name, true, step.Ok, step.Failed, level);
        b.AppendLine("</tr>");

        if (step.Failed.Count > 0)
        {
            b.AppendLine(@"<tr>");
            Cols(b, "", false, step.Ok, step.Failed, level);
            b.AppendLine("</tr>");
        }

        if (step.Steps == null || step.Steps.Count == 0)
            return;

        foreach (var innerStep in step.Steps)
            WriteStepRow(innerStep, level + 1, b);
    }

    private void WriteSnapshotTable(LoadReportData loadReportData, StringBuilder b, ScenarioLoadResult scenario)
    {
        if (!loadReportData.Snapshots.TryGetValue(scenario.ScenarioName, out var snapshots) || snapshots == null || snapshots.Count == 0)
            return;

        b.AppendLine(@"<details class=""lr-snapshots"">");
        b.AppendLine("<summary>Snapshot details</summary>");
        b.AppendLine(@"<p class=""lr-note"">Totals from the start of the measurement up to each snapshot. The charts above show each interval on its own.</p>");

        b.AppendLine("<table>");
        b.AppendLine("<thead>");
        b.AppendLine("<tr>");
        b.AppendLine(@"<th style=""width:1%;white-space:nowrap;"">Created</th>");
        WriteStatsHeader(b);
        b.AppendLine("</tr>");
        b.AppendLine("</thead>");
        b.AppendLine("<tbody>");

        foreach (var snapshot in snapshots)
        {
            b.AppendLine(@"<tr class=""summary"">");
            b.AppendLine(@$"<td style=""width:1%;white-space:nowrap;"">{snapshot.Created:yyyy-MM-dd HH:mm:ss.fff}</td>");
            Cols(b, "All steps", true, snapshot.Ok, snapshot.Failed, 1);
            b.AppendLine("</tr>");

            if (snapshot.Failed.Count > 0)
            {
                b.AppendLine(@"<tr class=""summary"">");
                b.AppendLine($"<td></td>");
                Cols(b, "", false, snapshot.Ok, snapshot.Failed, 1);
                b.AppendLine("</tr>");
            }

            foreach (var step in snapshot.Steps)
            {
                RenderSnapshotStep(b, step.Value);
            }
        }

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");
        b.AppendLine("</details>");
    }

    private void RenderSnapshotStep(StringBuilder b, StepLoadResult stepResult)
    {
        b.AppendLine(@"<tr>");
        b.AppendLine($"<td></td>");
        Cols(b, stepResult.Name, true, stepResult.Ok, stepResult.Failed, 1);
        b.AppendLine("</tr>");

        if (stepResult.Failed.Count > 0)
        {
            b.AppendLine(@"<tr>");
            b.AppendLine($"<td></td>");
            Cols(b, "", false, stepResult.Ok, stepResult.Failed, 1);
            b.AppendLine("</tr>");
        }

        if (stepResult.Steps == null || stepResult.Steps.Count == 0)
            return;

        foreach (var innerStep in stepResult.Steps)
        {
            RenderSnapshotStep(b, innerStep);
        }
    }

    private static void Cols(StringBuilder b, string stepName, bool isOkRow, Stats okStats, Stats failedStats, int level)
    {
        Stats stats = isOkRow ? okStats : failedStats;

        var padding = level > 1 ? $" style='padding-left:{(level - 1) * 20}px;'" : "";
        var prefix = "";
        if (level > 1 && !string.IsNullOrEmpty(stepName))
            prefix = "→ ";
        if (isOkRow)
        {
            prefix += failedStats.Count == 0
                ? BrandHtml.TestStatusIcon(TestStatus.Passed)
                : BrandHtml.TestStatusIcon(TestStatus.Failed);
        }

        var cssClass = isOkRow ? "ok" : "failed";
        var hasAny = stats.Count > 0;
        string Time(TimeSpan value) => hasAny ? E(Ms(value)) : "–";

        b.AppendLine($"<td{padding}>{prefix} {E(stepName)}</td>");
        b.AppendLine(@$"<td>{(isOkRow ? BrandHtml.Pill("ok", "Ok") : BrandHtml.Pill("bad", "Failed"))}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Count(stats.Count)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Count(stats.PerSecond)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Time(stats.DurationMean)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Time(stats.DurationMedian)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Time(stats.DurationPercentile75)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Time(stats.DurationPercentile90)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Time(stats.DurationPercentile95)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Time(stats.DurationPercentile99)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Time(stats.DurationMin)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Time(stats.DurationMax)}</td>");
        b.AppendLine(@$"<td class=""{cssClass} lr-num"">{Time(stats.DurationStandardDeviation)}</td>");
    }
}
