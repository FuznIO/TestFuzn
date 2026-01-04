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
            // Log or handle the exception as needed
            throw new InvalidOperationException("Failed to write HTML report.", ex);
        }
    }

    private string GenerateHtmlReport(StandardReportData reportData)
    {
        var b = new StringBuilder();

        b.AppendLine("<!DOCTYPE html>");
        b.AppendLine("<html lang='en'>");
        b.AppendLine("<head>");
        b.AppendLine("<meta charset='UTF-8'>");
        b.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        b.AppendLine("<title>TestFuzn - Feature Test Report</title>");
        b.AppendLine("<link rel='stylesheet' href='assets/styles/testfuzn.css'>");
        b.AppendLine("<script>");
        b.AppendLine("</script>");
        b.AppendLine("<style>");
        b.AppendLine("</style>");
        b.AppendLine("</head>");
        b.AppendLine("<body>");
        b.AppendLine(@"<div class=""page-container"">");

        // Header
        b.AppendLine($"<h1>{reportData.Suite.Name} - Feature Test Report</h1>");

        WriteTestInfo(reportData, b);

        WriteStatus(reportData, b);

        WriteGroupResults(reportData, b);

        b.AppendLine("</div>");
        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
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

    private static void WriteStatus(StandardReportData reportData, StringBuilder b)
    {
        var testsTotal = reportData.GroupResults.Sum(f => f.Value.TestResults.Count);
        var testsPassed = reportData.GroupResults.Sum(f => f.Value.TestResults.Count(s => s.Value.Status == TestStatus.Passed));
        var testsSkipped = reportData.GroupResults.Sum(f => f.Value.TestResults.Count(s => s.Value.Status == TestStatus.Skipped));
        var testsFailed = reportData.GroupResults.Sum(f => f.Value.TestResults.Count(s => s.Value.Status == TestStatus.Failed));

        b.AppendLine($"<h2>Test Status</h2>");
        if (testsTotal == 0)
        {
            b.AppendLine(@$"<div class=""status-panel passed"">");
            b.AppendLine(@$"<div class=""title"">✅ All tests passed</div>");
        }
        else
        {
            b.AppendLine(@$"<div class=""status-panel failed"">");
            b.AppendLine(@$"<div class=""title"">❌ {testsFailed} tests failed</div>");
        }

        b.AppendLine($@"<div class=""details"">");
        
        b.AppendLine("</div>");
        b.AppendLine("</div>");

        b.AppendLine("<table>");
        b.AppendLine("<tr>");
        b.AppendLine(@"<th class=""vertical"" style=""width:1%;white-space:nowrap"">Tests</th>");
        b.AppendLine($"<td style=\"width:1%;white-space:nowrap\">🔢 Total: {testsTotal}</td>");
        b.AppendLine($"<td style=\"width:1%;white-space:nowrap\">✅ Passed: {testsPassed}</td>");
        b.AppendLine($"<td style=\"width:1%;white-space:nowrap\">❌ Failed: {testsFailed}</td>");
        b.AppendLine($"<td>⚠️ Skipped: {testsSkipped}</td>");
        b.AppendLine("</tr>");
        b.AppendLine("</table>");
    }

    private void WriteGroupResults(StandardReportData reportData, StringBuilder b)
    {
        b.AppendLine($"<h2>Test Results</h2>");
        // Features
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

        //if (sr.Status != ScenarioStatus.Skipped)
        //    WriteScenarioInfo(b, sr);

        if (sr.IterationResults.Count > 0)
            WriteStepDetails(b, sr);

        if (sr.Status != TestStatus.Skipped
            && sr.IterationResults.Count > 0)
            b.AppendLine("</details>");
    }

    private void WriteStepDetails(StringBuilder b, ScenarioStandardResult sr)
    {
        b.AppendLine(@"<table style=""margin:30px;0;30px;0;"">");
        //b.AppendLine("<tr>");
        //b.AppendLine("<th>Input Data / Steps</th>");
        //b.AppendLine("<th>Status</th>");
        //b.AppendLine("<th>Duration</th>");
        //b.AppendLine("</tr>");

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

            //b.AppendLine($"<li>{(string.IsNullOrEmpty(iteration.InputData) ? " " : iteration.InputData)}</li>");
            //b.AppendLine($"<li>CorrelationId: {iteration.CorrelationId}</li>");
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
        //var symbol = level == 1 ? "" : " → ";
        //if (stepResult.Status == StepStatus.Passed)
        //    symbol += "✅";
        //else if (stepResult.Status == StepStatus.Failed)
        //    symbol += "❌";

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
