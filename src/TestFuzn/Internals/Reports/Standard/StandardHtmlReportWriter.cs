using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Reports.EmbeddedResources;
using System.Text;

namespace Fuzn.TestFuzn.Internals.Reports.Standard;

internal class StandardHtmlReportWriter : IStandardReport
{
    public async Task WriteReport(StandardReportData reportData)
    {
        try
        {
            await IncludeEmbeddedResources(reportData);

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
        var testsExecuted = testsPassed + testsFailed;
        var passRate = testsExecuted > 0 ? Math.Round((double)testsPassed / testsExecuted * 100, 1) : 0;

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

        b.AppendLine($"<h1>{reportData.Suite.Name} - Test Report</h1>");

        WriteDashboard(reportData, b, testsTotal, testsPassed, testsFailed, testsSkipped, passRate);

        WriteTestInfo(reportData, b);

        WriteGroupResults(reportData, b);

        WriteChartScript(b, testsPassed, testsFailed, testsSkipped);

        b.AppendLine("</div>");
        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
    }

    private static void WriteDashboard(StandardReportData reportData, StringBuilder b, 
        int testsTotal, int testsPassed, int testsFailed, int testsSkipped, double passRate)
    {
        b.AppendLine("<h2>Test Summary</h2>");

        if (testsTotal == 0)
        {
            b.AppendLine(@"<div class=""overall-status no-tests"">");
            b.AppendLine(@"<div class=""icon"">ℹ️</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">No Tests Executed</div>");
            b.AppendLine(@"<div class=""subtitle"">No test results were found in this run</div>");
            b.AppendLine("</div></div>");
            return;
        }
        
        if (testsFailed == 0)
        {
            b.AppendLine(@"<div class=""overall-status passed"">");
            b.AppendLine(@"<div class=""icon"">✅</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine($@"<div class=""title"">All Tests Passed</div>");
            b.AppendLine($@"<div class=""subtitle"">{testsPassed} of {testsTotal} tests completed successfully</div>");
            b.AppendLine("</div></div>");
        }
        else
        {
            b.AppendLine(@"<div class=""overall-status failed"">");
            b.AppendLine(@"<div class=""icon"">❌</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine($@"<div class=""title"">{testsFailed} Test{(testsFailed > 1 ? "s" : "")} Failed</div>");
            var testsExecuted = testsPassed + testsFailed;
            b.AppendLine($@"<div class=""subtitle"">{testsPassed} of {testsExecuted} executed tests passed ({passRate}% pass rate){(testsSkipped > 0 ? $", {testsSkipped} skipped" : "")}</div>");
            b.AppendLine("</div></div>");
        }

        b.AppendLine(@"<div class=""dashboard"">");

        // Donut Chart
        b.AppendLine(@"<div class=""chart-container"">");
        b.AppendLine(@"<h3>Test Results Distribution</h3>");
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
        b.AppendLine($@"<div class=""percentage"">Duration: {reportData.TestRunDuration.ToTestFuznReadableString()}</div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""stat-card passed"">");
        b.AppendLine($@"<div class=""value"">{testsPassed}</div>");
        b.AppendLine(@"<div class=""label"">Passed</div>");
        var passedPct = testsTotal > 0 ? Math.Round((double)testsPassed / testsTotal * 100, 1) : 0;
        b.AppendLine($@"<div class=""percentage"">{passedPct}% of total</div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""stat-card failed"">");
        b.AppendLine($@"<div class=""value"">{testsFailed}</div>");
        b.AppendLine(@"<div class=""label"">Failed</div>");
        var failedPct = testsTotal > 0 ? Math.Round((double)testsFailed / testsTotal * 100, 1) : 0;
        b.AppendLine($@"<div class=""percentage"">{failedPct}% of total</div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""stat-card skipped"">");
        b.AppendLine($@"<div class=""value"">{testsSkipped}</div>");
        b.AppendLine(@"<div class=""label"">Skipped</div>");
        var skippedPct = testsTotal > 0 ? Math.Round((double)testsSkipped / testsTotal * 100, 1) : 0;
        b.AppendLine($@"<div class=""percentage"">{skippedPct}% of total</div>");
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

    private static void WriteTestInfo(StandardReportData reportData, StringBuilder b)
    {
        b.AppendLine($"<h2>Test Info</h2>");
        b.AppendLine("<table>");
        b.AppendLine(@"<tr><th class=""vertical"">Suite - Name</th><td>" + reportData.Suite.Name + "</td></tr>");
        b.AppendLine(@"<tr><th class=""vertical"">Suite - ID</th><td>" + reportData.Suite.Id + "</td></tr>");

        if (reportData.Suite.Metadata != null)
        {
            b.AppendLine(@$"<tr><th class=""vertical"">Suite - Metadata</th><td><ul>");
            foreach (var metadata in reportData.Suite.Metadata)
            {
                b.AppendLine(@$"<li>{metadata.Key}: {metadata.Value}</li>");
            }
            b.AppendLine(@$"</tr>");
        }

        b.AppendLine(@$"<tr><th class=""vertical"">Run ID</th><td>{reportData.TestRunId}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Execution Environment</th><td>{(string.IsNullOrEmpty(GlobalState.ExecutionEnvironment) ? "-" : GlobalState.ExecutionEnvironment)}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Target Environment</th><td>{(string.IsNullOrEmpty(GlobalState.TargetEnvironment) ? "-" : GlobalState.TargetEnvironment)}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Start Time</th><td>{reportData.TestRunStartTime.ToLocalTime()}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">End Time</th><td>{reportData.TestRunEndTime.ToLocalTime()}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Duration</th><td>{reportData.TestRunDuration.ToTestFuznReadableString()}</td></tr>");
        b.AppendLine("</table>");
    }

    private void WriteGroupResults(StandardReportData reportData, StringBuilder b)
    {
        b.AppendLine($"<h2>Test Results</h2>");
        
        b.Append(@"<table class=""group-results"">");
        b.AppendLine($"<tr>");
        b.AppendLine($"<th>Details</th>");
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
            b.AppendLine($"<td>{symbol} {groupResult.Value.Name}</td>");
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
            b.AppendLine(@$"<td style=""padding-left:30px"">{symbol} {testResult.Value.Name}");
            var sr = testResult.Value.ScenarioResult;
            if (sr != null)
                WriteScenarioDetails(b, sr);
            b.AppendLine("</td>");
            b.AppendLine($"<td>{statusText}</td>");
            b.AppendLine($"<td>{testResult.Value.Duration.ToTestFuznReadableString()}</td>");
            b.AppendLine("<td>");
            if (testResult.Value.Tags != null && testResult.Value.Tags.Count > 0)
            {
                foreach (var tag in testResult.Value.Tags)
                    b.AppendLine($"{tag}<br/>");
            }
            b.AppendLine("</td>");
            b.AppendLine($"</tr>");
        }
    }

    private void WriteScenarioDetails(StringBuilder b, ScenarioStandardResult sr)
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

    private void WriteStepDetails(StringBuilder b, ScenarioStandardResult sr)
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
                b.AppendLine($"{(string.IsNullOrEmpty(iteration.InputData) ? " " : iteration.InputData)}");
                b.AppendLine($"<br/>{hiddenSymbol} CorrelationId: {iteration.CorrelationId}");
                b.AppendLine($"</th>");
                b.AppendLine($"<th>{statusText}</th>");
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
            b.AppendLine($"<td>{symbol} Step: {stepResult.Name}");
        else
            b.AppendLine($"<td style='padding-left:{padding}px'>{symbol} Step: {stepResult.Name}");

        if (stepResult.Comments != null && stepResult.Comments.Count > 0)
        {
            foreach (var comment in stepResult.Comments)
            {
                b.AppendLine($"<br/>{hiddenSymbol} // {comment.Text}");
            }
        }

        if (stepResult.Attachments != null && stepResult.Attachments.Count > 0)
        {
            foreach (var attachment in stepResult.Attachments)
            {
                var fileName = Path.GetFileName(attachment.Path);
                b.AppendLine($"<br/>{hiddenSymbol} Attachment: <a href=\"Attachments/{fileName}\" target=\"_blank\">{attachment.Name}</a>");
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

    private async Task IncludeEmbeddedResources(StandardReportData reportData)
    {
        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(
            "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Styles.testfuzn.css",
            Path.Combine(reportData.TestsOutputDirectory, "assets/styles/testfuzn.css"));

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(
            "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Scripts.chart.js",
            Path.Combine(reportData.TestsOutputDirectory, "assets/scripts/chart.js"));
    }
}
