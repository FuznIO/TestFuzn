using System.Text;
using TestFusion.Internals.ConsoleOutput;
using TestFusion.Contracts.Reports;
using TestFusion.Contracts.Results.Feature;
using TestFusion.Internals.Reports.EmbeddedResources;

namespace TestFusion.Internals.Reports.Feature;

internal class FeatureHtmlReportWriter : IFeatureReport
{
    public async Task WriteReport(FeatureReportData featureReportData)
    {
        try
        {
            await IncludeEmbeddedResources(featureReportData);

            var filePath = Path.Combine(featureReportData.TestsOutputDirectory, "TestFusion_Report_Features.html");

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
        b.AppendLine("<title>TestFusion - Feature Test Report</title>");
        b.AppendLine("<link rel='stylesheet' href='assets/styles/testfusion.css'>");
        b.AppendLine("<script>");
        b.AppendLine("document.addEventListener('DOMContentLoaded', function() {");
        b.AppendLine("  document.querySelectorAll('.collapsible').forEach(function(button) {");
        b.AppendLine("    button.addEventListener('click', function() {");
        b.AppendLine("      this.classList.toggle('collapsed');");
        b.AppendLine("      var content = this.nextElementSibling;");
        b.AppendLine("      content.style.display = content.style.display === 'block' ? 'none' : 'block';");
        b.AppendLine("    });");
        b.AppendLine("  });");
        b.AppendLine("});");
        b.AppendLine("</script>");
        b.AppendLine("</head>");
        b.AppendLine("<body>");

        // Header
        b.AppendLine($"<h1>TestFusion - Feature Test Report</h1>");

        b.AppendLine($"<h2>Test Metadata</h2>");
        b.AppendLine("<table>");
        b.AppendLine("<tbody>");
        b.AppendLine("<tr>");
        b.AppendLine("<th>Property</th>");
        b.AppendLine("<th>Value</th>");
        b.AppendLine("</tr");
        b.AppendLine("<tr><td>Test Suite</td><td>" + featureReportData.TestSuiteName + "</td></tr>");
        b.AppendLine("<tr><td>Test Run ID</td><td>" + featureReportData.TestRunId + "</td></tr>");
        b.AppendLine("<tr><td>Test Run - Start Time</td><td>" + featureReportData.TestRunStartTime.ToLocalTime() + "</td></tr>");
        b.AppendLine("<tr><td>Test Run - End Time</td><td>" + featureReportData.TestRunEndTime.ToLocalTime() + "</td></tr>");
        b.AppendLine("<tr><td>Test Run - Duration</td><td>" + featureReportData.TestRunDuration.ToTestFusionFormattedDuration() + "</td></tr>");
        
        // Summary
        var totalFeatures = featureReportData.Results.FeatureResults.Count;
        var totalScenarios = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count);
        var totalPassedScenarios = featureReportData.Results.FeatureResults.Sum(f => f.Value.ScenarioResults.Count(s => s.Value.Status == ScenarioStatus.Passed));
        var totalFailedScenarios = totalScenarios - totalPassedScenarios;

        b.AppendLine("<tr><td>Total Features</td><td>" + totalFeatures + "</td></tr>");
        b.AppendLine("<tr><td>Total Scenarios</td><td>" + totalScenarios + "</td></tr>");
        b.AppendLine("<tr><td>Passed Scenario Tests</td><td><span class='passed'>" + totalPassedScenarios + "</span></td></tr>");
        b.AppendLine("<tr><td>Failed Scenarios Tests</td><td><span class='failed'>" + totalFailedScenarios + "</span></td></tr>");

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");

        b.AppendLine($"<h2>Feature Test Results</h2>");
        // Features
        foreach (var featureResult in featureReportData.Results.FeatureResults)
        {
            b.AppendLine($"<button class='collapsible'>Feature: {featureResult.Key}</button>");
            b.AppendLine("<div class='content'>");

            foreach (var scenarioResult in featureResult.Value.ScenarioResults)
            {
                b.AppendLine($"<button class='collapsible'>Scenario: {scenarioResult.Value.Name} - <span class='{(scenarioResult.Value.Status == ScenarioStatus.Passed ? "passed" : "failed")}'>{(scenarioResult.Value.Status == ScenarioStatus.Passed ? "Passed" : "Failed")}</span></button>");
                b.AppendLine("<div class='content'>");

                if (scenarioResult.Value.HasInputData)
                {
                    foreach (var iteration in scenarioResult.Value.IterationResults)
                    {
                        b.AppendLine($"<button class='collapsible'>Input Data: <span class='{(iteration.Passed ? "passed" : "failed")}'>{(iteration.Passed ? "Passed" : "Failed")}</span></button>");
                        b.AppendLine("<div class='content'>");
                        b.AppendLine("<ul>");
                        b.AppendLine($"<li>{(string.IsNullOrEmpty(iteration.InputData) ? " " : iteration.InputData)}</li>");
                        foreach (var stepResult in iteration.StepResults)
                        {
                            WriteStepResult(b, stepResult.Value);
                        }
                        b.AppendLine("</ul>");
                        b.AppendLine("</div>");
                    }
                }
                else
                {
                    var iteration = scenarioResult.Value.IterationResults.FirstOrDefault();
                    if (iteration != null)
                    {
                        b.AppendLine("<ul>");
                        foreach (var stepResult in iteration.StepResults)
                        {
                            WriteStepResult(b, stepResult.Value);
                        }
                        b.AppendLine("</ul>");
                    }
                }

                b.AppendLine("</div>");
            }

            b.AppendLine("</div>");
        }

        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
    }

    private void WriteStepResult(StringBuilder b, StepFeatureResult stepResult)
    {
        b.AppendLine($"<li><span class='icon'>{SymbolSet.MapStepStatus(stepResult.Status)}</span>Step: {stepResult.Name} - <span class='{stepResult.Status.ToString().ToLower()}'>{stepResult.Status}</span> - Duration: {stepResult.Duration.ToTestFusionResponseTime()}</li>");

        if (stepResult.Attachments != null && stepResult.Attachments.Count > 0)
        {
            b.AppendLine("<ul>");
            foreach (var attachment in stepResult.Attachments)
            {
                var fileName = Path.GetFileName(attachment.Path);
                b.AppendLine($"<li>Attachment: <a href=\"Attachments/{fileName}\" target=\"_blank\">{attachment.Name}</a></li>");
            }
            b.AppendLine("</ul>");
        }
    }

    private async Task IncludeEmbeddedResources(FeatureReportData featureReportData)
    {
        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(
            "TestFusion.Internals.Reports.EmbeddedResources.Styles.testfusion.css",
            Path.Combine(featureReportData.TestsOutputDirectory, "assets/styles/testfusion.css"));

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(
            "TestFusion.Internals.Reports.EmbeddedResources.Scripts.chart.js",
            Path.Combine(featureReportData.TestsOutputDirectory, "assets/scripts/chart.js"));
    }
}
