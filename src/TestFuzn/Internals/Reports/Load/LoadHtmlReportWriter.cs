using System.Text;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Reports.Load;

internal class LoadHtmlReportWriter : ILoadReport
{
    public LoadHtmlReportWriter()
    {
    }

    public async Task WriteReport(LoadReportData loadReportData)
    {
        try
        {
            var reportName = FileNameHelper.MakeFilenameSafe($"{loadReportData.Group.Name}-{loadReportData.Test.Name}");
            var filePath = Path.Combine(GlobalState.TestsOutputDirectory, $"LoadTestReport-{reportName}.html");

            var htmlContent = GenerateHtmlReport(loadReportData);

            await File.WriteAllTextAsync(filePath, htmlContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write HTML load test report.", ex);
        }
    }

    private string GenerateHtmlReport(LoadReportData loadReportData)
    {
        var b = new StringBuilder();

        b.AppendLine("<!DOCTYPE html>");
        b.AppendLine("<html lang='en'>");
        b.AppendLine("<head>");
        b.AppendLine("<meta charset='UTF-8'>");
        b.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        b.AppendLine("<title>TestFuzn - Load Test Report</title>");
        b.AppendLine("<link rel='stylesheet' href='assets/styles/testfuzn.css'>");
        b.AppendLine("</head>");
        b.AppendLine("<body>");
        b.AppendLine(@"<div class=""page-container"">");

        // Header
        b.AppendLine($"<h1>TestFuzn - Load Test Report</h1>");

        WriteTestInfo(loadReportData, b);

        WriteOverallTestStatus(loadReportData, b);

        // Write each scenario
        for (int i = 0; i < loadReportData.ScenarioResults.Count; i++)
        {
            var scenarioResult = loadReportData.ScenarioResults[i];
            WriteScenarioSection(loadReportData, scenarioResult, b, i + 1);
        }

        b.AppendLine(@"</div>");
        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
    }

    private static void WriteTestInfo(LoadReportData loadReportData, StringBuilder b)
    {
        b.AppendLine($"<h2>Test Info</h2>");
        b.AppendLine("<table>");
        b.AppendLine(@$"<tr><th class=""vertical"">Test Run ID</th><td>{loadReportData.TestRunId}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Execution Environment</th><td>{GlobalState.ExecutionEnvironment}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Target Environment</th><td>{GlobalState.TargetEnvironment}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Suite - Name</th><td>{loadReportData.TestSuite.Name}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Suite - ID</th><td>{loadReportData.TestSuite.Id}</td></tr>");

        if (loadReportData.TestSuite.Metadata != null && loadReportData.TestSuite.Metadata.Count > 0)
        {
            b.AppendLine(@"<tr><th class=""vertical"">Suite - Metadata</th><td><ul>");
            foreach (var metadata in loadReportData.TestSuite.Metadata)
                b.AppendLine(@$"<li>{metadata.Key}: {metadata.Value}</li>");
            b.AppendLine("</ul></td></tr>");
        }

        b.AppendLine(@$"<tr><th class=""vertical"">Group - Name</th><td>{loadReportData.Group.Name}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Test - Name</th><td>{loadReportData.Test.Name}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Test - Full Name</th><td>{loadReportData.Test.FullName}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Test - ID</th><td>{loadReportData.Test.Id}</td></tr>");

        if (loadReportData.Test.Tags != null && loadReportData.Test.Tags.Count > 0)
        {
            b.AppendLine(@"<tr><th class=""vertical"">Test - Tags</th><td><ul>");
            foreach (var tag in loadReportData.Test.Tags)
                b.AppendLine($"<li>{tag}</li>");
            b.AppendLine("</ul></td></tr>");
        }

        if (loadReportData.Test.Metadata != null && loadReportData.Test.Metadata.Count > 0)
        {
            b.AppendLine(@"<tr><th class=""vertical"">Test - Metadata</th><td><ul>");
            foreach (var metadata in loadReportData.Test.Metadata)
                b.AppendLine($"<li>{metadata.Key}: {metadata.Value}</li>");
            b.AppendLine("</ul></td></tr>");
        }

        b.AppendLine(@$"<tr><th class=""vertical"">Scenarios Count</th><td>{loadReportData.ScenarioResults.Count}</td></tr>");
        b.AppendLine("</table>");
    }

    private static void WriteOverallTestStatus(LoadReportData loadReportData, StringBuilder b)
    {
        b.AppendLine($"<h2>Overall Test Status</h2>");

        var allPassed = loadReportData.ScenarioResults.All(s => s.Status == TestStatus.Passed);
        var failedCount = loadReportData.ScenarioResults.Count(s => s.Status == TestStatus.Failed);
        var passedCount = loadReportData.ScenarioResults.Count(s => s.Status == TestStatus.Passed);

        if (allPassed)
        {
            b.AppendLine(@$"<div class=""status-panel passed"">");
            b.AppendLine(@$"<div class=""title"">✅ All scenarios passed ({passedCount}/{loadReportData.ScenarioResults.Count})</div>");
            b.AppendLine(@$"</div>");
        }
        else
        {
            b.AppendLine(@$"<div class=""status-panel failed"">");
            b.AppendLine(@$"<div class=""title"">❌ Some scenarios failed ({failedCount} failed, {passedCount} passed)</div>");
            b.AppendLine(@$"</div>");
        }
    }

    private void WriteScenarioSection(LoadReportData loadReportData, ScenarioLoadResult scenarioResult, StringBuilder b, int scenarioIndex)
    {
        b.AppendLine($"<h2>Scenario {scenarioIndex} - {scenarioResult.ScenarioName}</h2>");

        WriteScenarioInfo(scenarioResult, b);
        WriteTestStatus(scenarioResult, b);
        WritePhaseTimings(scenarioResult, b);
        WriteSteps(b, scenarioResult);
        WriteSnapshots(loadReportData, b, scenarioResult);
    }

    private static void WriteScenarioInfo(ScenarioLoadResult scenarioResult, StringBuilder b)
    {
        b.AppendLine("<table>");
        b.AppendLine(@$"<tr><th class=""vertical"">Scenario - Name</th><td>{scenarioResult.ScenarioName}</td></tr>");
        b.AppendLine(@$"<tr><th class=""vertical"">Scenario - ID</th><td>{scenarioResult.Id}</td></tr>");

        if (scenarioResult.Tags != null && scenarioResult.Tags.Count > 0)
        {
            b.AppendLine(@"<tr><th class=""vertical"">Scenario - Tags</th><td><ul>");
            foreach (var tag in scenarioResult.Tags)
                b.AppendLine($"<li>{tag}</li>");
            b.AppendLine("</ul></td></tr>");
        }

        if (scenarioResult.Metadata != null && scenarioResult.Metadata.Count > 0)
        {
            b.AppendLine(@"<tr><th class=""vertical"">Scenario - Metadata</th><td><ul>");
            foreach (var metadata in scenarioResult.Metadata)
                b.AppendLine($"<li>{metadata.Key}: {metadata.Value}</li>");
            b.AppendLine("</ul></td></tr>");
        }

        b.AppendLine("<tr>");
        b.AppendLine(@"<th class=""vertical"">Simulations</th><td><ul>");
        foreach (var simulation in scenarioResult.Simulations)
            b.AppendLine($"<li>{simulation}</li>");
        b.AppendLine("</ul></td></tr>");
        b.AppendLine("</table>");
    }

    private static void WriteTestStatus(ScenarioLoadResult scenarioResult, StringBuilder b)
    {
        b.AppendLine($"<h3>Scenario Status</h3>");
        if (scenarioResult.Status == TestStatus.Passed)
        {
            b.AppendLine(@$"<div class=""status-panel passed"">");
            b.AppendLine(@$"<div class=""title"">✅ Scenario passed</div>");
            b.AppendLine(@$"</div>");
        }
        else if (scenarioResult.Status == TestStatus.Failed)
        {
            var exception = "";
            if (scenarioResult.AssertWhileRunningException != null)
                exception = $@"<span style=""font-weight:bold"">AssertWhileRunning failed:</span> {scenarioResult.AssertWhileRunningException.Message.ToString()}";
            else if (scenarioResult.AssertWhenDoneException != null)
                exception = $@"<span style=""font-weight:bold"">AssertWhenDone failed:</span> {scenarioResult.AssertWhenDoneException.Message.ToString()}";

            b.AppendLine(@$"<div class=""status-panel failed"">");
            b.AppendLine(@$"<div class=""title"">❌ Scenario failed</div>");
            if (exception != "")
            {
                b.AppendLine($@"<div class=""details"">{exception}</div>");
            }
            b.AppendLine("</div>");
        }
        else
            throw new InvalidOperationException($"Scenario status not supported: {scenarioResult.Status}");
    }

    private static void WritePhaseTimings(ScenarioLoadResult scenarioResult, StringBuilder b)
    {
        b.AppendLine($"<h3>Phase Timings</h3>");
        b.AppendLine("<table>");
        b.AppendLine("<tr>");
        b.AppendLine("<th>Phase</th>");
        b.AppendLine("<th>Duration</th>");
        b.AppendLine("<th>Started</th>");
        b.AppendLine("<th>Ended</th>");
        b.AppendLine("</tr>");
        // Init
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Init</td>");
        b.AppendLine($"<td>{scenarioResult.InitTotalDuration().ToTestFuznReadableString()}</td>");
        b.AppendLine($"<td>{scenarioResult.InitStartTime.ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.InitEndTime.ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        // Warmup
        if (scenarioResult.HasWarmupStep())
        {
            b.AppendLine("<tr>");
            b.AppendLine($"<td>Warmup</td>");
            b.AppendLine($"<td>{scenarioResult.WarmupTotalDuration().ToTestFuznReadableString()}</td>");
            b.AppendLine($"<td>{scenarioResult.WarmupStartTime.ToLocalTime()}</td>");
            b.AppendLine($"<td>{scenarioResult.WarmupEndTime.ToLocalTime()}</td>");
            b.AppendLine("</tr>");
        }
        // Execution
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Execution</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementTotalDuration().ToTestFuznReadableString()}</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementStartTime.ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementEndTime.ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        // Cleanup
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Cleanup</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupTotalDuration().ToTestFuznReadableString()}</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupStartTime.ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupEndTime.ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        // Test Run
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Total Test Run</td>");
        b.AppendLine($"<td>{scenarioResult.TestRunTotalDuration().ToTestFuznReadableString()}</td>");
        b.AppendLine($"<td>{scenarioResult.StartTime().ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.EndTime().ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        b.AppendLine("</table>");
    }

    private void WriteSteps(StringBuilder b, ScenarioLoadResult scenario)
    {
        b.AppendLine($"<h3>Test Results</h3>");

        // Table for the scenario
        b.AppendLine("<table>");
        b.AppendLine("<thead>");
        b.AppendLine("<tr>");
        b.AppendLine("<th>Step</th>");
        b.AppendLine("<th>Type</th>");
        b.AppendLine("<th>Requests</th>");
        b.AppendLine("<th>RPS</th>");
        b.AppendLine("<th>Mean (ms)</th>");
        b.AppendLine("<th>Median (ms)</th>");
        b.AppendLine("<th>P75 (ms)</th>");
        b.AppendLine("<th>P99 (ms)</th>");
        b.AppendLine("<th>Min (ms)</th>");
        b.AppendLine("<th>Max (ms)</th>");
        b.AppendLine("<th>StdDev (ms)</th>");
        b.AppendLine("</tr>");
        b.AppendLine("</thead>");
        b.AppendLine("<tbody>");

        // All rows.

        b.AppendLine(@$"<tr class=""summary"">");
        Cols(b, "All Steps (Summary)", true, scenario.Ok, scenario.Failed, 1);

        b.AppendLine("</tr>");
        b.AppendLine(@"<tr class=""summary"">");
        Cols(b, "", false, scenario.Ok, scenario.Failed, 1);
        b.AppendLine("</tr>");

        foreach (var step in scenario.Steps)
        {
            WriteStepRow(step.Value, 1);
        }

        void WriteStepRow(StepLoadResult step, int level)
        {
            // Step rows.
            b.AppendLine(@$"<tr>");
            Cols(b, step.Name, true, step.Ok, step.Failed, level);
            b.AppendLine("</tr>");
            b.AppendLine(@"<tr>");
            Cols(b, "", false, step.Ok, step.Failed, level);
            b.AppendLine("</tr>");

            // Show errors if present
            if (step.Errors != null && step.Errors.Count > 0)
            {
                b.AppendLine($@"<tr><td colspan=""12"" style=""padding-left:{level * 20}px;"" class=""errors"">
                <span style=""font-weight:bold"">Step Errors:</span><ul>");
                foreach (var error in step.Errors.Values)
                {
                    b.AppendLine("<li>");
                    b.AppendLine(@$"<span style=""font-weight:bold"">Message:</span> {error.Message}<br/>");
                    b.AppendLine(@$"<span style=""font-weight:bold"">Details:</span> {error.Details}<br/>");
                    b.AppendLine(@$"<span style=""font-weight:bold"">Count:</span> {error.Count}");
                    b.AppendLine("</li>");
                }
                b.AppendLine("</ul></td></tr>");
            }

            if (step.Steps == null || step.Steps.Count == 0)
                return;

            foreach (var innerStep in step.Steps)
                WriteStepRow(innerStep, level + 1);
        }

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");
    }

    private void WriteSnapshots(LoadReportData loadReportData, StringBuilder b, ScenarioLoadResult scenario)
    {

        // Table for snapshots
        b.AppendLine("<h3>Snapshot Timeline</h3>");

        b.AppendLine("<table>");
        b.AppendLine("<thead>");
        b.AppendLine("<tr>");
        b.AppendLine(@"<th style=""width:1%;white-space:nowrap;"">Created</th>");
        b.AppendLine("<th>Step</th>");
        b.AppendLine("<th>Type</th>");
        b.AppendLine("<th>Requests</th>");
        b.AppendLine("<th>RPS</th>");
        b.AppendLine("<th>Mean (ms)</th>");
        b.AppendLine("<th>Median (ms)</th>");
        b.AppendLine("<th>P75 (ms)</th>");
        b.AppendLine("<th>P99 (ms)</th>");
        b.AppendLine("<th>Min (ms)</th>");
        b.AppendLine("<th>Max (ms)</th>");
        b.AppendLine("<th>StdDev (ms)</th>");
        b.AppendLine("</tr>");
        b.AppendLine("</thead>");
        b.AppendLine("<tbody>");

        var snapshots = InMemorySnapshotCollectorSinkPlugin.GetSnapshots(loadReportData.Group.Name, scenario.ScenarioName);

        foreach (var snapshot in snapshots)
        {
            b.AppendLine(@$"<tr class=""summary"">");
            b.AppendLine(@$"<td style=""width:1%;white-space:nowrap;"">{snapshot.Created:yyyy-MM-dd HH:mm:ss.fff}</td>");
            Cols(b, "All Steps (Summary)", true, snapshot.Ok, snapshot.Failed, 1);
            b.AppendLine("</tr>");
            b.AppendLine(@"<tr class=""summary"">");
            b.AppendLine($"<td></td>");
            Cols(b, "", false, snapshot.Ok, snapshot.Failed, 1);
            b.AppendLine("</tr>");

            foreach (var step in snapshot.Steps)
            {
                RenderSnapshotStep(step.Value);
            }
        }

        void RenderSnapshotStep(StepLoadResult stepResult)
        {
            b.AppendLine(@"<tr>");
            b.AppendLine($"<td></td>");
            Cols(b, stepResult.Name, true, stepResult.Ok, stepResult.Failed, 1);
            b.AppendLine("</tr>");
            b.AppendLine(@"<tr>");
            b.AppendLine($"<td></td>");
            Cols(b, "", false, stepResult.Ok, stepResult.Failed, 1);
            b.AppendLine("</tr>");

            if (stepResult.Steps == null || stepResult.Steps.Count == 0)
                return;

            foreach (var innerStep in stepResult.Steps)
            {
                RenderSnapshotStep(innerStep);
            }
        }

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");
    }

    private void Cols(StringBuilder b, string stepName, bool isOkRow, Stats okStats, Stats failedStats, int level)
    {
        Stats stats = isOkRow ? okStats : failedStats;
        
        var padding = level > 1 ? $" style='padding-left:{(level - 1) * 20}px;'" : "";
        var prefix = "";
        if (level > 1 && !string.IsNullOrEmpty(stepName))
            prefix = "→ ";
        if (isOkRow)
        {
            if (failedStats.RequestCount == 0)
                prefix += "✅";
            else
                prefix += "❌";
        }

        var cssClass = "";
        if (isOkRow)
            cssClass = "ok";
        else
            cssClass = "failed";

        b.AppendLine($"<td{padding}>{prefix} {stepName}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{(isOkRow ? "Ok" : "Failed")}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.RequestCount}</td>"); 
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.RequestsPerSecond}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.ResponseTimeMean.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.ResponseTimeMedian.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class="" {cssClass} "">{stats.ResponseTimePercentile75.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class="" {cssClass} "">{stats.ResponseTimePercentile99.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class="" {cssClass} "">{stats.ResponseTimeMin.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class="" {cssClass} "">{stats.ResponseTimeMax.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class="" {cssClass} "">{stats.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}</td>");
    }
}
