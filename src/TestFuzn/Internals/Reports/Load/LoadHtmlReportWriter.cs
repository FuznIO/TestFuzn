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
            var reportName = FileNameHelper.MakeFilenameSafe(loadReportData.ScenarioResult.ScenarioName);
            var filePath = Path.Combine(GlobalState.TestsOutputDirectory, $"Load-Report-{reportName}.html");

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
        var scenarioResult = loadReportData.ScenarioResult;

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

        // Header
        b.AppendLine($"<h1>TestFuzn - Load Test Report</h>");
        b.AppendLine($"<h2>Test Info</h2>");
        b.AppendLine("<table>");
        b.AppendLine("<tr>");
        b.AppendLine("<th>Property</th>");
        b.AppendLine("<th>Value</th>");
        b.AppendLine("</tr");
        b.AppendLine($"<tr><td>Test Run ID</td><td>{loadReportData.TestRunId}</td></tr>");
        b.AppendLine($"<tr><td>Test Suite Name</td><td>{loadReportData.TestSuite.Name}</td></tr>");
        b.AppendLine($"<tr><td>Test Suite ID</td><td>{loadReportData.TestSuite.Id}</td></tr>");

        if (loadReportData.TestSuite.Metadata != null && loadReportData.TestSuite.Metadata.Count > 0)
        {
            b.AppendLine("<tr><td>Test Suite Metadata</td><td><ul>");
            foreach (var metadata in loadReportData.TestSuite.Metadata)
                b.AppendLine($"<li>{metadata.Key}: {metadata.Value}</li>");
            b.AppendLine("</ul></td></tr>");
        }
        b.AppendLine($"<tr><td>Feature Name</td><td>{loadReportData.Feature.Name}</td></tr>");
        b.AppendLine($"<tr><td>Feature Id</td><td>{loadReportData.Feature.Id}</td></tr>");

        if (loadReportData.Feature.Metadata != null && loadReportData.Feature.Metadata.Count > 0)
        {
            b.AppendLine("<tr><td>Feature Metadata</td><td><ul>");
            foreach (var metadata in loadReportData.Feature.Metadata)
                b.AppendLine($"<li>{metadata.Key}: {metadata.Value}</li>");
            b.AppendLine("</ul></td></tr>");
        }

        b.AppendLine($"<tr><td>Scenario Name</td><td>{loadReportData.ScenarioResult.ScenarioName}</td></tr>");
        b.AppendLine($"<tr><td>Scenario Id</td><td>{loadReportData.ScenarioResult.Id}</td></tr>");

        if (loadReportData.ScenarioResult.Tags != null && loadReportData.ScenarioResult.Tags.Count > 0)
        {
            b.AppendLine("<tr><td>Scenario Tags</td><td><ul>");
            foreach (var tag in loadReportData.ScenarioResult.Tags)
                b.AppendLine($"<li>{tag}</li>");
            b.AppendLine("</ul></td></tr>");
        }

        if (loadReportData.ScenarioResult.Metadata != null && loadReportData.ScenarioResult.Metadata.Count > 0)
        {
            b.AppendLine("<tr><td>Scenario Metadata</td><td><ul>");
            foreach (var metadata in loadReportData.ScenarioResult.Metadata)
                b.AppendLine($"<li>{metadata.Key}: {metadata.Value}</li>");
            b.AppendLine("</ul></td></tr>");
        }

        b.AppendLine("<tr>");
        b.AppendLine("<td>Simulations</td><td><ul>");
        foreach (var simulation in scenarioResult.Simulations)
            b.AppendLine($"<li>{simulation}</li>");
        b.AppendLine("</li></td></tr>");
        b.AppendLine("</table>");

        b.AppendLine("</table>");

        b.AppendLine($"<h2>Phase Timings</h2>");
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
        b.AppendLine($"<td>{scenarioResult.InitTotalDuration().ToTestFuznResponseTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.InitStartTime.ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.InitEndTime.ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        // Warmup
        if (loadReportData.ScenarioResult.HasWarmupStep())
        {
            b.AppendLine("<tr>");
            b.AppendLine($"<td>Warmup</td>");
            b.AppendLine($"<td>{loadReportData.ScenarioResult.WarmupTotalDuration().ToTestFuznResponseTime()}</td>");
            b.AppendLine($"<td>{loadReportData.ScenarioResult.WarmupStartTime.ToLocalTime()}</td>");
            b.AppendLine($"<td>{loadReportData.ScenarioResult.WarmupEndTime.ToLocalTime()}</td>");
            b.AppendLine("</tr>");
        }
        // Execution
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Execution</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementTotalDuration().ToTestFuznResponseTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementStartTime.ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementEndTime.ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        // Cleanup
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Cleanup</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupTotalDuration().ToTestFuznResponseTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupStartTime.ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupEndTime.ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        // Test Run
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Total Test Run</td>");
        b.AppendLine($"<td>{scenarioResult.TestRunTotalDuration().ToTestFuznResponseTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.StartTime().ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.EndTime().ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        b.AppendLine("</table>");

        b.AppendLine($"<h2>Test Statistics</h2>");

        //var chartDataList = new List<string>();
        //int chartIndex = 0;

        //// Add a canvas for the pie chart
        //string chartId = $"chart_{chartIndex}";
        //b.AppendLine($"<div class='chart-container'><canvas id='{chartId}'></canvas></div>");

        //// Prepare chart data for JavaScript
        //chartDataList.Add($@"{{ 
        //    id: '{chartId}', 
        //    label: 'Request Status Distribution', 
        //    ok: {loadReportData.ScenarioResult.Ok.RequestCount}, 
        //    notOk: {loadReportData.ScenarioResult.Failed.RequestCount},
        //    requestCount: {loadReportData.ScenarioResult.RequestCount}
        //}}");

        //chartIndex++;

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
        var scenario = loadReportData.ScenarioResult;
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

            if (step.Steps == null || step.Steps.Count == 0)
                return;

            foreach (var innerStep in step.Steps)
                WriteStepRow(innerStep, level + 1);
        }

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");

        // Table for snapshots
        b.AppendLine("<h2>Snapshot Timeline</h2>");

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

        var snapshots = InMemorySnapshotCollectorSinkPlugin.GetSnapshots(loadReportData.Feature.Name, scenario.ScenarioName);

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

        //// Chart.js CDN and script
        //b.AppendLine("<script src='assets/scripts/chart.js'></script>");
        //b.AppendLine("<script>");
        //b.AppendLine("const chartData = [");
        //b.AppendLine(string.Join(",", chartDataList));
        //b.AppendLine("];");
        //b.AppendLine(@"
        //chartData.forEach(data => {
        //    const ctx = document.getElementById(data.id).getContext('2d');
        //    new Chart(ctx, {
        //        type: 'pie',
        //        data: {
        //            labels: [`OK: ${data.ok}`, `Not OK: ${data.notOk}`],
        //            datasets: [{
        //                data: [data.ok, data.notOk],
        //                backgroundColor: ['#28a745', '#dc3545'],
        //            }]
        //        },
        //        options: {
        //            responsive: false,
        //            plugins: {
        //                title: {
        //                    display: true,
        //                    text: `Request Status Distribution | OK: ${data.ok} | Not OK: ${data.notOk} | Total: ${data.requestCount}`
        //                },
        //                legend: {
        //                    position: 'bottom'
        //                }
        //            }
        //        }
        //    });
        //});
        //function toggleDetails(id) {
        //    var rows = document.querySelectorAll('tr[id=' + id + ']');
        //    rows.forEach(function(row) {
        //        if (row.style.display === 'none') {
        //            row.style.display = '';
        //        } else {
        //            row.style.display = 'none';
        //        }
        //    });
        //}
        //");
        //b.AppendLine("</script>");
        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
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
