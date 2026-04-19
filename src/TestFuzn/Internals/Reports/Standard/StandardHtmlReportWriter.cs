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
        b.AppendLine("<script src='Data/Assets/scripts/chart.js'></script>");
        b.AppendLine("</head>");
        b.AppendLine("<body>");
        b.AppendLine(@"<div class=""page-container"">");

        BrandHtml.WriteMasthead(b, @"<a class=""masthead-link"" href=""TestFuzn_Log.log"" target=""_blank"">View Log ↗</a>");

        b.AppendLine($"<h1>{E(reportData.Suite.Name)} - Test Report</h1>");

        WriteDashboard(b, testsTotal, testsPassed, testsFailed, testsSkipped, passRate);

        WriteRunInfo(reportData, b);

        WriteGroupResults(reportData, b);

        WriteChartScript(b, testsPassed, testsFailed, testsSkipped);

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
            b.AppendLine(@"<div class=""title"">No Tests Executed</div>");
            b.AppendLine("</div></div>");
            return;
        }

        if (testsFailed == 0)
        {
            b.AppendLine(@"<div class=""overall-status passed"">");
            b.AppendLine($@"<div class=""icon"">{BrandHtml.TestStatusIconLarge(TestStatus.Passed)}</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">All Tests Passed</div>");
            b.AppendLine("</div></div>");
        }
        else
        {
            b.AppendLine(@"<div class=""overall-status failed"">");
            b.AppendLine($@"<div class=""icon"">{BrandHtml.TestStatusIconLarge(TestStatus.Failed)}</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine($@"<div class=""title"">{testsFailed} Test{(testsFailed > 1 ? "s" : "")} Failed</div>");
            b.AppendLine("</div></div>");
        }

        b.AppendLine(@"<div class=""dashboard"">");

        // Donut chart
        b.AppendLine(@"<div class=""chart-container"">");
        b.AppendLine(@"<div class=""chart-title"">Results Distribution</div>");
        b.AppendLine(@"<div class=""chart-wrapper"">");
        b.AppendLine(@"<canvas id=""statusChart""></canvas>");
        b.AppendLine(@"<div class=""chart-center-text"">");
        b.AppendLine($@"<div class=""rate"">{passRate}%</div>");
        b.AppendLine(@"<div class=""label"">Pass Rate</div>");
        b.AppendLine("</div>");
        b.AppendLine("</div>");
        b.AppendLine(@"<div class=""legend-container"">");
        b.AppendLine(@"<div class=""legend-item""><div class=""legend-color passed""></div>Passed</div>");
        b.AppendLine(@"<div class=""legend-item""><div class=""legend-color failed""></div>Failed</div>");
        b.AppendLine(@"<div class=""legend-item""><div class=""legend-color skipped""></div>Skipped</div>");
        b.AppendLine("</div>");
        b.AppendLine("</div>");

        // Stats cards
        b.AppendLine(@"<div class=""stats-grid"">");

        b.AppendLine(@"<div class=""stat-card total"">");
        b.AppendLine($@"<div class=""value"">{testsTotal}</div>");
        b.AppendLine(@"<div class=""label"">Total Tests</div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""stat-card passed"">");
        b.AppendLine($@"<div class=""value"">{testsPassed}</div>");
        b.AppendLine(@"<div class=""label"">Passed</div>");
        var passedPct = testsTotal > 0 ? Math.Round((double)testsPassed / testsTotal * 100, 1) : 0;
        b.AppendLine($@"<div class=""percentage"">{passedPct}%</div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""stat-card failed"">");
        b.AppendLine($@"<div class=""value"">{testsFailed}</div>");
        b.AppendLine(@"<div class=""label"">Failed</div>");
        var failedPct = testsTotal > 0 ? Math.Round((double)testsFailed / testsTotal * 100, 1) : 0;
        b.AppendLine($@"<div class=""percentage"">{failedPct}%</div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""stat-card skipped"">");
        b.AppendLine($@"<div class=""value"">{testsSkipped}</div>");
        b.AppendLine(@"<div class=""label"">Skipped</div>");
        var skippedPct = testsTotal > 0 ? Math.Round((double)testsSkipped / testsTotal * 100, 1) : 0;
        b.AppendLine($@"<div class=""percentage"">{skippedPct}%</div>");
        b.AppendLine("</div>");

        b.AppendLine("</div>"); // stats-grid
        b.AppendLine("</div>"); // dashboard
    }

    private static void WriteChartScript(StringBuilder b, int testsPassed, int testsFailed, int testsSkipped)
    {
        b.AppendLine("<script>");
        b.AppendLine("document.addEventListener('DOMContentLoaded', function() {");

        // Results filtering
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

        // Donut Chart
        b.AppendLine("    var statusCtx = document.getElementById('statusChart');");
        b.AppendLine("    if (statusCtx) {");
        b.AppendLine("        new Chart(statusCtx, {");
        b.AppendLine("            type: 'doughnut',");
        b.AppendLine("            data: {");
        b.AppendLine("                labels: ['Passed', 'Failed', 'Skipped'],");
        b.AppendLine("                datasets: [{");
        b.AppendLine($"                    data: [{testsPassed}, {testsFailed}, {testsSkipped}],");
        b.AppendLine("                    backgroundColor: ['#10B981', '#EF4444', '#F59E0B'],");
        b.AppendLine("                    borderWidth: 0,");
        b.AppendLine("                    hoverOffset: 4");
        b.AppendLine("                }]");
        b.AppendLine("            },");
        b.AppendLine("            options: {");
        b.AppendLine("                responsive: true,");
        b.AppendLine("                maintainAspectRatio: true,");
        b.AppendLine("                cutout: '70%',");
        b.AppendLine("                plugins: {");
        b.AppendLine("                    legend: { display: false },");
        b.AppendLine("                    tooltip: {");
        b.AppendLine("                        callbacks: {");
        b.AppendLine("                            label: function(context) {");
        b.AppendLine("                                var total = context.dataset.data.reduce(function(a, b) { return a + b; }, 0);");
        b.AppendLine("                                var percentage = ((context.raw / total) * 100).toFixed(1);");
        b.AppendLine("                                return context.label + ': ' + context.raw + ' (' + percentage + '%)';");
        b.AppendLine("                            }");
        b.AppendLine("                        }");
        b.AppendLine("                    }");
        b.AppendLine("                }");
        b.AppendLine("            }");
        b.AppendLine("        });");
        b.AppendLine("    }");

        b.AppendLine("});");
        b.AppendLine("</script>");
    }

    private void WriteRunInfo(StandardReportData reportData, StringBuilder b)
    {
        b.AppendLine(@"<div class=""run-info"">");

        b.AppendLine(@"<div class=""run-info-row"">");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Run ID</span><span class=""info-value"">" + E(reportData.TestRunId) + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Execution Environment</span><span class=""info-value"">" + (string.IsNullOrEmpty(_testSession.Configuration?.ExecutionEnvironment) ? "-" : E(_testSession.Configuration.ExecutionEnvironment)) + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Target Environment</span><span class=""info-value"">" + (string.IsNullOrEmpty(_testSession.Configuration?.TargetEnvironment) ? "-" : E(_testSession.Configuration.TargetEnvironment)) + "</span></div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""run-info-row"">");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Duration</span><span class=""info-value"">" + E(reportData.TestRunDuration.ToTestFuznReadableString()) + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Started</span><span class=""info-value"">" + reportData.TestRunStartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Ended</span><span class=""info-value"">" + reportData.TestRunEndTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "</span></div>");
        b.AppendLine("</div>");

        if (reportData.Suite.Metadata != null && reportData.Suite.Metadata.Count > 0)
        {
            b.AppendLine(@"<details class=""run-info-metadata"">");
            b.AppendLine(@"<summary><span class=""t-show"">Show metadata</span><span class=""t-hide"">Hide metadata</span></summary>");
            foreach (var metadata in reportData.Suite.Metadata)
            {
                b.AppendLine(@"<div class=""run-info-row"">");
                b.AppendLine($@"<div class=""info-item""><span class=""info-label"">{E(metadata.Key)}</span><span class=""info-value"">{E(metadata.Value)}</span></div>");
                b.AppendLine("</div>");
            }
            b.AppendLine("</details>");
        }

        b.AppendLine("</div>");
    }

    private static void WriteFilterBar(StandardReportData reportData, StringBuilder b)
    {
        var total = reportData.GroupResults.Sum(g => g.Value.TestResults.Count);
        var passed = reportData.GroupResults.Sum(g => g.Value.TestResults.Count(t => t.Value.Status == TestStatus.Passed));
        var failed = reportData.GroupResults.Sum(g => g.Value.TestResults.Count(t => t.Value.Status == TestStatus.Failed));
        var skipped = reportData.GroupResults.Sum(g => g.Value.TestResults.Count(t => t.Value.Status == TestStatus.Skipped));

        b.AppendLine(@"<div class=""results-filter"">");
        b.AppendLine(@"<div class=""results-filter-header"">");
        b.AppendLine(@"<span class=""results-filter-title"">Results</span>");
        b.AppendLine("</div>");
        b.AppendLine(@"<div class=""results-filter-controls"">");
        b.AppendLine(@"<div class=""status-pills"">");
        b.AppendLine($@"<button type=""button"" class=""pill active"" data-status-filter=""all"">All <span class=""count"">{total}</span></button>");
        b.AppendLine($@"<button type=""button"" class=""pill"" data-status-filter=""passed""><span class=""dot passed""></span>Passed <span class=""count"">{passed}</span></button>");
        b.AppendLine($@"<button type=""button"" class=""pill"" data-status-filter=""failed""><span class=""dot failed""></span>Failed <span class=""count"">{failed}</span></button>");
        b.AppendLine($@"<button type=""button"" class=""pill"" data-status-filter=""skipped""><span class=""dot skipped""></span>Skipped <span class=""count"">{skipped}</span></button>");
        b.AppendLine("</div>");
        b.AppendLine(@"<input type=""search"" id=""test-name-filter"" placeholder=""Filter by name..."" />");
        b.AppendLine("</div>");
        b.AppendLine("</div>");
    }

    private void WriteGroupResults(StandardReportData reportData, StringBuilder b)
    {
        WriteFilterBar(reportData, b);
        b.Append(@"<table class=""group-results"">");
        b.AppendLine("<tr>");
        b.AppendLine("<th>Test</th>");
        b.AppendLine("<th>Type</th>");
        b.AppendLine("<th>Status</th>");
        b.AppendLine("<th>Duration</th>");
        b.AppendLine("<th>Tags</th>");
        b.AppendLine("</tr>");
        foreach (var groupResult in reportData.GroupResults.OrderBy(f => f.Value.Name))
        {
            var icon = BrandHtml.TestStatusIcon(groupResult.Value.Status);

            var groupTotal = groupResult.Value.TestResults.Count;
            var groupPassed = groupResult.Value.TestResults.Count(t => t.Value.Status == TestStatus.Passed);
            var groupFailed = groupResult.Value.TestResults.Count(t => t.Value.Status == TestStatus.Failed);
            var groupSkipped = groupResult.Value.TestResults.Count(t => t.Value.Status == TestStatus.Skipped);

            var groupNameAttr = E(groupResult.Value.Name.ToLowerInvariant());
            b.AppendLine(@$"<tr class=""group"" data-group-row data-group-name=""{groupNameAttr}"">");
            b.AppendLine($@"<td colspan=""5"">{icon} {E(groupResult.Value.Name)}<span class=""group-summary"">{groupTotal} total: {groupPassed} passed, {groupFailed} failed, {groupSkipped} skipped</span></td>");
            b.AppendLine("</tr>");

            WriteTestResults(b, groupResult);
        }

        b.Append("</table>");
    }

    private void WriteTestResults(StringBuilder b, KeyValuePair<string, GroupResult> groupResults)
    {
        var groupNameAttr = E(groupResults.Value.Name.ToLowerInvariant());
        foreach (var testResult in groupResults.Value.TestResults.OrderBy(t => t.Value.Name))
        {
            var icon = BrandHtml.TestStatusIcon(testResult.Value.Status);
            var statusText = testResult.Value.Status switch
            {
                TestStatus.Passed => "Passed",
                TestStatus.Failed => "Failed",
                TestStatus.Skipped => "Skipped",
                _ => ""
            };
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
            b.AppendLine($@"<td><span class=""status-cell"">{icon}<span class=""status-text"">{statusText}</span></span></td>");
            b.AppendLine($"<td>{testResult.Value.TestRunDuration().ToTestFuznReadableString()}</td>");
            b.AppendLine("<td>");
            if (testResult.Value.Tags != null && testResult.Value.Tags.Count > 0)
            {
                foreach (var tag in testResult.Value.Tags)
                    b.AppendLine($"{E(tag)}<br/>");
            }
            b.AppendLine("</td>");
            b.AppendLine("</tr>");
        }
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
            b.AppendLine($@"<div><a href=""{reportLink}"">Load Test Report ↗</a></div>");

        if (!string.IsNullOrEmpty(sr.Description))
            b.AppendLine($"<div>{E(sr.Description)}</div>");

        if (useToggle)
            b.AppendLine(@"<details class=""link-toggle""><summary><span class=""t-show"">Show details</span><span class=""t-hide"">Hide details</span></summary>");

        var hasInfoRows = !string.IsNullOrEmpty(sr.Id)
            || showCorrelationId
            || (sr.Metadata != null && sr.Metadata.Count > 0);

        if (hasInfoRows)
        {
            b.AppendLine("<table>");
            if (!string.IsNullOrEmpty(sr.Id))
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

        b.AppendLine("<table>");
        b.AppendLine("<tr><th>Phase</th><th>Duration</th><th>Started</th><th>Ended</th></tr>");
        b.AppendLine($"<tr><td>Init</td><td>{sr.InitDuration().ToTestFuznResponseTime()}</td><td>{sr.InitStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td><td>{sr.InitEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td></tr>");
        b.AppendLine($"<tr><td>Execution</td><td>{sr.ExecuteDuration().ToTestFuznResponseTime()}</td><td>{sr.ExecuteStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td><td>{sr.ExecuteEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td></tr>");
        b.AppendLine($"<tr><td>Cleanup</td><td>{sr.CleanupDuration().ToTestFuznResponseTime()}</td><td>{sr.CleanupStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td><td>{sr.CleanupEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td></tr>");
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
        b.AppendLine(@"<table class=""iterations"">");
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
                var itStatusText = iteration.Passed ? "Passed" : "Failed";

                b.AppendLine("<tr>");
                b.Append($@"<td>→ {itIcon} Iteration #{i}");
                var inputDataString = iteration.InputData?.ToString();
                var inputDataText = string.IsNullOrEmpty(inputDataString) ? "(empty)" : E(inputDataString);
                b.Append($" - Input: {inputDataText}");
                b.Append(" ");
                WriteIterationDetailsToggle(b, iteration);
                b.AppendLine("</td>");
                b.AppendLine($@"<td><span class=""status-cell"">{itIcon}<span class=""status-text"">{itStatusText}</span></span></td>");
                b.AppendLine($"<td>{iteration.Duration().ToTestFuznResponseTime()}</td>");
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
        b.Append("<table>");
        b.Append($@"<tr><th class=""vertical"">CorrelationId</th><td>{E(iteration.CorrelationId)}</td></tr>");
        b.AppendLine("</table>");
        b.AppendLine("<table>");
        b.AppendLine("<tr><th>Phase</th><th>Duration</th><th>Started</th><th>Ended</th></tr>");
        b.AppendLine($"<tr><td>Init</td><td>{iteration.InitDuration().ToTestFuznResponseTime()}</td><td>{iteration.InitStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td><td>{iteration.InitEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td></tr>");
        b.AppendLine($"<tr><td>Execution</td><td>{iteration.ExecuteDuration().ToTestFuznResponseTime()}</td><td>{iteration.ExecuteStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td><td>{iteration.ExecuteEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td></tr>");
        b.AppendLine($"<tr><td>Cleanup</td><td>{iteration.CleanupDuration().ToTestFuznResponseTime()}</td><td>{iteration.CleanupStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td><td>{iteration.CleanupEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td></tr>");
        b.AppendLine($"<tr><td>Total Iteration Run</td><td>{iteration.Duration().ToTestFuznResponseTime()}</td><td>{iteration.InitStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td><td>{iteration.CleanupEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}</td></tr>");
        b.AppendLine("</table></details>");
    }

    private void WriteStepResult(StringBuilder b, StepStandardResult stepResult, int level)
    {
        var leftPadding = 24 + ((level - 1) * 20);

        var icon = BrandHtml.StepStatusIcon(stepResult.Status);
        var prefix = $"→ {icon}";
        var hiddenPrefix = $@"<span style=""visibility:hidden"">→ {icon}</span>";
        var statusText = stepResult.Status switch
        {
            StepStatus.Passed => "Passed",
            StepStatus.Failed => "Failed",
            StepStatus.Skipped => "Skipped",
            _ => ""
        };

        b.AppendLine("<tr>");
        b.AppendLine($"<td style=\"padding-left:{leftPadding}px\">{prefix} Step: {E(stepResult.Name)}");

        if (!string.IsNullOrEmpty(stepResult.Id))
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
        b.AppendLine($@"<td><span class=""status-cell"">{icon}<span class=""status-text"">{statusText}</span></span></td>");
        b.AppendLine($"<td>{stepResult.Duration.ToTestFuznResponseTime()}</td>");
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
