using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Feature;
using Fuzn.TestFuzn.Internals.Reports.EmbeddedResources;
using System.Reflection.Emit;
using System.Text;

namespace Fuzn.TestFuzn.Internals.Reports.Feature;

internal class FeatureHtmlReportWriter : IFeatureReport
{
    public async Task WriteReport(FeatureReportData featureReportData)
    {
        try
        {
            await IncludeEmbeddedResources(featureReportData);

            var filePath = Path.Combine(featureReportData.TestsOutputDirectory, "TestFuzn_Report_Features.html");

            var htmlContent = GenerateHtmlReport(featureReportData);

            await File.WriteAllTextAsync(filePath, htmlContent);
        }
        catch (Exception ex)
        {
            // Log or handle the exception as needed
            throw new InvalidOperationException("Failed to write HTML report.", ex);
        }
    }

    private string GenerateHtmlReport(FeatureReportData featureReportData)
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

        // Header
        b.AppendLine($"<h1>{featureReportData.TestSuite.Name} - Feature Test Report</h1>");

        b.AppendLine($"<h2>Test Suite Metadata</h2>");
        b.AppendLine("<table>");
        
        b.AppendLine("<tr><th>Name</th><td>" + featureReportData.TestSuite.Name + "</td></tr>");
        b.AppendLine("<tr><th>ID</th><td>" + featureReportData.TestSuite.Id + "</td></tr>");

        if (featureReportData.TestSuite.Metadata != null)
        {
            b.AppendLine("<tr><th>Metadata</th>");
            b.AppendLine("<td><table>");
            foreach (var metadata in featureReportData.TestSuite.Metadata)
            {
                b.AppendLine($"<tr><td>{metadata.Key}</td><td>{metadata.Value}</td></tr>");
            }
            b.AppendLine("</table></td></tr>");
        }
        
        b.AppendLine("</table>");

        b.AppendLine($"<h2>Test Run Info</h2>");
        b.AppendLine("<table>");
        b.AppendLine("<tbody>");
        b.AppendLine($"<tr><th>Run ID</th><td>{featureReportData.TestRunId}</td></tr>");
        b.AppendLine($"<tr><th>Start Time</th><td>{featureReportData.TestRunStartTime.ToLocalTime()}</td></tr>");
        b.AppendLine($"<tr><th>End Time</th><td>{featureReportData.TestRunEndTime.ToLocalTime()}</td></tr>");
        b.AppendLine($"<tr><th>Duration</th><td>{featureReportData.TestRunDuration.ToTestFuznFormattedDuration()}</td></tr>");

        // Summary
        var totalFeatures = featureReportData.Results.FeatureResults.Count;
        var totalScenarios = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count);
        var totalPassedScenarios = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count(s => s.Value.Status == ScenarioStatus.Passed));
        var totalSkippedScenarios = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count(s => s.Value.Status == ScenarioStatus.Skipped));
        var totalFailedScenarios = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count(s => s.Value.Status == ScenarioStatus.Failed));

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");

        b.AppendLine($"<h2>Test Results Summary</h2>");
        b.AppendLine("<table>");
        b.AppendLine("<tbody>");

        b.AppendLine($"<tr><td>Total Features</td><td>{totalFeatures}</td></tr>");
        b.AppendLine($"<tr><td>Total Scenario Tests</td><td>{totalScenarios}</td></tr>");
        b.AppendLine($"<tr><td>Passed Scenario Tests</td><td><span class='passed'>{totalPassedScenarios}</span></td></tr>");
        b.AppendLine($"<tr><td>Failed Scenario Tests</td><td><span class='failed'>{totalFailedScenarios}</span></td></tr>");
        b.AppendLine($"<tr><td>Skipped Scenario Tests</td><td><span class='skipped'>{totalSkippedScenarios}</span></td></tr>");

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");

        WriteFeatureResults(featureReportData, b);

        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
    }

    private void WriteFeatureResults(FeatureReportData featureReportData, StringBuilder b)
    {
        b.AppendLine($"<h2>Test Results Details</h2>");
        // Features
        b.Append("<table>");
        b.Append("<tbody>");
        b.AppendLine($"<tr>");
        b.AppendLine($"<th>Details</th>");
        b.AppendLine($"<th>Type</th>");
        b.AppendLine($"<th>Tags</th>");
        b.AppendLine($"<th>Duration</th>");
        b.AppendLine($"</tr>");
        foreach (var featureResult in featureReportData.Results.FeatureResults)
        {
            var symbol = "";
            if (featureResult.Value.Passed())
                symbol += "✅";
            else
                symbol += "❌";

            b.AppendLine($"<tr>");
            b.AppendLine($"<td>{symbol} {featureResult.Value.Name} (Feature)</td>");
            b.AppendLine($"<td>Feature</td>");
            b.AppendLine($"<td></td>");
            b.AppendLine($"<td></td>");
            b.AppendLine($"</tr>");

            WriteScenario(b, featureResult);
        }

        b.Append("</tbody>");
        b.Append("</table>");
    }

    private void WriteScenario(StringBuilder b, KeyValuePair<string, FeatureResult> featureResult)
    {
        foreach (var scenarioResult in featureResult.Value.ScenarioResults)
        {
            var sr = scenarioResult.Value;

            var symbol = "";
            var statusText = "";
            switch (scenarioResult.Value.Status)
            {
                case ScenarioStatus.Passed:
                    symbol += "→ ✅ ";
                    statusText = "Passed";
                    break;
                case ScenarioStatus.Failed:
                    symbol += "→ ❌";
                    statusText = "Failed";
                    break;
                case ScenarioStatus.Skipped:
                    symbol += "→ ⚠️";
                    statusText = "Skipped";
                    break;
            }

            b.AppendLine($"<tr>");
            b.AppendLine($"<td>{symbol} {sr.Name} (Scenario)");
            WriteScenarioDetails(b, sr);
            b.AppendLine("</td>");
            if (sr.Tags != null && sr.Tags.Count > 0)
            {
                b.AppendLine("<td>");
                foreach (var tag in sr.Tags)
                    b.AppendLine($"{tag}<br/>");
                b.AppendLine("</td>");
            }
            else
                b.AppendLine("<td></td>");
            b.AppendLine($"<td>{statusText}</td>");
            b.AppendLine($"<td>{sr.TestRunTotalDuration().ToTestFuznFormattedDuration()}</td>");
            b.AppendLine($"</tr>");
        }
    }

    private void WriteScenarioDetails(StringBuilder b, ScenarioFeatureResult sr)
    {
        // Scenario level metadata & tags block
        //b.AppendLine("<div class='scenario-meta'>");
        //if (!string.IsNullOrEmpty(sr.Id))
        //    b.AppendLine("<div><span class='meta-section-title'>Id:</span> " + sr.Id + "</div>");

        //if (sr.Tags != null && sr.Tags.Count > 0)
        //{
        //    b.AppendLine("<div class='meta-section-title'>Tags:</div>");
        //    b.AppendLine("<div>");
        //    foreach (var tag in sr.Tags)
        //        b.AppendLine("<span class='tag-badge'>" + tag + "</span>");
        //    b.AppendLine("</div>");
        //}

        //if (sr.Metadata != null && sr.Metadata.Count > 0)
        //{
        //    b.AppendLine("<div class='meta-section-title'>Metadata:</div>");
        //    b.AppendLine("<table><thead><tr><th>Key</th><th>Value</th></tr></thead><tbody>");
        //    foreach (var kv in sr.Metadata)
        //    {
        //        b.AppendLine("<tr><td>" + kv.Key + "</td><td>" + kv.Value + "</td></tr>");
        //    }
        //    b.AppendLine("</tbody></table>");
        //}

        //// Scenario timing (optional extra context)
        //if (sr.InitStartTime != default || sr.CleanupEndTime != default)
        //{
        //    b.AppendLine("<div class='meta-section-title'>Timing:</div>");
        //    b.AppendLine("<table><tbody>");
        //    if (sr.InitStartTime != default)
        //        b.AppendLine("<tr><td>Init Start</td><td>" + sr.InitStartTime.ToLocalTime() + "</td></tr>");
        //    if (sr.InitEndTime != default)
        //        b.AppendLine("<tr><td>Init End</td><td>" + sr.InitEndTime.ToLocalTime() + "</td></tr>");
        //    if (sr.ExecuteStartTime != default)
        //        b.AppendLine("<tr><td>Execute Start</td><td>" + sr.ExecuteStartTime.ToLocalTime() + "</td></tr>");
        //    if (sr.ExecuteEndTime != default)
        //        b.AppendLine("<tr><td>Execute End</td><td>" + sr.ExecuteEndTime.ToLocalTime() + "</td></tr>");
        //    if (sr.CleanupStartTime != default)
        //        b.AppendLine("<tr><td>Cleanup Start</td><td>" + sr.CleanupStartTime.ToLocalTime() + "</td></tr>");
        //    if (sr.CleanupEndTime != default)
        //        b.AppendLine("<tr><td>Cleanup End</td><td>" + sr.CleanupEndTime.ToLocalTime() + "</td></tr>");
        //    if (sr.StartTime() != default && sr.EndTime() != default)
        //        b.AppendLine("<tr><td>Total Duration</td><td>" + sr.TestRunTotalDuration().ToTestFuznFormattedDuration() + "</td></tr>");
        //    b.AppendLine("</tbody></table>");
        //}

        //b.AppendLine("</div>"); // scenario-meta

        b.AppendLine("<table>");
        foreach (var iteration in sr.IterationResults)
        {
            if (sr.HasInputData)
            {
                b.AppendLine($"<tr>");
                b.AppendLine($"<td>Input Data: ");
                b.AppendLine($"{(string.IsNullOrEmpty(iteration.InputData) ? " " : iteration.InputData)}");
                //b.AppendLine($"<br/>CorrelationId: {iteration.CorrelationId}");
                b.AppendLine($"</td>");
                b.AppendLine($"<td><span class='{(iteration.Passed ? "passed" : "failed")}'>{(iteration.Passed ? "Passed" : "Failed")}</span></td>");
                b.AppendLine($"<td></td>");
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

    private void WriteStepResult(StringBuilder b, StepFeatureResult stepResult, int level)
    {
        var padding = ((30 * level) - 30);
        var symbol = level == 1 ? "" : " → ";
        //if (stepResult.Status == StepStatus.Passed)
        //    symbol += "✅";
        //else if (stepResult.Status == StepStatus.Failed)
        //    symbol += "❌";
        
        b.AppendLine($"<tr>");
        if (level == 1)
            b.AppendLine($"<td>Step: {stepResult.Name}");
        else
            b.AppendLine($"<td style='padding-left:{padding}px'>{symbol} Step: {stepResult.Name}");

        if (stepResult.Comments != null && stepResult.Comments.Count > 0)
        {
            foreach (var comment in stepResult.Comments)
            {
                b.AppendLine($"<br/>{symbol}// {comment.Text}");
            }
        }

        if (stepResult.Attachments != null && stepResult.Attachments.Count > 0)
        {
            b.AppendLine("<ul>");
            foreach (var attachment in stepResult.Attachments)
            {
                var fileName = Path.GetFileName(attachment.Path);
                b.AppendLine($"<li style='padding-left:{padding + 20}px'>Attachment: <a href=\"Attachments/{fileName}\" target=\"_blank\">{attachment.Name}</a></li>");
            }
            b.AppendLine("</ul>");
        }

        b.AppendLine("</td>");
        b.AppendLine($"<td><span class='{stepResult.Status.ToString().ToLower()}'>{stepResult.Status}</span></td>");
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

    private async Task IncludeEmbeddedResources(FeatureReportData featureReportData)
    {
        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(
            "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Styles.testfuzn.css",
            Path.Combine(featureReportData.TestsOutputDirectory, "assets/styles/testfuzn.css"));

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(
            "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Scripts.chart.js",
            Path.Combine(featureReportData.TestsOutputDirectory, "assets/scripts/chart.js"));
    }
}
