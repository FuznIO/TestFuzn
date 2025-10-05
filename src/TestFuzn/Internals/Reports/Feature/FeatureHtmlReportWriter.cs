using Fuzn.TestFuzn.Contracts;
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

        b.AppendLine($"<h2>Test Execution Info</h2>");
        b.AppendLine("<table>");
        
        b.AppendLine(@"<tr><th class=""vertical"">Test Suite Name</th><td>" + featureReportData.TestSuite.Name + "</td></tr>");
        b.AppendLine(@"<tr><th class=""vertical"">Test Suite ID</th><td>" + featureReportData.TestSuite.Id + "</td></tr>");

        if (featureReportData.TestSuite.Metadata != null)
        {
            foreach (var metadata in featureReportData.TestSuite.Metadata)
            {
                b.AppendLine(@$"<tr><th class=""vertical"">Test Suite Metadata - {metadata.Key}</th><td>{metadata.Value}</td></tr>");
            }
        }
        
        b.AppendLine(@$"<tr><th class=""vertical"">Run ID</th><td>{featureReportData.TestRunId}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"" class=""vertical"">Start Time</th><td>{featureReportData.TestRunStartTime.ToLocalTime()}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">End Time</th><td>{featureReportData.TestRunEndTime.ToLocalTime()}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Duration</th><td>{featureReportData.TestRunDuration.ToTestFuznFormattedDuration()}</td></tr>");

        // Summary
        var featuresTotal = featureReportData.Results.FeatureResults.Count;
        var featuresPassed = featureReportData.Results.FeatureResults.Count(x => x.Value.Passed());
        var featuresFailed = featuresTotal - featuresPassed;
        var scenariosTotal = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count);
        var scenariosPassed = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count(s => s.Value.Status == ScenarioStatus.Passed));
        var scenariosSkipped = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count(s => s.Value.Status == ScenarioStatus.Skipped));
        var scenariosFailed = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count(s => s.Value.Status == ScenarioStatus.Failed));

        b.AppendLine(@$"<tr><th class=""vertical"">Summary - Features</th><td>Total: 🔢 {featuresTotal} |  Passed: ✅ {featuresPassed} | Failed: ❌ {featuresFailed}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Summary - Scenarios</th><td>Total: 🔢 {scenariosTotal} | Passed: ✅ {scenariosPassed} | Failed: ❌ {scenariosFailed} | Skipped: ⚠️ {scenariosSkipped}</td></tr>");

        b.AppendLine("</table>");

        WriteFeatureResults(featureReportData, b);

        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
    }

    private void WriteFeatureResults(FeatureReportData featureReportData, StringBuilder b)
    {
        b.AppendLine($"<h2>Test Execution Results</h2>");
        // Features
        b.Append(@"<table class=""feature-results"">");
        b.AppendLine($"<tr>");
        b.AppendLine($"<th>Details</th>");
        b.AppendLine(@$"<th>Type</th>");
        b.AppendLine(@$"<th>Status</th>");
        b.AppendLine(@$"<th>Tags</th>");
        b.AppendLine(@$"<th>Duration</th>");
        b.AppendLine($"</tr>");
        foreach (var featureResult in featureReportData.Results.FeatureResults)
        {
            var symbol = "";
            var statusText = "";
            if (featureResult.Value.Passed())
            {
                symbol = "✅";
                statusText = "✅ Passed";
            }
            else
            {
                symbol = "❌";
                statusText = "❌ Failed";
            }

            b.AppendLine($"<tr>");
            b.AppendLine($"<td>{symbol} {featureResult.Value.Name}</td>");
            b.AppendLine($"<td>Feature</td>");
            b.AppendLine($"<td>{statusText}</td>");
            b.AppendLine($"<td></td>");
            b.AppendLine($"<td></td>");
            b.AppendLine($"</tr>");

            WriteScenario(b, featureResult);
        }

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
                    symbol = "→ ✅ ";
                    statusText = "✅ Passed";
                    break;
                case ScenarioStatus.Failed:
                    symbol = "→ ❌";
                    statusText = "❌ Failed";
                    break;
                case ScenarioStatus.Skipped:
                    symbol = "→ ⚠️";
                    statusText = "⚠️ Skipped";
                    break;
            }

            b.AppendLine($"<tr>");
            b.AppendLine($"<td>{symbol} {sr.Name}");
            WriteScenarioDetails(b, sr);
            b.AppendLine("</td>");
            
            var typeText = sr.TestType == TestType.Feature ? "Feature" : "Load";

            b.AppendLine($"<td>Scenario - {typeText}</td>");
            b.AppendLine($"<td>{statusText}</td>");
            b.AppendLine("<td>");
            if (sr.Tags != null && sr.Tags.Count > 0)
            {
                foreach (var tag in sr.Tags)
                    b.AppendLine($"{tag}<br/>");
                
            }
            b.AppendLine("</td>");
            b.AppendLine($"<td>{sr.TestRunTotalDuration().ToTestFuznResponseTime()}</td>");
            b.AppendLine($"</tr>");
        }
    }

    private void WriteScenarioDetails(StringBuilder b, ScenarioFeatureResult sr)
    {
        if (sr.Status != ScenarioStatus.Skipped
            && sr.IterationResults.Count > 0)
        {
            b.AppendLine(@"<details class=""results"">");
            b.AppendLine("<summary></summary>");
        }

        //if (sr.Status != ScenarioStatus.Skipped)
        //    WriteScenarioInfo(b, sr);

        if (sr.IterationResults.Count > 0)
            WriteStepDetails(b, sr);

        if (sr.Status != ScenarioStatus.Skipped
            && sr.IterationResults.Count > 0)
            b.AppendLine("</details>");
    }

    private static void WriteScenarioInfo(StringBuilder b, ScenarioFeatureResult sr)
    {
        b.AppendLine("<table>");
        b.AppendLine($"<tr><th>ID</th><td>{sr.Id}</td></tr>");

        if (sr.Metadata != null)
        {
            foreach (var metadata in sr.Metadata)
                b.AppendLine(@$"<tr><th class=""vertical"">Metadata - {metadata.Key}</th><td>{metadata.Value}</td></tr>");
        }

        if (sr.InitStartTime != default)
            b.AppendLine($@"<tr><th class=""vertical"">Init Start Time</th><td>{sr.InitStartTime.ToLocalTime()}</td></tr>");
        if (sr.InitEndTime != default)
            b.AppendLine($@"<tr><th class=""vertical"">Init End Time</td><td>{sr.InitEndTime.ToLocalTime()}</td></tr>");
        if (sr.ExecuteStartTime != default)
            b.AppendLine($@"<tr><th class=""vertical"">Execute Start Time</th><td>{sr.ExecuteStartTime.ToLocalTime()}</td></tr>");
        if (sr.ExecuteEndTime != default)
            b.AppendLine($@"<tr><th class=""vertical"">Execute End Time</th><td>{sr.ExecuteEndTime.ToLocalTime()}</td></tr>");
        if (sr.CleanupStartTime != default)
            b.AppendLine($@"<tr><th class=""vertical"">Cleanup Start Time</th><td>{sr.CleanupStartTime.ToLocalTime()}</td></tr>");
        if (sr.CleanupEndTime != default)
            b.AppendLine($@"<tr><th class=""vertical"">Cleanup End Time</th><td>{sr.CleanupEndTime.ToLocalTime()}</td></tr>");
        if (sr.StartTime() != default && sr.EndTime() != default)
            b.AppendLine($@"<tr><th class=""vertical"">Duration</th><td>{sr.TestRunTotalDuration().ToTestFuznFormattedDuration()}</td></tr>");

        b.AppendLine("</table>");
    }

    private void WriteStepDetails(StringBuilder b, ScenarioFeatureResult sr)
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

    private void WriteStepResult(StringBuilder b, StepFeatureResult stepResult, int level)
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
