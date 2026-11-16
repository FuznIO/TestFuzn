using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Utils;
using System.Net;
using System.Text;

namespace Fuzn.TestFuzn.Internals.Reports.Standard;

internal class StandardHtmlReportWriter : IStandardReport
{
    private readonly IFileSystem _fileSystem;
    private readonly TestSession _testSession;

    public StandardHtmlReportWriter(IFileSystem fileSystem,
        TestSession testSession)
    {
        _fileSystem = fileSystem;
        _testSession = testSession;
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

    public async Task WriteReport(StandardReportData reportData)
    {
        try
        {
            var filePath = Path.Combine(reportData.TestsResultsDirectory, "TestReport.html");

            var htmlContent = GenerateHtmlReport(reportData);

            await _fileSystem.WriteAllTextAsync(filePath, htmlContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write HTML report.", ex);
        }
    }

    private string GenerateHtmlReport(StandardReportData reportData)
    {
        var b = new StringBuilder();

        var testsTotal = reportData.GroupResults.Sum(f => f.Value.TestResults.Count);
        var testsPassed = reportData.GroupResults.Sum(f => f.Value.TestResults.Count(s => s.Value.Status == TestStatus.Passed));
        var testsSkipped = reportData.GroupResults.Sum(f => f.Value.TestResults.Count(s => s.Value.Status == TestStatus.Skipped));
        var testsFailed = reportData.GroupResults.Sum(f => f.Value.TestResults.Count(s => s.Value.Status == TestStatus.Failed));
        var testsExecuted = testsPassed + testsFailed + testsSkipped;
        var passRate = testsExecuted > 0 ? Math.Round((double) (testsPassed + testsSkipped) / testsExecuted * 100, 1) : 0;

        b.AppendLine("<!DOCTYPE html>");
        b.AppendLine("<html lang='en'>");
        b.AppendLine("<head>");
        b.AppendLine("<meta charset='UTF-8'>");
        b.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        b.AppendLine("<title>TestFuzn - Test Report</title>");
        b.AppendLine(BrandHtml.GoogleFontsLinks);
        b.AppendLine("<link rel='stylesheet' href='Data/Assets/styles/testfuzn.css'>");
        b.AppendLine("</head>");
        b.AppendLine("<body>");
        BrandHtml.WriteMasthead(b, "Data/Assets/", @"<a class=""masthead-link"" href=""TestFuzn_Log.log"" target=""_blank"">View log ↗</a>");
        b.AppendLine(@"<div class=""page-container"">");

        BrandHtml.WritePageTitle(b, "tests", reportData.Suite.Name, "Test report");

        WriteDashboard(b, testsTotal, testsPassed, testsFailed, testsSkipped, passRate);

        b.AppendLine(@"<div class=""overview"">");
        WriteResultDonut(b, testsPassed, testsFailed, testsSkipped);
        WriteRunInfo(reportData, b);
        b.AppendLine("</div>");

        WriteGroupResults(reportData, b);

        BrandHtml.WriteProductSignature(b);

        b.AppendLine("</div>");
        WritePane(b);
        WriteScript(b);
        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
    }

    private static void WriteDashboard(StringBuilder b,
        int testsTotal, int testsPassed, int testsFailed, int testsSkipped, double passRate)
    {
        if (testsTotal == 0)
        {
            b.AppendLine(@"<div class=""overall-status no-tests"">");
            b.AppendLine($@"<div class=""icon"">{BrandHtml.InfoIconLarge()}</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">No tests executed</div>");
            b.AppendLine("</div></div>");
            return;
        }

        if (testsFailed == 0)
        {
            b.AppendLine(@"<div class=""overall-status passed"">");
            b.AppendLine($@"<div class=""icon"">{BrandHtml.TestStatusIconLarge(TestStatus.Passed)}</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">All tests passed</div>");
            b.AppendLine("</div></div>");
        }
        else
        {
            b.AppendLine(@"<div class=""overall-status failed"">");
            b.AppendLine($@"<div class=""icon"">{BrandHtml.TestStatusIconLarge(TestStatus.Failed)}</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine($@"<div class=""title"">{testsFailed} test{(testsFailed > 1 ? "s" : "")} failed</div>");
            b.AppendLine("</div></div>");
        }

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string Share(int n) => $"{(testsTotal > 0 ? Math.Round((double)n / testsTotal * 100, 1) : 0).ToString(inv)}%";

        b.AppendLine(@"<div class=""tiles"">");
        BrandHtml.Tile(b, "info", "doc", "Total tests", testsTotal.ToString("N0", inv));
        BrandHtml.Tile(b, "ok", "check", "Passed", testsPassed.ToString("N0", inv), Share(testsPassed));
        BrandHtml.Tile(b, "bad", "x", "Failed", testsFailed.ToString("N0", inv), Share(testsFailed));
        BrandHtml.Tile(b, "neu", "circle", "Skipped", testsSkipped.ToString("N0", inv), Share(testsSkipped));
        BrandHtml.Tile(b, "info", "percent", "Pass rate", $"{passRate.ToString(inv)}%");
        b.AppendLine("</div>");
    }

    private static void WriteResultDonut(StringBuilder b, int passed, int failed, int skipped)
    {
        var total = passed + failed + skipped;
        if (total == 0)
            return;

        const double size = 132;
        const double stroke = 18;
        const double radius = (size - stroke) / 2;
        var circumference = 2 * Math.PI * radius;
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        var slices = new (string Label, int Count, string Tone)[]
        {
            ("Passed", passed, "ok"),
            ("Failed", failed, "bad"),
            ("Skipped", skipped, "neu")
        };
        var shown = slices.Where(slice => slice.Count > 0).ToList();
        var gap = shown.Count > 1 ? 2.0 : 0.0;

        string Pct(int n)
        {
            var p = 100.0 * n / total;
            if (p > 0 && p < 1) return "&lt;1%";
            if (p > 99 && p < 100) return "&gt;99%";
            return $"{Math.Round(p).ToString(inv)}%";
        }

        BrandHtml.CardStart(b, "check", "Test results", "The result of every test in this run.");
        b.AppendLine(@"<div class=""donut"">");
        b.AppendLine(@"<div class=""ring"">");
        b.AppendLine($@"<svg width=""{size}"" height=""{size}"" viewBox=""0 0 {size} {size}"" role=""img"" aria-label=""{passed} passed, {failed} failed, {skipped} skipped"">");
        b.AppendLine($@"<g transform=""rotate(-90 {size / 2} {size / 2})"">");
        var offset = 0.0;
        foreach (var slice in shown)
        {
            var length = slice.Count / (double)total * circumference;
            var dash = Math.Max(0.5, length - gap);
            b.AppendLine($@"<circle class=""seg {slice.Tone}"" cx=""{size / 2}"" cy=""{size / 2}"" r=""{radius.ToString("0.###", inv)}"" stroke-width=""{stroke}"" stroke-dasharray=""{dash.ToString("0.###", inv)} {circumference.ToString("0.###", inv)}"" stroke-dashoffset=""{(-offset).ToString("0.###", inv)}""><title>{slice.Label}: {slice.Count} ({Pct(slice.Count)})</title></circle>");
            offset += length;
        }
        b.AppendLine("</g>");
        b.AppendLine("</svg>");
        b.AppendLine($@"<span class=""donut-center""><b>{total.ToString("N0", inv)}</b><span>tests</span></span>");
        b.AppendLine("</div>");
        b.AppendLine(@"<ul class=""legend"">");
        foreach (var slice in slices)
            b.AppendLine($@"<li><span class=""mark {slice.Tone}""></span><span class=""legend-label"">{slice.Label}</span><span class=""legend-count"">{slice.Count.ToString("N0", inv)}</span><span class=""legend-pct"">{Pct(slice.Count)}</span></li>");
        b.AppendLine("</ul>");
        b.AppendLine("</div>");
        BrandHtml.CardEnd(b);
    }

    private void WriteRunInfo(StandardReportData reportData, StringBuilder b)
    {
        BrandHtml.CardStart(b, "play", "Run", "Where and when the tests ran.");
        b.AppendLine(@"<div class=""details"">");
        BrandHtml.DetailItem(b, "hash", "Run id", reportData.TestRunId);
        BrandHtml.DetailItem(b, "server", "Execution environment", _testSession.Configuration?.ExecutionEnvironment);
        BrandHtml.DetailItem(b, "server", "Target environment", _testSession.Configuration?.TargetEnvironment);
        BrandHtml.DetailItem(b, "clock", "Duration", reportData.TestRunDuration.ToTestFuznReadableString());
        BrandHtml.DetailItem(b, "play", "Started", reportData.TestRunStartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        BrandHtml.DetailItem(b, "flag", "Ended", reportData.TestRunEndTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        b.AppendLine("</div>");

        if (reportData.Suite.Metadata != null && reportData.Suite.Metadata.Count > 0)
        {
            b.AppendLine(@"<details class=""run-info-metadata"">");
            b.AppendLine(@"<summary><span class=""t-show"">Show metadata</span><span class=""t-hide"">Hide metadata</span></summary>");
            b.AppendLine(@"<div class=""details"">");
            foreach (var metadata in reportData.Suite.Metadata)
                BrandHtml.DetailItem(b, "tag", metadata.Key, metadata.Value);
            b.AppendLine("</div>");
            b.AppendLine("</details>");
        }

        BrandHtml.CardEnd(b);
    }

    private static void WriteFilterBar(StandardReportData reportData, StringBuilder b)
    {
        var total = reportData.GroupResults.Sum(g => g.Value.TestResults.Count);
        var passed = reportData.GroupResults.Sum(g => g.Value.TestResults.Count(t => t.Value.Status == TestStatus.Passed));
        var failed = reportData.GroupResults.Sum(g => g.Value.TestResults.Count(t => t.Value.Status == TestStatus.Failed));
        var skipped = reportData.GroupResults.Sum(g => g.Value.TestResults.Count(t => t.Value.Status == TestStatus.Skipped));

        b.AppendLine(@"<div class=""results-filter"">");
        b.AppendLine(@"<div class=""status-pills"">");
        b.AppendLine($@"<button type=""button"" class=""pill active"" data-status-filter=""all"">All <span class=""count"">{total}</span></button>");
        b.AppendLine($@"<button type=""button"" class=""pill"" data-status-filter=""passed"">{BrandHtml.StatusMark("Passed", 15)}Passed <span class=""count"">{passed}</span></button>");
        b.AppendLine($@"<button type=""button"" class=""pill"" data-status-filter=""failed"">{BrandHtml.StatusMark("Failed", 15)}Failed <span class=""count"">{failed}</span></button>");
        b.AppendLine($@"<button type=""button"" class=""pill"" data-status-filter=""skipped"">{BrandHtml.StatusMark("Skipped", 15)}Skipped <span class=""count"">{skipped}</span></button>");
        b.AppendLine("</div>");

        var tests = reportData.GroupResults.SelectMany(g => g.Value.TestResults.Values).ToList();
        var standard = tests.Count(t => t.TestType != TestType.Load);
        var load = tests.Count(t => t.TestType == TestType.Load);
        b.AppendLine(@"<div class=""type-toggle"" role=""group"" aria-label=""Test type"">");
        b.AppendLine(@"<button type=""button"" class=""active"" data-type-filter=""all"" aria-pressed=""true"">All types</button>");
        b.AppendLine($@"<button type=""button"" data-type-filter=""standard"" aria-pressed=""false"">Standard tests <span class=""count"">{standard}</span></button>");
        b.AppendLine($@"<button type=""button"" data-type-filter=""load"" aria-pressed=""false"">Load tests <span class=""count"">{load}</span></button>");
        b.AppendLine("</div>");
        b.AppendLine(@"<input type=""search"" id=""test-name-filter"" placeholder=""Filter by name"" />");
        b.AppendLine("</div>");
    }

    private void WriteGroupResults(StandardReportData reportData, StringBuilder b)
    {
        var total = reportData.GroupResults.Sum(g => g.Value.TestResults.Count);
        BrandHtml.CardStart(b, "tests", "Tests", "Click a test to see its steps.", $@"<span class=""card-count"">{total.ToString("N0", Inv)} tests</span>", "results-card");
        WriteFilterBar(reportData, b);
        b.Append(@"<div class=""table-scroll""><table class=""group-results"">");
        b.AppendLine("<thead><tr>");
        b.AppendLine("<th>Test</th>");
        b.AppendLine("<th>Type</th>");
        b.AppendLine("<th>Status</th>");
        b.AppendLine(@"<th class=""num"">Duration</th>");
        b.AppendLine("<th>Tags</th>");
        b.AppendLine("</tr></thead><tbody>");

        var panes = new StringBuilder();
        var index = 0;
        foreach (var groupResult in reportData.GroupResults.OrderBy(f => f.Value.Name))
        {
            var group = groupResult.Value;
            var groupTotal = group.TestResults.Count;
            var groupPassed = group.TestResults.Count(t => t.Value.Status == TestStatus.Passed);
            var groupFailed = group.TestResults.Count(t => t.Value.Status == TestStatus.Failed);
            var groupSkipped = group.TestResults.Count(t => t.Value.Status == TestStatus.Skipped);

            var groupNameAttr = E(group.Name.ToLowerInvariant());
            b.AppendLine(@$"<tr class=""group"" data-group-row data-group-name=""{groupNameAttr}"">");
            b.Append($@"<td colspan=""5""><span class=""group-name"">{E(group.Name)}</span>");
            b.Append($@"<span class=""group-summary"">{groupTotal} test{(groupTotal == 1 ? "" : "s")} · {groupPassed} passed");
            if (groupFailed > 0)
                b.Append($@" · <span class=""bad-text"">{groupFailed} failed</span>");
            else
                b.Append(" · 0 failed");
            b.AppendLine($" · {groupSkipped} skipped</span></td>");
            b.AppendLine("</tr>");

            foreach (var testResult in group.TestResults.OrderBy(t => t.Value.Name))
            {
                index++;
                var paneId = $"test-{index}";
                var reportLink = LoadReportLink(group, testResult.Value);
                WriteTestRow(b, group, testResult.Value, paneId);
                WriteTestPane(panes, reportData, group, testResult.Value, paneId, reportLink);
            }
        }

        b.Append("</tbody></table></div>");
        BrandHtml.CardEnd(b);
        b.Append(panes);
    }

    private static string? LoadReportLink(GroupResult group, TestResult sr)
    {
        if (sr.TestType != TestType.Load || sr.Status == TestStatus.Skipped)
            return null;
        var reportName = FileNameHelper.MakeFilenameSafe($"{group.Name}-{sr.Name}");
        return $"Data/{E(reportName)}.html";
    }

    private static string StatusText(TestStatus status) =>
        status switch
        {
            TestStatus.Passed => "Passed",
            TestStatus.Failed => "Failed",
            TestStatus.Skipped => "Skipped",
            _ => ""
        };

    private static string StatusText(StepStatus status) =>
        status switch
        {
            StepStatus.Passed => "Passed",
            StepStatus.Failed => "Failed",
            StepStatus.Skipped => "Skipped",
            _ => ""
        };

    private static void WriteTestRow(StringBuilder b, GroupResult group, TestResult sr, string paneId)
    {
        var status = StatusText(sr.Status);
        var typeText = sr.Status == TestStatus.Skipped
            ? "N/A"
            : sr.TestType == TestType.Load ? "Load" : "Standard";

        b.AppendLine($@"<tr data-test-row data-pane=""{paneId}"" tabindex=""0"" data-type=""{(sr.TestType == TestType.Load ? "load" : "standard")}"" data-status=""{status.ToLowerInvariant()}""data-test-name=""{E(sr.Name.ToLowerInvariant())}"" data-group-name=""{E(group.Name.ToLowerInvariant())}"">");
        b.AppendLine($@"<td><span class=""test-name"">{BrandHtml.StatusMark(status)}<span class=""name-link"">{E(sr.Name)}</span></span></td>");
        b.AppendLine($@"<td><span class=""type-pill"">{E(typeText)}</span></td>");
        b.AppendLine($@"<td>{BrandHtml.StatusPill(sr.Status)}</td>");
        b.AppendLine($@"<td class=""num"">{(sr.Status == TestStatus.Skipped ? "" : DurationCell(sr))}</td>");
        b.AppendLine(@"<td><span class=""tags"">");
        if (sr.Tags != null && sr.Tags.Count > 0)
        {
            foreach (var tag in sr.Tags)
                b.Append($@"<span class=""tag"">{E(tag)}</span>");
        }
        b.AppendLine("</span></td>");
        b.AppendLine("</tr>");
    }

    private static string DurationCell(TestResult sr)
    {
        var total = sr.TestRunDuration().ToTestFuznReadableString();

        if (sr.TestType == TestType.Load || sr.TotalCount <= 1)
            return total;

        var counts = $"{sr.TotalCount} iterations";

        if (sr.FailedCount > 0)
            counts += $", {sr.FailedCount} failed";

        var average = TimeSpan.FromTicks((long)sr.IterationResults.Average(i => i.Duration().Ticks));

        return $@"{total}<span class=""duration-detail"">{counts} &middot; {average.ToTestFuznDurationHtml()} avg</span>";
    }

    private static string Time(DateTime value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", Inv);

    private static bool HasTimes(TestResult sr) => sr.Status != TestStatus.Skipped && sr.InitStartTime != default;

    private void WriteTestPane(StringBuilder b, StandardReportData reportData, GroupResult group, TestResult sr, string paneId, string? reportLink)
    {
        var status = StatusText(sr.Status);
        var isLoad = sr.TestType == TestType.Load && sr.Status != TestStatus.Skipped;

        b.AppendLine($@"<template id=""{paneId}"">");

        b.AppendLine(@"<div class=""titlerow"">");
        b.AppendLine($@"<h2 title=""{E(sr.FullName)}"">{E(sr.Name)}</h2>");
        b.AppendLine($@"<span class=""kindbadge{(isLoad ? " load" : "")}"">{(isLoad ? "Load test" : "Standard test")}</span>");
        b.AppendLine("</div>");
        if (!string.IsNullOrEmpty(sr.Description))
            b.AppendLine($@"<p class=""desc"">{E(sr.Description)}</p>");

        b.AppendLine(@"<dl class=""d-meta"">");
        b.AppendLine($@"<div><dt>Group</dt><dd title=""{E(group.Name)}"">{E(group.Name)}</dd></div>");
        b.AppendLine($@"<div><dt>Suite</dt><dd>{E(reportData.Suite.Name)}</dd></div>");
        b.Append("<div><dt>Tags</dt>");
        if (sr.Tags != null && sr.Tags.Count > 0)
        {
            b.Append(@"<dd class=""dtags"">");
            foreach (var tag in sr.Tags)
                b.Append($@"<span class=""dtag"" title=""{E(tag)}"">{E(tag)}</span>");
            b.Append("</dd>");
        }
        else
            b.Append(@"<dd class=""muted"">None</dd>");
        b.AppendLine("</div>");
        b.AppendLine("</dl>");

        var targetEnvironment = TargetEnvironment();
        b.AppendLine(@"<div class=""rfacts"">");
        if (!string.IsNullOrEmpty(targetEnvironment))
            b.AppendLine($@"<span><span class=""k"">Environment</span> {E(targetEnvironment)}</span>");
        b.AppendLine(@"<a class=""alink"" href=""TestFuzn_Log.log"" target=""_blank"">Open the log ↗</a>");
        b.AppendLine("</div>");

        if (isLoad && reportLink != null)
            WriteLoadPreview(b, reportLink);

        WriteTiles(b, sr, status);
        WriteProperties(b, reportData, group, sr);
        WriteError(b, sr);

        if (HasTimes(sr) && !isLoad)
            WritePhases(b, sr);

        if (!isLoad && sr.IterationResults.Count > 0
            && (sr.IterationResults.Count > 1 || sr.IterationResults.Any(i => i.StepResults.Count > 0)))
            WriteSteps(b, sr);

        b.AppendLine("</template>");
    }

    private string? TargetEnvironment()
    {
        if (_testSession.Configuration == null)
            return null;
        return _testSession.Configuration.TargetEnvironment;
    }

    private string? ExecutionEnvironment()
    {
        if (_testSession.Configuration == null)
            return null;
        return _testSession.Configuration.ExecutionEnvironment;
    }

    private static void WriteLoadPreview(StringBuilder b, string reportLink)
    {
        b.AppendLine(@"<div class=""loadprev"">");
        b.AppendLine(@"<svg class=""lpart"" viewBox=""0 0 132 92"" aria-hidden=""true"">"
            + @"<rect class=""lp-win"" x=""4"" y=""4"" width=""124"" height=""84"" rx=""9""/>"
            + @"<path class=""lp-bar"" d=""M4 13a9 9 0 0 1 9-9h106a9 9 0 0 1 9 9v5H4z""/>"
            + @"<circle class=""lp-dot"" cx=""13"" cy=""11"" r=""2""/><circle class=""lp-dot"" cx=""20"" cy=""11"" r=""2""/><circle class=""lp-dot"" cx=""27"" cy=""11"" r=""2""/>"
            + @"<rect class=""lp-soft"" x=""38"" y=""9"" width=""44"" height=""4"" rx=""2""/>"
            + @"<polyline class=""lp-line"" points=""16,48 32,36 48,42 64,28 80,34 96,22""/>"
            + @"<rect class=""lp-col"" x=""16"" y=""70"" width=""8"" height=""10"" rx=""1.5""/><rect class=""lp-col"" x=""28"" y=""64"" width=""8"" height=""16"" rx=""1.5""/>"
            + @"<rect class=""lp-col"" x=""40"" y=""58"" width=""8"" height=""22"" rx=""1.5""/><rect class=""lp-col"" x=""52"" y=""66"" width=""8"" height=""14"" rx=""1.5""/>"
            + @"<circle class=""lp-ring"" cx=""100"" cy=""66"" r=""11""/><path class=""lp-arc"" d=""M100 55a11 11 0 0 1 10.5 14.3""/>"
            + "</svg>");
        b.AppendLine(@"<div class=""lptext"">");
        b.AppendLine(@"<span class=""lptitle"">This is a load test.</span>");
        b.AppendLine(@"<span class=""lpsub"">The charts, the steps and all numbers are in the load test report.</span>");
        b.AppendLine($@"<a class=""btn primary lpbtn"" href=""{reportLink}"">{BrandHtml.Icon("load", 17)}Open the load test report</a>");
        b.AppendLine("</div></div>");
    }

    private static void WriteTiles(StringBuilder b, TestResult sr, string status)
    {
        var tone = BrandHtml.StatusTone(status);
        string hint;
        if (sr.Status == TestStatus.Skipped)
            hint = "The test did not run";
        else if (sr.FailedIn != null)
            hint = $"Failed in {sr.FailedIn}";
        else if (sr.TotalCount > 1)
            hint = $"{sr.TotalCount} iterations, {sr.FailedCount} failed";
        else if (sr.Status == TestStatus.Failed)
            hint = "A step failed";
        else
            hint = "Every step passed";

        b.AppendLine(@"<div class=""stiles"">");
        b.AppendLine($@"<div class=""stile framed {tone}""><span class=""tinner"">"
            + $@"<span class=""tico {tone}""><svg viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""{BrandHtml.StatusGlyph(status)}""/></svg></span>"
            + $@"<span class=""tbody""><span class=""tlabel"">Result</span><span class=""tvalue"">{status}</span><span class=""thint"">{E(hint)}</span></span>"
            + "</span></div>");

        if (HasTimes(sr))
        {
            var durationHint = sr.TotalCount > 1 && sr.TestType != TestType.Load
                ? $"Average {TimeSpan.FromTicks((long)sr.IterationResults.Average(i => i.Duration().Ticks)).ToTestFuznReadableString()} per iteration"
                : $"Execution {sr.ExecuteDuration().ToTestFuznReadableString()}";
            Tile(b, "slow", "Duration", sr.TestRunDuration().ToTestFuznReadableString(), durationHint);

            var start = sr.StartTime().ToLocalTime();
            var end = sr.EndTime().ToLocalTime();
            var endText = start.Date == end.Date ? end.ToString("HH:mm:ss", Inv) : end.ToString("yyyy-MM-dd HH:mm:ss", Inv);
            Tile(b, "runs", "Run", $"{start.ToString("HH:mm:ss", Inv)} - {endText}", start.ToString("d MMMM yyyy", Inv));

            if (sr.TestType != TestType.Load && sr.TotalCount > 1)
                Tile(b, "layers", "Iterations", sr.TotalCount.ToString("N0", Inv), $"{sr.PassedCount} passed, {sr.FailedCount} failed");
        }

        b.AppendLine("</div>");
    }

    private static void Tile(StringBuilder b, string icon, string label, string value, string hint)
    {
        b.AppendLine($@"<div class=""stile""><span class=""tico soft"">{BrandHtml.Icon(icon, 20)}</span>"
            + $@"<span class=""tbody""><span class=""tlabel"">{E(label)}</span><span class=""tvalue"" title=""{E(value)}"">{E(value)}</span><span class=""thint"">{E(hint)}</span></span></div>");
    }

    private static void BlockHead(StringBuilder b, string icon, string title, string? extraHtml = null)
    {
        b.AppendLine($@"<div class=""block-h""><span class=""bico"">{BrandHtml.Icon(icon, 20)}</span><span class=""btitle"">{E(title)}</span>{extraHtml}</div>");
    }

    private void WriteProperties(StringBuilder b, StandardReportData reportData, GroupResult group, TestResult sr)
    {
        b.AppendLine(@"<div class=""block closed"">");
        b.AppendLine($@"<button type=""button"" class=""block-h fold"" aria-expanded=""false""><span class=""bico"">{BrandHtml.Icon("info", 20)}</span><span class=""btitle"">Properties</span>{BrandHtml.Icon("chevron", 18).Replace(@"class=""ico""", @"class=""ico pchev""")}</button>");
        b.AppendLine(@"<div class=""fold-body"">");

        if (sr.Metadata != null && sr.Metadata.Count > 0)
        {
            b.AppendLine(@"<h4 class=""psub"">Metadata</h4><dl class=""plist"">");
            foreach (var kv in sr.Metadata)
                PropRow(b, kv.Key, kv.Value);
            b.AppendLine("</dl>");
        }

        b.AppendLine(@"<h4 class=""psub"">Test</h4><dl class=""plist"">");
        PropRow(b, "Full name", sr.FullName);
        PropRow(b, "Group", group.Name);
        PropRow(b, "Type", sr.TestType == TestType.Load ? "Load" : "Standard");
        if (sr.Status != TestStatus.Skipped && sr.TestType != TestType.Load)
            PropRow(b, "Iterations", sr.TotalCount.ToString("N0", Inv));
        if (HasTimes(sr))
        {
            PropRow(b, "Started", Time(sr.StartTime()));
            PropRow(b, "Ended", Time(sr.EndTime()));
        }
        b.AppendLine("</dl>");

        b.AppendLine(@"<h4 class=""psub"">Run</h4><dl class=""plist"">");
        PropRow(b, "Suite", reportData.Suite.Name);
        PropRow(b, "Machine", ExecutionEnvironment());
        PropRow(b, "Target environment", TargetEnvironment());
        b.AppendLine("</dl>");

        b.AppendLine(@"<h4 class=""psub"">IDs</h4><dl class=""plist"">");
        if (!string.IsNullOrEmpty(sr.Id) && !sr.IsIdAutomatic)
            PropRow(b, "Test id", sr.Id, true);
        if (!sr.HasInputData && sr.IterationResults.Count > 0)
            PropRow(b, "Correlation id", sr.IterationResults[0].CorrelationId, true);
        PropRow(b, "Run id", reportData.TestRunId, true);
        b.AppendLine("</dl>");

        b.AppendLine("</div></div>");
    }

    private static void PropRow(StringBuilder b, string label, string? value, bool id = false)
    {
        if (string.IsNullOrEmpty(value))
            return;
        b.AppendLine($@"<div class=""prow""><dt>{E(label)}</dt><dd{(id ? @" class=""id""" : "")}><span class=""pval"">{E(value)}</span>{(id ? CopyButton(value) : "")}</dd></div>");
    }

    private static string CopyButton(string value) =>
        $@"<button type=""button"" class=""copy"" data-copy=""{E(value)}"" title=""Copy"" aria-label=""Copy"">{BrandHtml.Icon("copy", 14)}</button>";

    private static void WriteError(StringBuilder b, TestResult sr)
    {
        if (sr.Failure == null)
            return;
        // Like Cockpit: a step that shows the same error makes the error block redundant.
        if (sr.FailedIn == null && sr.IterationResults.Any(i => i.StepResults.Values.Any(s => StepOwns(s, sr.Failure))))
            return;

        b.AppendLine(@"<div class=""block err-block"">");
        BlockHead(b, "bug", "Error", sr.FailedIn == null ? null : $@"<span class=""bsub"">Failed in {E(sr.FailedIn)}</span>");
        WriteFailure(b, sr.Failure);
        b.AppendLine("</div>");
    }

    private static bool StepOwns(StepStandardResult step, Exception failure)
    {
        if (step.Exception != null
            && (ReferenceEquals(step.Exception, failure)
                || (step.Exception.GetType() == failure.GetType() && step.Exception.Message == failure.Message)))
            return true;
        if (step.StepResults == null)
            return false;
        return step.StepResults.Any(s => StepOwns(s, failure));
    }

    private static void WritePhases(StringBuilder b, TestResult sr)
    {
        b.AppendLine(@"<div class=""block"">");
        BlockHead(b, "doc", "Phases");
        b.AppendLine(@"<div class=""tscroll""><table class=""dtable"">");
        b.AppendLine(@"<thead><tr><th>Phase</th><th class=""r"">Duration</th><th>Started</th><th>Ended</th></tr></thead><tbody>");
        PhaseRow(b, "Init", sr.InitDuration(), sr.InitStartTime, sr.InitEndTime);
        PhaseRow(b, "Execution", sr.ExecuteDuration(), sr.ExecuteStartTime, sr.ExecuteEndTime);
        PhaseRow(b, "Cleanup", sr.CleanupDuration(), sr.CleanupStartTime, sr.CleanupEndTime);
        b.AppendLine("</tbody></table></div>");
        b.AppendLine("</div>");
    }

    private static void PhaseRow(StringBuilder b, string name, TimeSpan duration, DateTime start, DateTime end)
    {
        b.AppendLine($@"<tr><td>{name}</td><td class=""r"">{duration.ToTestFuznReadableString()}</td><td class=""small"">{Time(start)}</td><td class=""small"">{Time(end)}</td></tr>");
    }

    private static void WriteSteps(StringBuilder b, TestResult sr)
    {
        var iterations = sr.IterationResults;
        var single = iterations.Count == 1;

        b.AppendLine(@"<div class=""block"">");
        string? extra = null;
        if (!single)
        {
            var allOpen = iterations.All(i => !i.Passed);
            extra = $@"<span class=""hcount"">{iterations.Count} iterations</span><button type=""button"" class=""foldall"" data-foldall>{(allOpen ? "Collapse all" : "Expand all")}</button>";
        }
        BlockHead(b, "list", "Steps", extra);

        if (single)
        {
            var iteration = iterations[0];
            if (sr.HasInputData)
                WriteIterationMeta(b, iteration);
            WriteStepList(b, iteration);
        }
        else
        {
            for (var i = 0; i < iterations.Count; i++)
            {
                var iteration = iterations[i];
                var itStatus = iteration.Passed ? "Passed" : "Failed";
                var open = !iteration.Passed;
                var input = InputText(iteration);
                var stepCount = iteration.StepResults.Count;

                b.AppendLine($@"<div class=""iter{(open ? " open" : "")}"">");
                b.Append($@"<button type=""button"" class=""iter-h"" aria-expanded=""{(open ? "true" : "false")}"">");
                b.Append(BrandHtml.Icon("chevron", 14).Replace(@"class=""ico""", @"class=""ico ichev"""));
                b.Append(BrandHtml.StatusMark(itStatus));
                b.Append($@"<span class=""iname"">Iteration {i + 1}<span class=""muted""> · {E(input)}</span></span>");
                b.Append($@"<span class=""isum"">{stepCount} {(stepCount == 1 ? "step" : "steps")} · {iteration.Duration().ToTestFuznReadableString()}</span>");
                b.AppendLine("</button>");
                b.AppendLine(@"<div class=""ibody"">");
                WriteIterationMeta(b, iteration);
                WriteStepList(b, iteration);
                b.AppendLine("</div></div>");
            }
        }

        b.AppendLine("</div>");
    }

    private static string InputText(IterationResult iteration)
    {
        var text = iteration.InputData == null ? null : iteration.InputData.ToString();
        return string.IsNullOrEmpty(text) ? "(empty input)" : text;
    }

    private static void WriteIterationMeta(StringBuilder b, IterationResult iteration)
    {
        if (iteration.InputData != null)
            b.AppendLine($@"<div class=""imeta"">Input: {E(InputText(iteration))}</div>");
        if (!string.IsNullOrEmpty(iteration.CorrelationId))
            b.AppendLine($@"<div class=""imeta corr"">Correlation ID: <span class=""cval"">{E(iteration.CorrelationId)}</span>{CopyButton(iteration.CorrelationId)}</div>");
    }

    private static void WriteStepList(StringBuilder b, IterationResult iteration)
    {
        b.AppendLine(@"<div class=""steptable"">");
        b.AppendLine(@"<div class=""step-h""><span>Step</span><span class=""sdur"">Duration</span></div>");
        b.AppendLine(@"<div class=""stree"">");
        var n = 0;
        foreach (var step in iteration.StepResults)
        {
            n++;
            WriteStepRow(b, step.Value, n.ToString(Inv));
        }
        b.AppendLine("</div></div>");
    }

    private static void WriteStepRow(StringBuilder b, StepStandardResult step, string num)
    {
        b.AppendLine($@"<div class=""srow"">{BrandHtml.StatusMark(StatusText(step.Status))}<span class=""snum"">Step {num} ·</span><span class=""sname"" title=""{E(step.Name)}"">{E(step.Name)}</span><span class=""sdur"">{step.Duration.ToTestFuznReadableString()}</span></div>");

        var showId = !string.IsNullOrEmpty(step.Id) && !step.IsIdAutomatic;
        var hasComments = step.Comments != null && step.Comments.Count > 0;
        var hasAttachments = step.Attachments != null && step.Attachments.Count > 0;
        var hasFailure = step.Status == StepStatus.Failed && step.Exception != null;
        var hasSubSteps = step.StepResults != null && step.StepResults.Count > 0;
        if (!showId && !hasComments && !hasAttachments && !hasFailure && !hasSubSteps)
            return;

        b.AppendLine(@"<div class=""skids"">");
        if (showId)
            b.AppendLine($@"<div class=""sline""><span class=""sico"">#</span><span class=""stext"">id: {E(step.Id)}</span><span class=""sdur"">-</span></div>");
        if (hasComments)
        {
            foreach (var comment in step.Comments!)
                b.AppendLine($@"<div class=""sline""><span class=""sico"">{BrandHtml.Icon("comment", 15)}</span><span class=""stext"">{E(comment.Text)}</span><span class=""sdur"">-</span></div>");
        }
        if (hasAttachments)
        {
            foreach (var attachment in step.Attachments!)
            {
                var fileName = Path.GetFileName(attachment.Path);
                b.AppendLine($@"<div class=""sline""><span class=""sico"">{BrandHtml.Icon("clip", 15)}</span><span class=""stext""><a class=""alink"" href=""Data/Attachments/{E(fileName)}"" target=""_blank"">{E(attachment.Name)}</a></span><span class=""sdur"">-</span></div>");
            }
        }
        if (hasFailure)
        {
            b.AppendLine(@"<div class=""stepfail"">");
            WriteFailure(b, step.Exception);
            b.AppendLine("</div>");
        }
        if (hasSubSteps)
        {
            for (var i = 0; i < step.StepResults!.Count; i++)
                WriteStepRow(b, step.StepResults[i], $"{num}.{i + 1}");
        }
        b.AppendLine("</div>");
    }

    private const int TraceLines = 6;

    private static void WriteFailure(StringBuilder b, Exception exception)
    {
        b.AppendLine(@"<div class=""failure"">");
        var first = true;
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (!first)
                b.AppendLine(@"<div class=""causedby"">Caused by</div>");
            first = false;

            b.AppendLine($@"<div class=""fmsg""><span class=""ftype"">{E(current.GetType().FullName)}</span>: {E(current.Message)}</div>");

            if (string.IsNullOrEmpty(current.StackTrace))
                continue;

            var lines = current.StackTrace.TrimEnd().Split('\n');
            if (lines.Length <= TraceLines)
            {
                b.AppendLine($@"<pre class=""trace"">{E(current.StackTrace.TrimEnd())}</pre>");
                continue;
            }

            var head = string.Join("\n", lines.Take(TraceLines));
            var rest = string.Join("\n", lines.Skip(TraceLines));
            b.AppendLine($@"<div class=""tracebox""><pre class=""trace"">{E(head)}<span class=""tracerest"">{"\n"}{E(rest)}</span></pre>"
                + $@"<button type=""button"" class=""tracemore"" data-more=""Show all {lines.Length} lines"">Show all {lines.Length} lines</button></div>");
        }
        b.AppendLine("</div>");
    }

    private static void WritePane(StringBuilder b)
    {
        b.AppendLine(@"<div class=""grip"" id=""pane-grip"" hidden title=""Drag to resize""></div>");
        b.AppendLine(@"<aside class=""pane"" id=""test-pane"" hidden aria-label=""Test result"">");
        b.AppendLine(@"<div class=""d-bar"">");
        b.AppendLine(@"<span class=""headlbl"">Test</span><span class=""dpos"" id=""pane-pos""></span>");
        b.AppendLine(@"<span class=""navbtns"" role=""group"" aria-label=""Step through tests"">"
            + @"<button type=""button"" class=""stepbtn"" data-nav=""-1"" title=""Previous test"" aria-label=""Previous test""><svg viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M15 6l-6 6 6 6""/></svg></button>"
            + @"<button type=""button"" class=""stepbtn"" data-nav=""1"" title=""Next test"" aria-label=""Next test""><svg viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M9 6l6 6-6 6""/></svg></button>"
            + "</span>");
        b.AppendLine(@"<button type=""button"" class=""close"" id=""pane-close"" title=""Close (Esc)"" aria-label=""Close"">×</button>");
        b.AppendLine("</div>");
        b.AppendLine(@"<div class=""tdetail"" id=""pane-body""></div>");
        b.AppendLine("</aside>");
    }

    private static void WriteScript(StringBuilder b)
    {
        b.AppendLine("<script>");
        b.AppendLine(Script);
        b.AppendLine("</script>");
    }

    private const string Script = """
document.addEventListener('DOMContentLoaded', function () {
    var activeStatus = 'all', activeType = 'all', nameQuery = '';
    var allRows = Array.prototype.slice.call(document.querySelectorAll('tr[data-test-row]'));
    var pane = document.getElementById('test-pane'), grip = document.getElementById('pane-grip');
    var body = document.getElementById('pane-body'), pos = document.getElementById('pane-pos');
    var current = null;

    function visibleRows() { return allRows.filter(function (r) { return !r.classList.contains('filter-hidden'); }); }

    function applyFilters() {
        var visibleGroups = {};
        allRows.forEach(function (row) {
            var statusOk = activeStatus === 'all' || row.dataset.status === activeStatus;
            var typeOk = activeType === 'all' || row.dataset.type === activeType;
            var nameOk = nameQuery === '' || (row.dataset.testName || '').indexOf(nameQuery) !== -1;
            var visible = statusOk && typeOk && nameOk;
            row.classList.toggle('filter-hidden', !visible);
            if (visible) visibleGroups[row.dataset.groupName] = true;
        });
        document.querySelectorAll('tr[data-group-row]').forEach(function (row) {
            row.classList.toggle('filter-hidden', !visibleGroups[row.dataset.groupName]);
        });
        if (current) updateNav();
    }
    document.querySelectorAll('.pill[data-status-filter]').forEach(function (pill) {
        pill.addEventListener('click', function () {
            document.querySelectorAll('.pill[data-status-filter]').forEach(function (p) { p.classList.remove('active'); });
            pill.classList.add('active');
            activeStatus = pill.dataset.statusFilter;
            applyFilters();
        });
    });
    document.querySelectorAll('[data-type-filter]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            document.querySelectorAll('[data-type-filter]').forEach(function (b) {
                b.classList.toggle('active', b === btn);
                b.setAttribute('aria-pressed', b === btn);
            });
            activeType = btn.dataset.typeFilter;
            applyFilters();
        });
    });
    var nameInput = document.getElementById('test-name-filter');
    if (nameInput) nameInput.addEventListener('input', function () { nameQuery = nameInput.value.toLowerCase(); applyFilters(); });

    function updateNav() {
        var list = visibleRows(), i = list.indexOf(current);
        pos.textContent = i < 0 ? '' : (i + 1) + ' of ' + list.length;
        pane.querySelector('[data-nav="-1"]').disabled = i <= 0;
        pane.querySelector('[data-nav="1"]').disabled = i < 0 || i >= list.length - 1;
    }
    function openRow(row) {
        var template = document.getElementById(row.dataset.pane);
        if (!template) return;
        if (current) current.classList.remove('sel');
        current = row;
        row.classList.add('sel');
        body.replaceChildren(template.content.cloneNode(true));
        pane.hidden = false; grip.hidden = false; pane.scrollTop = 0;
        updateNav();
        history.replaceState(null, '', '#' + row.dataset.pane);
    }
    function closePane() {
        pane.hidden = true; grip.hidden = true;
        if (current) { current.classList.remove('sel'); current.focus(); }
        current = null;
        history.replaceState(null, '', location.pathname + location.search);
    }
    allRows.forEach(function (row) {
        row.addEventListener('click', function (e) { if (!e.target.closest('a')) openRow(row); });
        row.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); openRow(row); } });
    });
    pane.querySelectorAll('[data-nav]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var list = visibleRows(), next = list[list.indexOf(current) + Number(btn.dataset.nav)];
            if (next) { openRow(next); next.scrollIntoView({ block: 'nearest' }); }
        });
    });
    document.getElementById('pane-close').addEventListener('click', closePane);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && current) closePane(); });

    body.addEventListener('click', function (e) {
        var t = e.target.closest('button');
        if (!t) return;
        if (t.classList.contains('iter-h')) {
            var open = t.parentElement.classList.toggle('open');
            t.setAttribute('aria-expanded', open);
        } else if (t.hasAttribute('data-foldall')) {
            var iters = body.querySelectorAll('.iter'), all = body.querySelectorAll('.iter.open').length === iters.length;
            iters.forEach(function (it) { it.classList.toggle('open', !all); it.firstElementChild.setAttribute('aria-expanded', !all); });
            t.textContent = all ? 'Expand all' : 'Collapse all';
        } else if (t.classList.contains('fold')) {
            var closed = t.parentElement.classList.toggle('closed');
            t.setAttribute('aria-expanded', !closed);
        } else if (t.classList.contains('tracemore')) {
            var more = t.parentElement.classList.toggle('open');
            t.textContent = more ? 'Show less' : t.dataset.more;
        } else if (t.hasAttribute('data-copy') && navigator.clipboard) {
            navigator.clipboard.writeText(t.dataset.copy).then(function () {
                t.classList.add('done');
                setTimeout(function () { t.classList.remove('done'); }, 1200);
            }, function () { });
        }
    });

    var widthKey = 'testfuzn.paneWidth';
    function clampWidth(w) { return Math.round(Math.min(Math.max(w, 420), window.innerWidth * 0.75)); }
    function setWidth(w) { pane.style.width = w + 'px'; grip.style.right = w + 'px'; return w; }
    var saved = NaN;
    try { saved = Number(localStorage.getItem(widthKey)); } catch (e) { }
    var paneWidth = setWidth(clampWidth(saved > 0 ? saved : Math.min(860, window.innerWidth * 0.5)));
    grip.addEventListener('pointerdown', function (e) {
        e.preventDefault();
        var startX = e.clientX, startWidth = paneWidth;
        function move(ev) { paneWidth = setWidth(clampWidth(startWidth + startX - ev.clientX)); }
        function up() {
            window.removeEventListener('pointermove', move);
            window.removeEventListener('pointerup', up);
            try { localStorage.setItem(widthKey, String(paneWidth)); } catch (e) { }
        }
        window.addEventListener('pointermove', move);
        window.addEventListener('pointerup', up);
    });

    var start = location.hash.slice(1);
    var startRow = allRows.filter(function (r) { return r.dataset.pane === start; })[0];
    if (startRow) { openRow(startRow); startRow.scrollIntoView({ block: 'center' }); }
});
""";
}
