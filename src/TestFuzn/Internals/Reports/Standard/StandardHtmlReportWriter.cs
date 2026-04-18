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
        b.AppendLine("<link rel='stylesheet' href='Data/Assets/styles/testfuzn.css'>");
        b.AppendLine("<script src='Data/Assets/scripts/chart.js'></script>");
        b.AppendLine("<style>");
        b.AppendLine(".group-results td, .group-results th { padding: 4px 8px; line-height: 1.3; }");
        b.AppendLine(".group-results tr.group td { padding-top: 10px; }");
        b.AppendLine("details.link-toggle summary { display: inline; list-style: none; cursor: pointer; color: #0645ad; text-decoration: underline; font-weight: normal; }");
        b.AppendLine("details.link-toggle summary::-webkit-details-marker { display: none; }");
        b.AppendLine("details.link-toggle summary::marker { display: none; }");
        b.AppendLine("details.link-toggle summary .t-hide { display: none; }");
        b.AppendLine("details.link-toggle[open] summary .t-show { display: none; }");
        b.AppendLine("details.link-toggle[open] summary .t-hide { display: inline; }");
        b.AppendLine("</style>");
        b.AppendLine("</head>");
        b.AppendLine("<body>");
        b.AppendLine(@"<div class=""page-container"">");

        b.AppendLine($"<h1>{E(reportData.Suite.Name)} - Test Report</h1>");

        WriteRunInfo(reportData, b);

        WriteDashboard(b, testsTotal, testsPassed, testsFailed, testsSkipped, passRate);

        WriteGroupResults(reportData, b);

        WriteChartScript(b, testsPassed, testsFailed, testsSkipped);

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
            b.AppendLine(@"<div class=""icon"">ℹ️</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">No Tests Executed</div>");
            b.AppendLine("</div></div>");
            return;
        }
        
        if (testsFailed == 0)
        {
            b.AppendLine(@"<div class=""overall-status passed"">");
            b.AppendLine(@"<div class=""icon"">✅</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine($@"<div class=""title"">All Tests Passed</div>");
            b.AppendLine("</div></div>");
        }
        else
        {
            b.AppendLine(@"<div class=""overall-status failed"">");
            b.AppendLine(@"<div class=""icon"">❌</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine($@"<div class=""title"">{testsFailed} Test{(testsFailed > 1 ? "s" : "")} Failed</div>");
            b.AppendLine("</div></div>");
        }

        b.AppendLine(@"<div class=""dashboard"">");

        // Donut Chart
        b.AppendLine(@"<div class=""chart-container"">");
        b.AppendLine(@"<div class=""chart-title"">Results Distribution</div>");
        b.AppendLine(@"<div class=""chart-wrapper"">");
        b.AppendLine(@"<canvas id=""statusChart""></canvas>");
        b.AppendLine($@"<div class=""chart-center-text"">");
        b.AppendLine($@"<div class=""rate"">{passRate}%</div>");
        b.AppendLine($@"<div class=""label"">Pass Rate</div>");
        b.AppendLine("</div>");
        b.AppendLine("</div>");
        b.AppendLine(@"<div class=""legend-container"">");
        b.AppendLine(@"<div class=""legend-item""><div class=""legend-color passed""></div>Passed</div>");
        b.AppendLine(@"<div class=""legend-item""><div class=""legend-color failed""></div>Failed</div>");
        b.AppendLine(@"<div class=""legend-item""><div class=""legend-color skipped""></div>Skipped</div>");
        b.AppendLine("</div>");
        b.AppendLine("</div>");

        // Stats Cards
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
        b.AppendLine("function toggleDetails(link) {");
        b.AppendLine("    var details = link.closest('tr').querySelector('details.results');");
        b.AppendLine("    if (details) {");
        b.AppendLine("        details.open = !details.open;");
        b.AppendLine("        link.textContent = details.open ? 'Hide' : 'Show';");
        b.AppendLine("    }");
        b.AppendLine("}");
        b.AppendLine("document.addEventListener('DOMContentLoaded', function() {");
        
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
            foreach (var metadata in reportData.Suite.Metadata)
            {
                b.AppendLine(@"<div class=""run-info-row"">");
                b.AppendLine($@"<div class=""info-item""><span class=""info-label"">{E(metadata.Key)}</span><span class=""info-value"">{E(metadata.Value)}</span></div>");
                b.AppendLine("</div>");
            }
        }

        b.AppendLine(@"<div class=""run-info-row"">");
        b.AppendLine(@"<div class=""info-item""><a href=""TestFuzn_Log.log"" target=""_blank"">View Log</a></div>");
        b.AppendLine("</div>");

        b.AppendLine("</div>");
    }

    private void WriteGroupResults(StandardReportData reportData, StringBuilder b)
    {
        b.Append(@"<table class=""group-results"">");
        b.AppendLine($"<tr>");
        b.AppendLine($"<th>Test</th>");
        b.AppendLine(@$"<th>Details</th>");
        b.AppendLine(@$"<th>Type</th>");
        b.AppendLine(@$"<th>Status</th>");
        b.AppendLine(@$"<th>Duration</th>");
        b.AppendLine(@$"<th>Tags</th>");
        b.AppendLine($"</tr>");
        foreach (var groupResult in reportData.GroupResults.OrderBy(f => f.Value.Name))
        {
            var symbol = "";
            var statusText = "";

            switch (groupResult.Value.Status)
            {
                case TestStatus.Passed: 
                    symbol = "✅";
                    statusText = "Passed";
                    break;
                case TestStatus.Failed:
                     symbol = "❌";
                    statusText = "Failed";
                    break;
                case TestStatus.Skipped:
                    symbol = "⚠️";
                    statusText = "Skipped";
                    break;
            }

            var groupTotal = groupResult.Value.TestResults.Count;
            var groupPassed = groupResult.Value.TestResults.Count(t => t.Value.Status == TestStatus.Passed);
            var groupFailed = groupResult.Value.TestResults.Count(t => t.Value.Status == TestStatus.Failed);
            var groupSkipped = groupResult.Value.TestResults.Count(t => t.Value.Status == TestStatus.Skipped);

            b.AppendLine(@$"<tr class=""group"">");
            b.AppendLine($"<td>{symbol} {E(groupResult.Value.Name)}<br/><span style=\"visibility:hidden\">{symbol}</span> <span style=\"font-size:smaller;opacity:0.7\">{groupTotal} total: {groupPassed} passed, {groupFailed} failed, {groupSkipped} skipped</span></td>");
            b.AppendLine($"<td></td>");
            b.AppendLine($"<td></td>");
            b.AppendLine($"<td>{statusText}</td>");
            b.AppendLine($"<td></td>");
            b.AppendLine($"<td></td>");
            b.AppendLine($"</tr>");

            WriteTestResults(b, groupResult);
        }

        b.Append("</table>");
    }

    private void WriteTestResults(StringBuilder b, KeyValuePair<string, GroupResult> groupResults)
    {
        foreach (var testResult in groupResults.Value.TestResults.OrderBy(t => t.Value.Name))
        {
            var symbol = "";
            var statusText = "";
            switch (testResult.Value.Status)
            {
                case TestStatus.Passed:
                    symbol = "→ ✅ ";
                    statusText = "Passed";
                    break;
                case TestStatus.Failed:
                    symbol = "→ ❌";
                    statusText = "Failed";
                    break;
                case TestStatus.Skipped:
                    symbol = "→ ⚠️";
                    statusText = "Skipped";
                    break;
            }

            var typeText = testResult.Value.Status == TestStatus.Skipped
                ? "N/A"
                : testResult.Value.TestType == TestType.Load ? "Load" : "Standard";

            var detailsLinks = new List<string>();
            if (testResult.Value.Status != TestStatus.Skipped)
            {
                detailsLinks.Add(@"<a href=""#"" class=""toggle-details"" onclick=""toggleDetails(this); return false;"">Show</a>");
            }
            if (testResult.Value.TestType == TestType.Load && testResult.Value.Status != TestStatus.Skipped)
            {
                var reportName = FileNameHelper.MakeFilenameSafe($"{groupResults.Value.Name}-{testResult.Value.Name}");
                detailsLinks.Add($"<a href=\"Data/{E(reportName)}.html\">Report</a>");
            }
            var detailsLink = string.Join(" | ", detailsLinks);

            b.AppendLine($"<tr>");
            b.AppendLine(@$"<td style=""padding-left:30px"">{symbol} {E(testResult.Value.Name)}");

            WriteTestDetails(b, testResult.Value);

            b.AppendLine("</td>");
            b.AppendLine($"<td>{detailsLink}</td>");
            b.AppendLine($"<td>{typeText}</td>");
            b.AppendLine($"<td>{statusText}</td>");
            b.AppendLine($"<td>{testResult.Value.TestRunDuration().ToTestFuznReadableString()}</td>");
            b.AppendLine("<td>");
            if (testResult.Value.Tags != null && testResult.Value.Tags.Count > 0)
            {
                foreach (var tag in testResult.Value.Tags)
                    b.AppendLine($"{E(tag)}<br/>");
            }
            b.AppendLine("</td>");
            b.AppendLine($"</tr>");
        }
    }

    private void WriteTestDetails(StringBuilder b, TestResult sr)
    {
        if (sr.Status == TestStatus.Skipped) return;

        b.AppendLine(@"<details class=""results"">");
        b.AppendLine(@"<summary style=""display:none""></summary>");

        var showCorrelationId = !sr.HasInputData && sr.IterationResults.Count > 0;
        var useToggle = sr.TestType != TestType.Load;

        if (!string.IsNullOrEmpty(sr.Description))
            b.AppendLine($@"<div style=""margin:6px 0"">{E(sr.Description)}</div>");

        if (useToggle)
            b.AppendLine(@"<details class=""link-toggle"" style=""margin:6px 0 20px 0""><summary><span class=""t-show"">Show details</span><span class=""t-hide"">Hide details</span></summary>");

        var hasInfoRows = !string.IsNullOrEmpty(sr.Id)
            || showCorrelationId
            || (sr.Metadata != null && sr.Metadata.Count > 0);

        if (hasInfoRows)
        {
            b.AppendLine(@"<table style=""margin:6px 0"">");
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

        b.AppendLine(@"<table style=""margin:6px 0"">");
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
        b.AppendLine(@"<table class=""iterations"" style=""margin:6px 0;border-collapse:collapse;width:100%"">");
        b.AppendLine(@"<tr style=""background:#eee;text-align:left"">");
        b.AppendLine($@"<th style=""padding:4px 8px"">{(sr.HasInputData ? "Iteration / Step" : "Step")}</th>");
        b.AppendLine(@"<th style=""padding:4px 8px;width:110px"">Status</th>");
        b.AppendLine(@"<th style=""padding:4px 8px;width:90px"">Duration</th>");
        b.AppendLine(@"</tr>");

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
                var symbol = iteration.Passed ? "→ ✅" : "→ ❌";
                var statusText = iteration.Passed ? "✅ Passed" : "❌ Failed";

                var rowStyle = i == 0
                    ? "background:#fafafa"
                    : "background:#fafafa;border-top:2px solid #ddd";
                b.AppendLine($@"<tr style=""{rowStyle}"">");
                b.Append($"<td style=\"padding:4px 8px\">{symbol} Iteration #{i}");
                var inputDataString = iteration.InputData?.ToString();
                var inputDataText = string.IsNullOrEmpty(inputDataString) ? "(empty)" : E(inputDataString);
                b.Append($" - Input: {inputDataText}");
                b.Append(" ");
                WriteIterationDetailsToggle(b, iteration);
                b.AppendLine("</td>");
                b.AppendLine($"<td style=\"padding:4px 8px\">{statusText}</td>");
                b.AppendLine($"<td style=\"padding:4px 8px\">{iteration.Duration().ToTestFuznResponseTime()}</td>");
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
        b.Append(@"<table style=""margin:6px 0;font-weight:normal"">");
        b.Append($@"<tr><th class=""vertical"">CorrelationId</th><td>{E(iteration.CorrelationId)}</td></tr>");
        b.AppendLine("</table>");
        b.AppendLine(@"<table style=""margin:6px 0;font-weight:normal"">");
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

        var symbol = "";
        var hiddenSymbol = "";
        var statusText = "";
        switch (stepResult.Status)
        {
            case StepStatus.Passed:
                symbol = "→ ✅";
                hiddenSymbol = @"<span style=""visibility:hidden"">→ ✅</span>";
                statusText = "✅ Passed";
                break;
            case StepStatus.Failed:
                symbol = "→ ❌";
                hiddenSymbol = @"<span style=""visibility:hidden"">→ ❌</span>";
                statusText = "❌ Failed";
                break;
            case StepStatus.Skipped:
                symbol = "→ ⚠️";
                hiddenSymbol = @"<span style=""visibility:hidden"">→ ⚠️</span>";
                statusText = "⚠️ Skipped";
                break;
        }

        b.AppendLine($"<tr>");
        b.AppendLine($"<td style=\"padding:4px 8px;padding-left:{leftPadding}px\">{symbol} Step: {E(stepResult.Name)}");

        if (!string.IsNullOrEmpty(stepResult.Id))
        {
            b.AppendLine($"<br/>{hiddenSymbol} <span style=\"font-size:smaller;opacity:0.7\">Id: {E(stepResult.Id)}</span>");
        }

        if (stepResult.Comments != null && stepResult.Comments.Count > 0)
        {
            foreach (var comment in stepResult.Comments)
            {
                b.AppendLine($"<br/>{hiddenSymbol} // {E(comment.Text)}");
            }
        }

        if (stepResult.Attachments != null && stepResult.Attachments.Count > 0)
        {
            foreach (var attachment in stepResult.Attachments)
            {
                var fileName = Path.GetFileName(attachment.Path);
                b.AppendLine($"<br/>{hiddenSymbol} Attachment: <a href=\"Data/Attachments/{E(fileName)}\" target=\"_blank\">{E(attachment.Name)}</a>");
            }
        }

        if (stepResult.Status == StepStatus.Failed && stepResult.Exception != null)
        {
            WriteFailure(b, stepResult.Exception, hiddenSymbol);
        }

        b.AppendLine("</td>");
        b.AppendLine($"<td style=\"padding:4px 8px\">{statusText}</td>");
        b.AppendLine($"<td style=\"padding:4px 8px\">{stepResult.Duration.ToTestFuznResponseTime()}</td>");
        b.AppendLine($"</tr>");

        if (stepResult.StepResults != null && stepResult.StepResults.Count > 0)
        {
            foreach (var subStep in stepResult.StepResults)
            {
                WriteStepResult(b, subStep, level + 1);
            }
        }
    }

    private static void WriteFailure(StringBuilder b, Exception exception, string hiddenSymbol)
    {
        b.AppendLine($"<br/>{hiddenSymbol} <div style=\"display:inline-block;margin-top:6px;padding:8px 10px;background:#fee;border-left:3px solid #EF4444;font-family:Consolas,Menlo,monospace;font-size:0.85em;white-space:pre-wrap\">");
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
