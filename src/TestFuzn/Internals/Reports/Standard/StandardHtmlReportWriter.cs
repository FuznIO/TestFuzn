using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using System.Net;
using System.Text;

namespace Fuzn.TestFuzn.Internals.Reports.Standard;

internal class StandardHtmlReportWriter : IStandardReport
{
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public async Task WriteReport(StandardReportData reportData)
    {
        try
        {
            var filePath = Path.Combine(reportData.TestsOutputDirectory, "TestReport.html");

            var htmlContent = GenerateHtmlReport(reportData);

            await File.WriteAllTextAsync(filePath, htmlContent);
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
        b.AppendLine("<link rel='stylesheet' href='assets/styles/testfuzn.css'>");
        b.AppendLine("<script src='assets/scripts/chart.js'></script>");
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

    private static void WriteRunInfo(StandardReportData reportData, StringBuilder b)
    {
        b.AppendLine(@"<div class=""run-info"">");
        
        b.AppendLine(@"<div class=""run-info-row"">");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Run ID</span><span class=""info-value"">" + E(reportData.TestRunId) + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Duration</span><span class=""info-value"">" + E(reportData.TestRunDuration.ToTestFuznReadableString()) + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Started</span><span class=""info-value"">" + reportData.TestRunStartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Completed</span><span class=""info-value"">" + reportData.TestRunEndTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "</span></div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""run-info-row"">");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Execution Environment</span><span class=""info-value"">" + (string.IsNullOrEmpty(GlobalState.ExecutionEnvironment) ? "-" : E(GlobalState.ExecutionEnvironment)) + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Target Environment</span><span class=""info-value"">" + (string.IsNullOrEmpty(GlobalState.TargetEnvironment) ? "-" : E(GlobalState.TargetEnvironment)) + "</span></div>");

        if (reportData.Suite.Metadata != null && reportData.Suite.Metadata.Count > 0)
        {
            foreach (var metadata in reportData.Suite.Metadata)
            {
                b.AppendLine($@"<div class=""info-item""><span class=""info-label"">{E(metadata.Key)}</span><span class=""info-value"">{E(metadata.Value)}</span></div>");
            }
        }
        b.AppendLine("</div>");

        b.AppendLine("</div>");
    }

    private void WriteGroupResults(StandardReportData reportData, StringBuilder b)
    {
        b.Append(@"<table class=""group-results"">");
        b.AppendLine($"<tr>");
        b.AppendLine($"<th>Test</th>");
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

            b.AppendLine(@$"<tr class=""group"">");
            b.AppendLine($"<td>{symbol} {E(groupResult.Value.Name)}</td>");
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

            b.AppendLine($"<tr>");
            b.AppendLine(@$"<td style=""padding-left:30px"">{symbol} {E(testResult.Value.Name)}");
            
            WriteScenarioDetails(b, testResult.Value);

            b.AppendLine("</td>");
            b.AppendLine($"<td>{statusText}</td>");
            b.AppendLine($"<td>{testResult.Value.Duration.ToTestFuznReadableString()}</td>");
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

    private void WriteScenarioDetails(StringBuilder b, TestResult sr)
    {
        if (sr.Status != TestStatus.Skipped
            && sr.IterationResults.Count > 0)
        {
            b.AppendLine(@"<details class=""results"">");
            b.AppendLine("<summary></summary>");
        }

        if (sr.IterationResults.Count > 0)
            WriteStepDetails(b, sr);

        if (sr.Status != TestStatus.Skipped
            && sr.IterationResults.Count > 0)
            b.AppendLine("</details>");
    }

    private void WriteStepDetails(StringBuilder b, TestResult sr)
    {
        b.AppendLine(@"<table style=""margin:30px;0;30px;0;"">");

        foreach (var iteration in sr.IterationResults)
        {
            if (sr.HasInputData)
            {
                var symbol = "";
                var hiddenSymbol = "";
                var statusText = "";
                if (iteration.Passed)
                {
                    symbol = "→ ✅";
                    hiddenSymbol = @"<span style=""visibility:hidden"">→ ✅</span>";
                    statusText = "✅ Passed";
                }
                else
                {
                    symbol = "→ ❌";
                    hiddenSymbol = @"<span style=""visibility:hidden"">→ ❌</span>";
                    statusText = "❌ Failed";
                }

                b.AppendLine($"<tr>");
                b.AppendLine($"<th>{symbol} Input Data: ");
                b.AppendLine($"{(string.IsNullOrEmpty(iteration.InputData) ? " " : E(iteration.InputData))}");
                b.AppendLine($"<br/>{hiddenSymbol} CorrelationId: {E(iteration.CorrelationId)}");
                b.AppendLine($"</th>");
                b.AppendLine($"<th>{statusText}</th>");
                b.AppendLine($"<th></th>");
                b.AppendLine($"</tr>");
            }
            else
            {
                b.AppendLine($"<tr>");
                b.AppendLine($"<th>CorrelationId: {E(iteration.CorrelationId)}");
                b.AppendLine($"</th>");
                b.AppendLine($"<th></th>");
                b.AppendLine($"<th></th>");
                b.AppendLine($"</tr>");
            }

            foreach (var stepResult in iteration.StepResults)
            {
                WriteStepResult(b, stepResult.Value, 1);
            }
        }
        b.AppendLine("</table>");
    }

    private void WriteStepResult(StringBuilder b, StepStandardResult stepResult, int level)
    {
        var padding = ((30 * level) - 30);

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
        if (level == 1)
            b.AppendLine($"<td>{symbol} Step: {E(stepResult.Name)}");
        else
            b.AppendLine($"<td style='padding-left:{padding}px'>{symbol} Step: {E(stepResult.Name)}");

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
                b.AppendLine($"<br/>{hiddenSymbol} Attachment: <a href=\"Attachments/{E(fileName)}\" target=\"_blank\">{E(attachment.Name)}</a>");
            }
        }

        b.AppendLine("</td>");
        b.AppendLine($"<td>{statusText}</td>");
        b.AppendLine($"<td>{stepResult.Duration.ToTestFuznResponseTime()}</td>");
        b.AppendLine($"</tr>");

        if (stepResult.StepResults != null && stepResult.StepResults.Count > 0)
        {
            foreach (var subStep in stepResult.StepResults)
            {
                WriteStepResult(b, subStep, level + 1);
            }
        }
    }
}
