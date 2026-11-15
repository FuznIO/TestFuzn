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
        b.AppendLine(@"<div class=""page-container"">");

        BrandHtml.WriteMasthead(b, "Data/Assets/", @"<a class=""masthead-link"" href=""TestFuzn_Log.log"" target=""_blank"">View log ↗</a>");

        BrandHtml.WritePageTitle(b, "tests", reportData.Suite.Name, "Test report");

        WriteDashboard(b, testsTotal, testsPassed, testsFailed, testsSkipped, passRate);

        b.AppendLine(@"<div class=""overview"">");
        WriteResultDonut(b, testsPassed, testsFailed, testsSkipped);
        WriteRunInfo(reportData, b);
        b.AppendLine("</div>");

        WriteGroupResults(reportData, b);

        WriteFilterScript(b);

        BrandHtml.WriteProductSignature(b);

        b.AppendLine("</div>");
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

    private static void WriteFilterScript(StringBuilder b)
    {
        b.AppendLine("<script>");
        b.AppendLine("document.addEventListener('DOMContentLoaded', function() {");
        b.AppendLine("    var activeStatus = 'all';");
        b.AppendLine("    var nameQuery = '';");
        b.AppendLine("    function applyFilters() {");
        b.AppendLine("        var visibleGroups = {};");
        b.AppendLine("        document.querySelectorAll('tr[data-test-row]').forEach(function(row) {");
        b.AppendLine("            var status = row.dataset.status;");
        b.AppendLine("            var name = row.dataset.testName || '';");
        b.AppendLine("            var statusOk = activeStatus === 'all' || status === activeStatus;");
        b.AppendLine("            var nameOk = nameQuery === '' || name.indexOf(nameQuery) !== -1;");
        b.AppendLine("            var visible = statusOk && nameOk;");
        b.AppendLine("            row.classList.toggle('filter-hidden', !visible);");
        b.AppendLine("            if (visible) visibleGroups[row.dataset.groupName] = true;");
        b.AppendLine("        });");
        b.AppendLine("        document.querySelectorAll('tr[data-group-row]').forEach(function(row) {");
        b.AppendLine("            row.classList.toggle('filter-hidden', !visibleGroups[row.dataset.groupName]);");
        b.AppendLine("        });");
        b.AppendLine("    }");
        b.AppendLine("    document.querySelectorAll('.pill[data-status-filter]').forEach(function(pill) {");
        b.AppendLine("        pill.addEventListener('click', function() {");
        b.AppendLine("            document.querySelectorAll('.pill[data-status-filter]').forEach(function(p) { p.classList.remove('active'); });");
        b.AppendLine("            pill.classList.add('active');");
        b.AppendLine("            activeStatus = pill.dataset.statusFilter;");
        b.AppendLine("            applyFilters();");
        b.AppendLine("        });");
        b.AppendLine("    });");
        b.AppendLine("    var nameInput = document.getElementById('test-name-filter');");
        b.AppendLine("    if (nameInput) {");
        b.AppendLine("        nameInput.addEventListener('input', function() {");
        b.AppendLine("            nameQuery = nameInput.value.toLowerCase();");
        b.AppendLine("            applyFilters();");
        b.AppendLine("        });");
        b.AppendLine("    }");
        b.AppendLine("});");
        b.AppendLine("</script>");
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
        b.AppendLine($@"<button type=""button"" class=""pill"" data-status-filter=""passed""><span class=""dot passed""></span>Passed <span class=""count"">{passed}</span></button>");
        b.AppendLine($@"<button type=""button"" class=""pill"" data-status-filter=""failed""><span class=""dot failed""></span>Failed <span class=""count"">{failed}</span></button>");
        b.AppendLine($@"<button type=""button"" class=""pill"" data-status-filter=""skipped""><span class=""dot skipped""></span>Skipped <span class=""count"">{skipped}</span></button>");
        b.AppendLine("</div>");
        b.AppendLine(@"<input type=""search"" id=""test-name-filter"" placeholder=""Filter by name"" />");
        b.AppendLine("</div>");
    }

    private void WriteGroupResults(StandardReportData reportData, StringBuilder b)
    {
        var total = reportData.GroupResults.Sum(g => g.Value.TestResults.Count);
        BrandHtml.CardStart(b, "tests", "Tests", null, $@"<span class=""card-count"">{total.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} tests</span>", "results-card");
        WriteFilterBar(reportData, b);
        b.Append(@"<div class=""table-scroll""><table class=""group-results"">");
        b.AppendLine("<thead><tr>");
        b.AppendLine("<th>Test</th>");
        b.AppendLine("<th>Type</th>");
        b.AppendLine("<th>Status</th>");
        b.AppendLine(@"<th class=""num"">Duration</th>");
        b.AppendLine("<th>Tags</th>");
        b.AppendLine("</tr></thead><tbody>");
        foreach (var groupResult in reportData.GroupResults.OrderBy(f => f.Value.Name))
        {
            var groupTotal = groupResult.Value.TestResults.Count;
            var groupPassed = groupResult.Value.TestResults.Count(t => t.Value.Status == TestStatus.Passed);
            var groupFailed = groupResult.Value.TestResults.Count(t => t.Value.Status == TestStatus.Failed);
            var groupSkipped = groupResult.Value.TestResults.Count(t => t.Value.Status == TestStatus.Skipped);

            var groupNameAttr = E(groupResult.Value.Name.ToLowerInvariant());
            b.AppendLine(@$"<tr class=""group"" data-group-row data-group-name=""{groupNameAttr}"">");
            b.Append($@"<td colspan=""5""><span class=""group-name"">{E(groupResult.Value.Name)}</span>");
            b.Append($@"<span class=""group-summary"">{groupTotal} test{(groupTotal == 1 ? "" : "s")} · {groupPassed} passed");
            if (groupFailed > 0)
                b.Append($@" · <span class=""bad-text"">{groupFailed} failed</span>");
            else
                b.Append(" · 0 failed");
            b.AppendLine($" · {groupSkipped} skipped</span></td>");
            b.AppendLine("</tr>");

            WriteTestResults(b, groupResult);
        }

        b.Append("</tbody></table></div>");
        BrandHtml.CardEnd(b);
    }

    private void WriteTestResults(StringBuilder b, KeyValuePair<string, GroupResult> groupResults)
    {
        var groupNameAttr = E(groupResults.Value.Name.ToLowerInvariant());
        foreach (var testResult in groupResults.Value.TestResults.OrderBy(t => t.Value.Name))
        {
            var icon = BrandHtml.TestStatusIcon(testResult.Value.Status);
            var statusAttr = testResult.Value.Status switch
            {
                TestStatus.Passed => "passed",
                TestStatus.Failed => "failed",
                TestStatus.Skipped => "skipped",
                _ => ""
            };

            var typeText = testResult.Value.Status == TestStatus.Skipped
                ? "N/A"
                : testResult.Value.TestType == TestType.Load ? "Load" : "Standard";

            string? reportLink = null;
            if (testResult.Value.TestType == TestType.Load && testResult.Value.Status != TestStatus.Skipped)
            {
                var reportName = FileNameHelper.MakeFilenameSafe($"{groupResults.Value.Name}-{testResult.Value.Name}");
                reportLink = $"Data/{E(reportName)}.html";
            }

            var testNameAttr = E(testResult.Value.Name.ToLowerInvariant());
            b.AppendLine($@"<tr data-test-row data-status=""{statusAttr}"" data-test-name=""{testNameAttr}"" data-group-name=""{groupNameAttr}"">");
            b.AppendLine(@"<td>");

            WriteTestDetails(b, testResult.Value, icon, reportLink);

            b.AppendLine("</td>");
            b.AppendLine($@"<td><span class=""type-pill"">{E(typeText)}</span></td>");
            b.AppendLine($@"<td>{BrandHtml.StatusPill(testResult.Value.Status)}</td>");
            b.AppendLine($@"<td class=""num"">{DurationCell(testResult.Value)}</td>");
            b.AppendLine(@"<td><span class=""tags"">");
            if (testResult.Value.Tags != null && testResult.Value.Tags.Count > 0)
            {
                foreach (var tag in testResult.Value.Tags)
                    b.Append($@"<span class=""tag"">{E(tag)}</span>");
            }
            b.AppendLine("</span></td>");
            b.AppendLine("</tr>");
        }
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

    private void WriteTestDetails(StringBuilder b, TestResult sr, string icon, string? reportLink)
    {
        if (sr.Status == TestStatus.Skipped)
        {
            b.AppendLine($"{icon} {E(sr.Name)}");
            return;
        }

        b.AppendLine(@"<details class=""results"">");
        b.AppendLine($@"<summary class=""test-name"">{icon}<span class=""name-link"">{E(sr.Name)}</span></summary>");

        var showCorrelationId = !sr.HasInputData && sr.IterationResults.Count > 0;
        var useToggle = sr.TestType != TestType.Load;

        if (!string.IsNullOrEmpty(reportLink))
            b.AppendLine($@"<div><a class=""report-link"" href=""{reportLink}"">Open the load test report →</a></div>");

        if (!string.IsNullOrEmpty(sr.Description))
            b.AppendLine($"<div>{E(sr.Description)}</div>");

        if (sr.Failure != null && sr.FailedIn != null)
            b.AppendLine($@"<div class=""hook-failure"">Failed in {E(sr.FailedIn)}: {E(sr.Failure.GetType().Name)}: {E(sr.Failure.Message)}</div>");

        if (useToggle)
            b.AppendLine(@"<details class=""link-toggle""><summary><span class=""t-show"">Show details</span><span class=""t-hide"">Hide details</span></summary>");

        var showId = !string.IsNullOrEmpty(sr.Id) && !sr.IsIdAutomatic;
        var hasInfoRows = showId
            || showCorrelationId
            || (sr.Metadata != null && sr.Metadata.Count > 0);

        if (hasInfoRows)
        {
            b.AppendLine(@"<table class=""inner"">");
            if (showId)
                b.AppendLine($@"<tr><th class=""vertical"">Id</th><td>{E(sr.Id)}</td></tr>");
            if (showCorrelationId)
                b.AppendLine($@"<tr><th class=""vertical"">CorrelationId</th><td>{E(sr.IterationResults[0].CorrelationId)}</td></tr>");
            if (sr.Metadata != null && sr.Metadata.Count > 0)
            {
                foreach (var kv in sr.Metadata)
                    b.AppendLine($@"<tr><th class=""vertical"">{E(kv.Key)}</th><td>{E(kv.Value)}</td></tr>");
            }
            b.AppendLine("</table>");
        }

        b.AppendLine(@"<table class=""inner"">");
        b.AppendLine("<tr><th>Phase</th><th>Duration</th><th>Started</th><th>Ended</th></tr>");
        b.AppendLine($"<tr><td>Init</td><td>{sr.InitDuration().ToTestFuznDurationHtml()}</td><td>{sr.InitStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td><td>{sr.InitEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td></tr>");
        b.AppendLine($"<tr><td>Execution</td><td>{sr.ExecuteDuration().ToTestFuznDurationHtml()}</td><td>{sr.ExecuteStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td><td>{sr.ExecuteEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td></tr>");
        b.AppendLine($"<tr><td>Cleanup</td><td>{sr.CleanupDuration().ToTestFuznDurationHtml()}</td><td>{sr.CleanupStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td><td>{sr.CleanupEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td></tr>");
        b.AppendLine($"<tr><td>Total Test Run</td><td>{sr.TestRunDuration().ToTestFuznReadableString()}</td><td>{sr.StartTime().ToLocalTime():yyyy-MM-dd HH:mm:ss}</td><td>{sr.EndTime().ToLocalTime():yyyy-MM-dd HH:mm:ss}</td></tr>");
        b.AppendLine("</table>");

        if (useToggle)
            b.AppendLine("</details>");

        if (sr.IterationResults.Count > 0)
        {
            WriteStepDetails(b, sr);
        }

        b.AppendLine("</details>");
    }

    private void WriteStepDetails(StringBuilder b, TestResult sr)
    {
        b.AppendLine(@"<table class=""inner iterations"">");
        b.AppendLine("<tr>");
        b.AppendLine($"<th>{(sr.HasInputData ? "Iteration / Step" : "Step")}</th>");
        b.AppendLine(@"<th style=""width:130px"">Status</th>");
        b.AppendLine(@"<th style=""width:90px"">Duration</th>");
        b.AppendLine("</tr>");

        if (!sr.HasInputData)
        {
            foreach (var stepResult in sr.IterationResults[0].StepResults)
            {
                WriteStepResult(b, stepResult.Value, 1);
            }
        }
        else
        {
            for (int i = 0; i < sr.IterationResults.Count; i++)
            {
                var iteration = sr.IterationResults[i];
                var itStatus = iteration.Passed ? TestStatus.Passed : TestStatus.Failed;
                var itIcon = BrandHtml.TestStatusIcon(itStatus);

                b.AppendLine("<tr>");
                b.Append($@"<td>→ {itIcon} Iteration #{i + 1}");
                var inputDataString = iteration.InputData?.ToString();
                var inputDataText = string.IsNullOrEmpty(inputDataString) ? "(empty)" : E(inputDataString);
                b.Append($" - Input: {inputDataText}");
                b.Append(" ");
                WriteIterationDetailsToggle(b, iteration);
                b.AppendLine("</td>");
                b.AppendLine($@"<td>{BrandHtml.StatusPill(itStatus)}</td>");
                b.AppendLine($"<td>{iteration.Duration().ToTestFuznDurationHtml()}</td>");
                b.AppendLine("</tr>");

                foreach (var stepResult in iteration.StepResults)
                {
                    WriteStepResult(b, stepResult.Value, 1);
                }
            }
        }
        b.AppendLine("</table>");
    }

    private static void WriteIterationDetailsToggle(StringBuilder b, IterationResult iteration)
    {
        b.Append(@"<details class=""link-toggle"" style=""display:inline""><summary><span class=""t-show"">Show details</span><span class=""t-hide"">Hide details</span></summary>");
        b.Append(@"<table class=""inner"">");
        b.Append($@"<tr><th class=""vertical"">CorrelationId</th><td>{E(iteration.CorrelationId)}</td></tr>");
        b.AppendLine("</table>");
        b.AppendLine(@"<table class=""inner"">");
        b.AppendLine("<tr><th>Phase</th><th>Duration</th><th>Started</th><th>Ended</th></tr>");
        b.AppendLine($"<tr><td>Init</td><td>{iteration.InitDuration().ToTestFuznDurationHtml()}</td><td>{iteration.InitStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td><td>{iteration.InitEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td></tr>");
        b.AppendLine($"<tr><td>Execution</td><td>{iteration.ExecuteDuration().ToTestFuznDurationHtml()}</td><td>{iteration.ExecuteStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td><td>{iteration.ExecuteEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td></tr>");
        b.AppendLine($"<tr><td>Cleanup</td><td>{iteration.CleanupDuration().ToTestFuznDurationHtml()}</td><td>{iteration.CleanupStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td><td>{iteration.CleanupEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td></tr>");
        b.AppendLine($"<tr><td>Total Iteration Run</td><td>{iteration.Duration().ToTestFuznDurationHtml()}</td><td>{iteration.InitStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td><td>{iteration.CleanupEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td></tr>");
        b.AppendLine("</table></details>");
    }

    private void WriteStepResult(StringBuilder b, StepStandardResult stepResult, int level)
    {
        var leftPadding = 24 + ((level - 1) * 20);

        var icon = BrandHtml.StepStatusIcon(stepResult.Status);
        var prefix = $"→ {icon}";
        var hiddenPrefix = $@"<span style=""visibility:hidden"">→ {icon}</span>";

        b.AppendLine("<tr>");
        b.AppendLine($"<td style=\"padding-left:{leftPadding}px\">{prefix} Step: {E(stepResult.Name)}");

        if (!string.IsNullOrEmpty(stepResult.Id) && !stepResult.IsIdAutomatic)
        {
            b.AppendLine($@"<br/>{hiddenPrefix} <span class=""step-meta"">Id: {E(stepResult.Id)}</span>");
        }

        if (stepResult.Comments != null && stepResult.Comments.Count > 0)
        {
            foreach (var comment in stepResult.Comments)
            {
                b.AppendLine($"<br/>{hiddenPrefix} // {E(comment.Text)}");
            }
        }

        if (stepResult.Attachments != null && stepResult.Attachments.Count > 0)
        {
            foreach (var attachment in stepResult.Attachments)
            {
                var fileName = Path.GetFileName(attachment.Path);
                b.AppendLine($"<br/>{hiddenPrefix} Attachment: <a href=\"Data/Attachments/{E(fileName)}\" target=\"_blank\">{E(attachment.Name)}</a>");
            }
        }

        if (stepResult.Status == StepStatus.Failed && stepResult.Exception != null)
        {
            WriteFailure(b, stepResult.Exception, hiddenPrefix);
        }

        b.AppendLine("</td>");
        b.AppendLine($@"<td>{BrandHtml.StepStatusPill(stepResult.Status)}</td>");
        b.AppendLine($"<td>{stepResult.Duration.ToTestFuznDurationHtml()}</td>");
        b.AppendLine("</tr>");

        if (stepResult.StepResults != null && stepResult.StepResults.Count > 0)
        {
            foreach (var subStep in stepResult.StepResults)
            {
                WriteStepResult(b, subStep, level + 1);
            }
        }
    }

    private static void WriteFailure(StringBuilder b, Exception exception, string hiddenPrefix)
    {
        b.AppendLine($@"<br/>{hiddenPrefix} <div class=""step-failure"">");
        WriteExceptionBody(b, exception);
        b.AppendLine("</div>");
    }

    private static void WriteExceptionBody(StringBuilder b, Exception exception)
    {
        b.AppendLine($"<strong>{E(exception.GetType().FullName)}</strong>: {E(exception.Message)}");
        if (exception.StackTrace != null)
        {
            b.AppendLine($"<br/>{E(exception.StackTrace)}");
        }
        if (exception.InnerException != null)
        {
            b.AppendLine("<br/><br/><strong>Inner Exception:</strong><br/>");
            WriteExceptionBody(b, exception.InnerException);
        }
    }

}
