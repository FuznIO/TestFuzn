using System.Text;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Reports.EmbeddedResources;

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
            await IncludeEmbeddedResources(loadReportData);

            var reportName = FileNameHelper.MakeFilenameSafe($"{loadReportData.Group.Name}-{loadReportData.Test.Name}");
            var filePath = Path.Combine(loadReportData.TestsOutputDirectory, $"LoadTestReport-{reportName}.html");

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
        b.AppendLine("<script src='assets/scripts/chart.js'></script>");
        b.AppendLine("</head>");
        b.AppendLine("<body>");
        b.AppendLine(@"<div class=""page-container"">");

        b.AppendLine($"<h1>{loadReportData.Test.Name} - Load Test Report</h1>");
        b.AppendLine($@"<div class=""test-name"">{loadReportData.Group.Name}</div>");

        WriteRunInfo(loadReportData, b);

        WriteOverallTestStatus(loadReportData, b);

        // Write each scenario
        for (int i = 0; i < loadReportData.ScenarioResults.Count; i++)
        {
            var scenarioResult = loadReportData.ScenarioResults[i];
            WriteScenarioSection(loadReportData, scenarioResult, b, i + 1);
        }

        WriteChartScripts(loadReportData, b);

        b.AppendLine(@"</div>");
        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
    }

    private static void WriteRunInfo(LoadReportData loadReportData, StringBuilder b)
    {
        b.AppendLine(@"<div class=""run-info"">");

        b.AppendLine(@"<div class=""run-info-row"">");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Run ID</span><span class=""info-value"">" + loadReportData.TestRunId + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Duration</span><span class=""info-value"">" + loadReportData.TestRunDuration.ToTestFuznReadableString() + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Started</span><span class=""info-value"">" + loadReportData.TestRunStartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Completed</span><span class=""info-value"">" + loadReportData.TestRunEndTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "</span></div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""run-info-row"">");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Execution Environment</span><span class=""info-value"">" + (string.IsNullOrEmpty(GlobalState.ExecutionEnvironment) ? "-" : GlobalState.ExecutionEnvironment) + "</span></div>");
        b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Target Environment</span><span class=""info-value"">" + (string.IsNullOrEmpty(GlobalState.TargetEnvironment) ? "-" : GlobalState.TargetEnvironment) + "</span></div>");
        b.AppendLine("</div>");

        if (loadReportData.Test.Tags != null && loadReportData.Test.Tags.Count > 0)
        {
            b.AppendLine(@"<div class=""run-info-row"">");
            b.AppendLine(@"<div class=""info-item""><span class=""info-label"">Tags</span><span class=""info-value"">" + string.Join(", ", loadReportData.Test.Tags) + "</span></div>");
            b.AppendLine("</div>");
        }
        if (loadReportData.TestSuite.Metadata != null && loadReportData.TestSuite.Metadata.Count > 0)
        {
            b.AppendLine(@"<div class=""run-info-row"">");
            foreach (var metadata in loadReportData.TestSuite.Metadata)
            {
                b.AppendLine($@"<div class=""info-item""><span class=""info-label"">{metadata.Key}</span><span class=""info-value"">{metadata.Value}</span></div>");
            }
            b.AppendLine("</div>");
        }
        if (loadReportData.Test.Metadata != null && loadReportData.Test.Metadata.Count > 0)
        {
            b.AppendLine(@"<div class=""run-info-row"">");
            foreach (var metadata in loadReportData.Test.Metadata)
            {
                b.AppendLine($@"<div class=""info-item""><span class=""info-label"">{metadata.Key}</span><span class=""info-value"">{metadata.Value}</span></div>");
            }
            b.AppendLine("</div>");
        }

        b.AppendLine("</div>");
    }

    private static void WriteOverallTestStatus(LoadReportData loadReportData, StringBuilder b)
    {
        var scenariosTotal = loadReportData.ScenarioResults.Count;
        
        if (scenariosTotal == 0)
        {
            b.AppendLine(@"<div class=""overall-status no-tests"">");
            b.AppendLine(@"<div class=""icon"">ℹ️</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">No Scenarios Executed</div>");
            b.AppendLine("</div></div>");
            return;
        }

        if (scenariosTotal == 1)
            return;

        var allPassed = loadReportData.ScenarioResults.All(s => s.Status == TestStatus.Passed);
        var failedCount = loadReportData.ScenarioResults.Count(s => s.Status == TestStatus.Failed);
        var passedCount = loadReportData.ScenarioResults.Count(s => s.Status == TestStatus.Passed);

        if (allPassed)
        {
            b.AppendLine(@"<div class=""overall-status passed"">");
            b.AppendLine(@"<div class=""icon"">✅</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine($@"<div class=""title"">Test Passed</div>");
            b.AppendLine("</div></div>");
        }
        else
        {
            b.AppendLine(@"<div class=""overall-status failed"">");
            b.AppendLine(@"<div class=""icon"">❌</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine($@"<div class=""title"">{failedCount} Scenario{(failedCount > 1 ? "s" : "")} Failed</div>");
            b.AppendLine("</div></div>");
        }
    }

    private void WriteScenarioSection(LoadReportData loadReportData, ScenarioLoadResult scenarioResult, StringBuilder b, int scenarioIndex)
    {
        if (loadReportData.ScenarioResults.Count == 1)
            b.AppendLine($"<h2>Scenario -  {scenarioResult.ScenarioName}</h2>");
        else
            b.AppendLine($"<h2>Scenario {scenarioIndex} -  {scenarioResult.ScenarioName}</h2>");

        WriteScenarioInfo(scenarioResult, b);                                    // 1
        b.AppendLine(@"<div style=""margin-top: 32px;""></div>");
        WriteScenarioStatus(loadReportData, scenarioResult, b);                  // 2
        
        b.AppendLine(@"<div style=""margin-top: 32px;""></div>");
        
        WriteDashboard(scenarioResult, b, scenarioIndex);                        // 3
        
        WriteSnapshotTimelineChart(loadReportData, b, scenarioResult, scenarioIndex); // 3.5 - Timeline chart
        
        b.AppendLine(@"<div style=""margin-top: 32px;""></div>");
        
        WriteResponseTimeChart(scenarioResult, b, scenarioIndex);                // 4
        
        b.AppendLine("<h3>Test Phases</h3>");
        WritePhaseTimings(scenarioResult, b);                                    // 5
        
        b.AppendLine("<h3>Step Performance</h3>");
        WriteSteps(b, scenarioResult);                                           // 6
        
        WriteSnapshotTable(loadReportData, b, scenarioResult);                   // 7 - Snapshot table (has its own h3)
        WriteFailureDetails(scenarioResult, b);                                  // 8 (has its own h3)
    }

    private static void WriteScenarioInfo(ScenarioLoadResult scenarioResult, StringBuilder b)
    {
        b.AppendLine("<table>");
        if (!string.IsNullOrEmpty(scenarioResult.Description))
            b.AppendLine(@$"<tr><th class=""vertical"">Description</th><td>{scenarioResult.Description}</td></tr>");

        if (scenarioResult.Simulations != null && scenarioResult.Simulations.Count > 0)
        {
            b.AppendLine("<tr>");
            b.AppendLine(@"<th class=""vertical"">Simulations</th><td><ul>");
            foreach (var simulation in scenarioResult.Simulations)
                b.AppendLine($"<li>{simulation}</li>");
            b.AppendLine("</ul></td></tr>");
        }
        b.AppendLine("</table>");
    }

    private static void WriteScenarioStatus(LoadReportData loadReportData, ScenarioLoadResult scenarioResult, StringBuilder b)
    {
        if (scenarioResult.Status == TestStatus.Passed)
        {
            b.AppendLine(@"<div class=""overall-status passed"">");
            b.AppendLine(@"<div class=""icon"">✅</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">Scenario Passed</div>");
            b.AppendLine("</div></div>");
        }
        else if (scenarioResult.Status == TestStatus.Failed)
        {
            var exception = "";
            if (scenarioResult.AssertWhileRunningException != null)
                exception = $"AssertWhileRunning failed: {scenarioResult.AssertWhileRunningException.Message}";
            else if (scenarioResult.AssertWhenDoneException != null)
                exception = $"AssertWhenDone failed: {scenarioResult.AssertWhenDoneException.Message}";

            b.AppendLine(@"<div class=""overall-status failed"">");
            b.AppendLine(@"<div class=""icon"">❌</div>");
            b.AppendLine(@"<div class=""message"">");
            b.AppendLine(@"<div class=""title"">Scenario Failed</div>");
            if (!string.IsNullOrEmpty(exception))
            {
                b.AppendLine($@"<div class=""subtitle"">{exception}</div>");
            }
            b.AppendLine("</div></div>");
        }
    }

    private static void WriteDashboard(ScenarioLoadResult scenarioResult, StringBuilder b, int scenarioIndex)
    {
        var totalRequests = scenarioResult.Ok.RequestCount + scenarioResult.Failed.RequestCount;
        var successRate = totalRequests > 0 ? Math.Round(100.0 * scenarioResult.Ok.RequestCount / totalRequests, 1) : 0;

        b.AppendLine(@"<div class=""dashboard"">");

        // Donut Chart
        b.AppendLine(@"<div class=""chart-container"">");
        b.AppendLine(@"<div class=""chart-title"">Request Distribution</div>");
        b.AppendLine(@"<div class=""chart-wrapper"">");
        b.AppendLine($@"<canvas id=""successChart{scenarioIndex}""></canvas>");
        b.AppendLine($@"<div class=""chart-center-text"">");
        b.AppendLine($@"<div class=""rate"">{successRate}%</div>");
        b.AppendLine($@"<div class=""label"">Success</div>");
        b.AppendLine("</div>");
        b.AppendLine("</div>");
        b.AppendLine(@"<div class=""legend-container"">");
        b.AppendLine(@"<div class=""legend-item""><div class=""legend-color passed""></div>OK</div>");
        b.AppendLine(@"<div class=""legend-item""><div class=""legend-color failed""></div>Failed</div>");
        b.AppendLine("</div>");
        b.AppendLine("</div>");

        // Stats Cards
        b.AppendLine(@"<div class=""stats-grid"">");
        
        b.AppendLine(@"<div class=""stat-card total"">");
        b.AppendLine($@"<div class=""value"">{totalRequests:N0}</div>");
        b.AppendLine(@"<div class=""label"">Total Requests</div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""stat-card passed"">");
        b.AppendLine($@"<div class=""value"">{scenarioResult.Ok.RequestCount:N0}</div>");
        b.AppendLine(@"<div class=""label"">Successful</div>");
        b.AppendLine($@"<div class=""percentage"">{successRate}%</div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""stat-card failed"">");
        b.AppendLine($@"<div class=""value"">{scenarioResult.Failed.RequestCount:N0}</div>");
        b.AppendLine(@"<div class=""label"">Failed</div>");
        var failedPct = totalRequests > 0 ? Math.Round(100.0 * scenarioResult.Failed.RequestCount / totalRequests, 1) : 0;
        b.AppendLine($@"<div class=""percentage"">{failedPct}%</div>");
        b.AppendLine("</div>");

        b.AppendLine(@"<div class=""stat-card total"">");
        b.AppendLine($@"<div class=""value"">{scenarioResult.Ok.RequestsPerSecond}</div>");
        b.AppendLine(@"<div class=""label"">Requests/sec</div>");
        b.AppendLine("</div>");

        b.AppendLine("</div>"); // stats-grid
        b.AppendLine("</div>"); // dashboard
    }

    private void WriteSnapshotTimelineChart(LoadReportData loadReportData, StringBuilder b, ScenarioLoadResult scenario, int scenarioIndex)
    {
        var snapshots = InMemorySnapshotCollectorSinkPlugin.GetSnapshots(loadReportData.Group.Name, loadReportData.Test.Name, scenario.ScenarioName);

        if (snapshots == null || snapshots.Count == 0)
            return;

        b.AppendLine(@"<div style=""display: grid; gap: 1.5rem;"">");

        // RPS Chart
        b.AppendLine(@"<div class=""chart-card"">");
        b.AppendLine(@"<div class=""chart-title"">Requests Per Second</div>");
        b.AppendLine($@"<canvas id=""rpsChart{scenarioIndex}""></canvas>");
        b.AppendLine("</div>");

        // Response Time Timeline Chart
        b.AppendLine(@"<div class=""chart-card"">");
        b.AppendLine(@"<div class=""chart-title"">Response Time Over Time</div>");
        b.AppendLine($@"<canvas id=""timelineChart{scenarioIndex}""></canvas>");
        b.AppendLine("</div>");

        b.AppendLine("</div>");
    }

    private static void WriteResponseTimeChart(ScenarioLoadResult scenarioResult, StringBuilder b, int scenarioIndex)
    {
        b.AppendLine(@"<div style=""display: grid; gap: 1.5rem;"">");
        b.AppendLine(@"<div class=""chart-card"">");
        b.AppendLine(@"<div class=""chart-title"">Response Time Percentiles</div>");
        b.AppendLine($@"<canvas id=""responseTimeChart{scenarioIndex}""></canvas>");
        b.AppendLine("</div>");
        b.AppendLine("</div>");
    }

    private static void WritePhaseTimings(ScenarioLoadResult scenarioResult, StringBuilder b)
    {
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
        b.AppendLine($"<td>{scenarioResult.InitStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
        b.AppendLine($"<td>{scenarioResult.InitEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
        b.AppendLine("</tr>");
        // Warmup
        if (scenarioResult.HasWarmupStep())
        {
            b.AppendLine("<tr>");
            b.AppendLine($"<td>Warmup</td>");
            b.AppendLine($"<td>{scenarioResult.WarmupTotalDuration().ToTestFuznReadableString()}</td>");
            b.AppendLine($"<td>{scenarioResult.WarmupStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
            b.AppendLine($"<td>{scenarioResult.WarmupEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
            b.AppendLine("</tr>");
        }
        // Execution
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Execution</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementTotalDuration().ToTestFuznReadableString()}</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
        b.AppendLine("</tr>");
        // Cleanup
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Cleanup</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupTotalDuration().ToTestFuznReadableString()}</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupStartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupEndTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
        b.AppendLine("</tr>");
        // Test Run
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Total Test Run</td>");
        b.AppendLine($"<td>{scenarioResult.TestRunTotalDuration().ToTestFuznReadableString()}</td>");
        b.AppendLine($"<td>{scenarioResult.StartTime().ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
        b.AppendLine($"<td>{scenarioResult.EndTime().ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>");
        b.AppendLine("</tr>");
        b.AppendLine("</table>");
    }

    private void WriteSteps(StringBuilder b, ScenarioLoadResult scenario)
    {
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

        // Summary rows
        b.AppendLine(@"<tr class=""summary"">");
        Cols(b, "All Steps (Summary)", true, scenario.Ok, scenario.Failed, 1);
        b.AppendLine("</tr>");
        
        // Only show Failed summary row if there are failures
        if (scenario.Failed.RequestCount > 0)
        {
            b.AppendLine(@"<tr class=""summary"">");
            Cols(b, "", false, scenario.Ok, scenario.Failed, 1);
            b.AppendLine("</tr>");
        }

        foreach (var step in scenario.Steps)
        {
            WriteStepRow(step.Value, 1, b);
        }

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");
    }

    private void WriteStepRow(StepLoadResult step, int level, StringBuilder b)
    {
        // OK row
        b.AppendLine(@"<tr>");
        Cols(b, step.Name, true, step.Ok, step.Failed, level);
        b.AppendLine("</tr>");
        
        // Failed row - only if there are failures
        if (step.Failed.RequestCount > 0)
        {
            b.AppendLine(@"<tr>");
            Cols(b, "", false, step.Ok, step.Failed, level);
            b.AppendLine("</tr>");
        }

        // Show errors if present
        if (step.Errors != null && step.Errors.Count > 0)
        {
            b.AppendLine($@"<tr><td colspan=""11"" style=""padding-left:{level * 20}px;"" class=""errors"">");
            b.AppendLine(@"<span style=""font-weight:bold"">Step Errors:</span><ul>");
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
            WriteStepRow(innerStep, level + 1, b);
    }

    private void WriteSnapshotTable(LoadReportData loadReportData, StringBuilder b, ScenarioLoadResult scenario)
    {
        var snapshots = InMemorySnapshotCollectorSinkPlugin.GetSnapshots(loadReportData.Group.Name, loadReportData.Test.Name, scenario.ScenarioName);

        if (snapshots == null || snapshots.Count == 0)
            return;

        b.AppendLine("<h3>Snapshot Details</h3>");

        // Snapshots Table
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

        foreach (var snapshot in snapshots)
        {
            // Summary OK row
            b.AppendLine(@"<tr class=""summary"">");
            b.AppendLine(@$"<td style=""width:1%;white-space:nowrap;"">{snapshot.Created:yyyy-MM-dd HH:mm:ss.fff}</td>");
            Cols(b, "All Steps (Summary)", true, snapshot.Ok, snapshot.Failed, 1);
            b.AppendLine("</tr>");

            // Summary Failed row - only if there are failures
            if (snapshot.Failed.RequestCount > 0)
            {
                b.AppendLine(@"<tr class=""summary"">");
                b.AppendLine($"<td></td>");
                Cols(b, "", false, snapshot.Ok, snapshot.Failed, 1);
                b.AppendLine("</tr>");
            }

            // Step rows
            foreach (var step in snapshot.Steps)
            {
                RenderSnapshotStep(b, step.Value);
            }
        }

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");
    }

    private void RenderSnapshotStep(StringBuilder b, StepLoadResult stepResult)
    {
        // OK row
        b.AppendLine(@"<tr>");
        b.AppendLine($"<td></td>"); // Empty timestamp column
        Cols(b, stepResult.Name, true, stepResult.Ok, stepResult.Failed, 1);
        b.AppendLine("</tr>");

        // Failed row - only if there are failures
        if (stepResult.Failed.RequestCount > 0)
        {
            b.AppendLine(@"<tr>");
            b.AppendLine($"<td></td>"); // Empty timestamp column
            Cols(b, "", false, stepResult.Ok, stepResult.Failed, 1);
            b.AppendLine("</tr>");
        }

        if (stepResult.Steps == null || stepResult.Steps.Count == 0)
            return;

        foreach (var innerStep in stepResult.Steps)
        {
            RenderSnapshotStep(b, innerStep);
        }
    }

    private static void WriteFailureDetails(ScenarioLoadResult scenarioResult, StringBuilder b)
    {
        if (scenarioResult.Status != TestStatus.Failed)
            return;

        b.AppendLine($"<h3>Failure Details</h3>");

        if (scenarioResult.AssertWhileRunningException != null)
        {
            b.AppendLine(@"<div class=""status-panel failed"">");
            b.AppendLine(@"<div class=""title"">AssertWhileRunning Failed</div>");
            b.AppendLine($@"<div class=""details"">{scenarioResult.AssertWhileRunningException.Message}</div>");
            b.AppendLine("</div>");
        }

        if (scenarioResult.AssertWhenDoneException != null)
        {
            b.AppendLine(@"<div class=""status-panel failed"">");
            b.AppendLine(@"<div class=""title"">AssertWhenDone Failed</div>");
            b.AppendLine($@"<div class=""details"">{scenarioResult.AssertWhenDoneException.Message}</div>");
            b.AppendLine("</div>");
        }
    }

    private static void Cols(StringBuilder b, string stepName, bool isOkRow, Stats okStats, Stats failedStats, int level)
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

        var cssClass = isOkRow ? "ok" : "failed";

        b.AppendLine($"<td{padding}>{prefix} {stepName}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{(isOkRow ? "Ok" : "Failed")}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.RequestCount}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.RequestsPerSecond}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.ResponseTimeMean.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.ResponseTimeMedian.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.ResponseTimePercentile75.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.ResponseTimePercentile99.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.ResponseTimeMin.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.ResponseTimeMax.ToTestFuznResponseTime()}</td>");
        b.AppendLine(@$"<td class=""{cssClass}"">{stats.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}</td>");
    }

    private void WriteChartScripts(LoadReportData loadReportData, StringBuilder b)
    {
        b.AppendLine("<script>");
        b.AppendLine("document.addEventListener('DOMContentLoaded', function() {");

        for (int i = 0; i < loadReportData.ScenarioResults.Count; i++)
        {
            var scenario = loadReportData.ScenarioResults[i];
            var scenarioIndex = i + 1;
            var stats = scenario.Ok;

            // Success Rate Donut Chart
            b.Append("var successCtx").Append(scenarioIndex).Append(" = document.getElementById('successChart").Append(scenarioIndex).AppendLine("');");
            b.Append("if (successCtx").Append(scenarioIndex).AppendLine(") {");
            b.Append("  new Chart(successCtx").Append(scenarioIndex).AppendLine(", {");
            b.AppendLine("    type: 'doughnut',");
            b.AppendLine("    data: {");
            b.AppendLine("      labels: ['OK', 'Failed'],");
            b.AppendLine("      datasets: [{");
            b.Append("        data: [").Append(scenario.Ok.RequestCount).Append(", ").Append(scenario.Failed.RequestCount).AppendLine("],");
            b.AppendLine("        backgroundColor: ['#10B981', '#EF4444'],");
            b.AppendLine("        borderWidth: 0,");
            b.AppendLine("        hoverOffset: 4");
            b.AppendLine("      }],");
            b.AppendLine("    }, ");
            b.AppendLine("    options: {");
            b.AppendLine("      responsive: true,");
            b.AppendLine("      maintainAspectRatio: true,");
            b.AppendLine("      cutout: '70%',");
            b.AppendLine("      plugins: {");
            b.AppendLine("        legend: { display: false },");
            b.AppendLine("        tooltip: {");
            b.AppendLine("          callbacks: {");
            b.AppendLine("            label: function(context) {");
            b.AppendLine("              var total = context.dataset.data.reduce(function(a, b) { return a + b; }, 0);");
            b.AppendLine("              var percentage = ((context.raw / total) * 100).toFixed(1);");
            b.AppendLine("              return context.label + ': ' + context.raw + ' (' + percentage + '%)';");
            b.AppendLine("            }");
            b.AppendLine("          }");
            b.AppendLine("        }");
            b.AppendLine("      }");
            b.AppendLine("    }");
            b.AppendLine("  });");
            b.AppendLine("}");

            // Response Time Bar Chart
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            b.Append("var rtCtx").Append(scenarioIndex).Append(" = document.getElementById('responseTimeChart").Append(scenarioIndex).AppendLine("');");
            b.Append("if (rtCtx").Append(scenarioIndex).AppendLine(") {");
            b.Append("  new Chart(rtCtx").Append(scenarioIndex).AppendLine(", {");
            b.AppendLine("    type: 'bar',");
            b.AppendLine("    data: {");
            b.AppendLine("      labels: ['Min', 'Mean', 'Median', 'P75', 'P95', 'P99', 'Max'],");
            b.AppendLine("      datasets: [{");
            b.AppendLine("        label: 'Response Time (ms)',");
            b.Append("        data: [")
                .Append(stats.ResponseTimeMin.TotalMilliseconds.ToString("F1", inv)).Append(", ")
                .Append(stats.ResponseTimeMean.TotalMilliseconds.ToString("F1", inv)).Append(", ")
                .Append(stats.ResponseTimeMedian.TotalMilliseconds.ToString("F1", inv)).Append(", ")
                .Append(stats.ResponseTimePercentile75.TotalMilliseconds.ToString("F1", inv)).Append(", ")
                .Append(stats.ResponseTimePercentile95.TotalMilliseconds.ToString("F1", inv)).Append(", ")
                .Append(stats.ResponseTimePercentile99.TotalMilliseconds.ToString("F1", inv)).Append(", ")
                .Append(stats.ResponseTimeMax.TotalMilliseconds.ToString("F1", inv))
                .AppendLine("],");
            b.AppendLine("        backgroundColor: ['#10B981', '#3B82F6', '#8B5CF6', '#6366F1', '#F59E0B', '#EF4444', '#DC2626'],");
            b.AppendLine("        borderRadius: 4");
            b.AppendLine("      }],");
            b.AppendLine("    }, ");
            b.AppendLine("    options: {");
            b.AppendLine("      responsive: true,");
            b.AppendLine("      maintainAspectRatio: false,");
            b.AppendLine("      plugins: { legend: { display: false } }, ");
            b.AppendLine("      scales: { y: { beginAtZero: true, title: { display: true, text: 'ms' } } }");
            b.AppendLine("    }");
            b.AppendLine("  });");
            b.AppendLine("}");

            // RPS and Timeline Charts (if snapshots exist)
            var snapshots = InMemorySnapshotCollectorSinkPlugin.GetSnapshots(loadReportData.Group.Name, loadReportData.Test.Name, scenario.ScenarioName);
            if (snapshots != null && snapshots.Count > 0)
            {
                var labels = string.Join(",", snapshots.Select(s => "'" + s.Created.ToString("HH:mm:ss") + "'"));
                var rpsData = string.Join(",", snapshots.Select(s => s.Ok.RequestsPerSecond.ToString(inv)));
                var meanData = string.Join(",", snapshots.Select(s => s.Ok.ResponseTimeMean.TotalMilliseconds.ToString("F1", inv)));
                var p99Data = string.Join(",", snapshots.Select(s => s.Ok.ResponseTimePercentile99.TotalMilliseconds.ToString("F1", inv)));

                // RPS Chart (standalone)
                b.Append("var rpsCtx").Append(scenarioIndex).Append(" = document.getElementById('rpsChart").Append(scenarioIndex).AppendLine("');");
                b.Append("if (rpsCtx").Append(scenarioIndex).AppendLine(") {");
                b.Append("  new Chart(rpsCtx").Append(scenarioIndex).AppendLine(", {");
                b.AppendLine("    type: 'line',");
                b.AppendLine("    data: {");
                b.Append("      labels: [").Append(labels).AppendLine("],");
                b.AppendLine("      datasets: [{");
                b.AppendLine("          label: 'Requests Per Second',");
                b.Append("          data: [").Append(rpsData).AppendLine("],");
                b.AppendLine("          borderColor: '#3B82F6',");
                b.AppendLine("          backgroundColor: 'rgba(59, 130, 246, 0.2)',");
                b.AppendLine("          fill: true,");
                b.AppendLine("          tension: 0.3,");
                b.AppendLine("          pointRadius: 3,");
                b.AppendLine("          pointBackgroundColor: '#3B82F6'");
                b.AppendLine("      }]},");
                b.AppendLine("    options: {");
                b.AppendLine("      responsive: true,");
                b.AppendLine("      maintainAspectRatio: false,");
                b.AppendLine("      interaction: { mode: 'index', intersect: false },");
                b.AppendLine("      plugins: { legend: { display: false } },");
                b.AppendLine("      scales: {");
                b.AppendLine("        y: { beginAtZero: true, title: { display: true, text: 'Requests/sec' } }");
                b.AppendLine("      }");
                b.AppendLine("    }");
                b.AppendLine("  });");
                b.AppendLine("}");

                // Timeline Chart (RPS + Response Times)
                b.Append("var tlCtx").Append(scenarioIndex).Append(" = document.getElementById('timelineChart").Append(scenarioIndex).AppendLine("');");
                b.Append("if (tlCtx").Append(scenarioIndex).AppendLine(") {");
                b.Append("  new Chart(tlCtx").Append(scenarioIndex).AppendLine(", {");
                b.AppendLine("    type: 'line',");
                b.AppendLine("    data: {");
                b.Append("      labels: [").Append(labels).AppendLine("],");
                b.AppendLine("      datasets: [");
                b.AppendLine("        {");
                b.AppendLine("          label: 'Mean (ms)',");
                b.Append("          data: [").Append(meanData).AppendLine("],");
                b.AppendLine("          borderColor: '#10B981',");
                b.AppendLine("          backgroundColor: 'rgba(16, 185, 129, 0.1)',");
                b.AppendLine("          fill: true,");
                b.AppendLine("          tension: 0.3,");
                b.AppendLine("          pointRadius: 3,");
                b.AppendLine("          pointBackgroundColor: '#10B981'");
                b.AppendLine("        },");
                b.AppendLine("        {");
                b.AppendLine("          label: 'P99 (ms)',");
                b.Append("          data: [").Append(p99Data).AppendLine("],");
                b.AppendLine("          borderColor: '#EF4444',");
                b.AppendLine("          borderDash: [5, 5],");
                b.AppendLine("          fill: false,");
                b.AppendLine("          tension: 0.3,");
                b.AppendLine("          pointRadius: 3,");
                b.AppendLine("          pointBackgroundColor: '#EF4444'");
                b.AppendLine("        }");
                b.AppendLine("      ]");
                b.AppendLine("    }, ");
                b.AppendLine("    options: {");
                b.AppendLine("      responsive: true,");
                b.AppendLine("      maintainAspectRatio: false,");
                b.AppendLine("      interaction: { mode: 'index', intersect: false }, ");
                b.AppendLine("      scales: {");
                b.AppendLine("        y: { beginAtZero: true, title: { display: true, text: 'Response Time (ms)' } }");
                b.AppendLine("      }");
                b.AppendLine("    }");
                b.AppendLine("  });");
                b.AppendLine("}");
            }
        }

        b.AppendLine("});");
        b.AppendLine("</script>");
    }

    private async Task IncludeEmbeddedResources(LoadReportData loadReportData)
    {
        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(
            "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Styles.testfuzn.css",
            Path.Combine(loadReportData.TestsOutputDirectory, "assets/styles/testfuzn.css"));

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(
            "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Scripts.chart.js",
            Path.Combine(loadReportData.TestsOutputDirectory, "assets/scripts/chart.js"));
    }
}
