using System.Text;
using TestFusion.Internals.State;
using TestFusion.Contracts.Reports;
using TestFusion.Contracts.Results.Load;

namespace TestFusion.Internals.Reports.Load;

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
            var filePath = Path.Combine(GlobalState.TestsOutputDirectory, $"TestFusion_Report_Load_{reportName}.html");

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

        var builder = new StringBuilder();

        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang='en'>");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset='UTF-8'>");
        builder.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        builder.AppendLine("<title>TestFusion - Load Test Report</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        builder.AppendLine("h1 { color: #333; }");
        builder.AppendLine("h2 { color: #555; margin-top: 40px; }");
        builder.AppendLine("h3 { color: #555; margin-top: 40px; }");
        builder.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
        builder.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        builder.AppendLine("th { background-color: #f4f4f4; }");
        builder.AppendLine(".metric-header { text-align: center; }");
        builder.AppendLine(".ok-row { background-color: #d4edda; }");
        builder.AppendLine(".failed-row { background-color: #ffcccc; }");
        builder.AppendLine(".details-btn { background: #eee; border: 1px solid #bbb; padding: 4px 12px; cursor: pointer; border-radius: 4px; font-size: 1em; }");
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");

        // Header
        builder.AppendLine($"<h1>TestFusion - Load Test Report</h>");
        builder.AppendLine($"<h2>Test Metadata</h2>");
        builder.AppendLine($"<h3>Test Info</h3>");
        builder.AppendLine("<table>");
        builder.AppendLine("<tbody>");
        builder.AppendLine("<tr>");
        builder.AppendLine("<th>Property</th>");
        builder.AppendLine("<th>Value</th>");
        builder.AppendLine("</tr");
        builder.AppendLine($"<tr><td>Test Run Id</td><td>{loadReportData.TestRunId}</td></tr>");
        builder.AppendLine($"<tr><td>Feature Name</td><td>{loadReportData.FeatureName}</td></tr>");
        builder.AppendLine($"<tr><td>Scenario Name</td><td>{loadReportData.ScenarioResult.ScenarioName}</td></tr>");
        builder.AppendLine("</table>");
        builder.AppendLine("</tbody>");
        
        builder.AppendLine($"<h3>Phase Timings</h3>");

        builder.AppendLine("<table>");
        builder.AppendLine("<tbody>");
        builder.AppendLine("<tr>");
        builder.AppendLine("<th>Phase</th>");
        builder.AppendLine("<th>Duration</th>");
        builder.AppendLine("<th>Started</th>");
        builder.AppendLine("<th>Ended</th>");
        builder.AppendLine("</tr>");
        // Test Run
        builder.AppendLine("<tr>");
        builder.AppendLine($"<td>Test Run</td>");
        builder.AppendLine($"<td>{scenarioResult.TestRunTotalDuration().ToTestFusionFormattedDuration()}</td>");
        builder.AppendLine($"<td>{scenarioResult.StartTime().ToLocalTime()}</td>");
        builder.AppendLine($"<td>{scenarioResult.EndTime().ToLocalTime()}</td>");
        builder.AppendLine("</tr>");
        // Init
        builder.AppendLine("<tr>");
        builder.AppendLine($"<td>Init</td>");
        builder.AppendLine($"<td>{scenarioResult.InitTotalDuration().ToTestFusionFormattedDuration()}</td>");
        builder.AppendLine($"<td>{scenarioResult.InitStartTime.ToLocalTime()}</td>");
        builder.AppendLine($"<td>{scenarioResult.InitEndTime.ToLocalTime()}</td>");
        builder.AppendLine("</tr>");
        // Warmup
        if (loadReportData.ScenarioResult.HasWarmupStep())
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>Warmup</td>");
            builder.AppendLine($"<td>{loadReportData.ScenarioResult.WarmupTotalDuration().ToTestFusionFormattedDuration()}</td>");
            builder.AppendLine($"<td>{loadReportData.ScenarioResult.WarmupStartTime.ToLocalTime()}</td>");
            builder.AppendLine($"<td>{loadReportData.ScenarioResult.WarmupEndTime.ToLocalTime()}</td>");
            builder.AppendLine("</tr>");
        }
        // Execution
        builder.AppendLine("<tr>");
        builder.AppendLine($"<td>Execution</td>");
        builder.AppendLine($"<td>{scenarioResult.MeasurementTotalDuration().ToTestFusionFormattedDuration()}</td>");
        builder.AppendLine($"<td>{scenarioResult.MeasurementStartTime.ToLocalTime()}</td>");
        builder.AppendLine($"<td>{scenarioResult.MeasurementEndTime.ToLocalTime()}</td>");
        builder.AppendLine("</tr>");
        // Cleanup
        builder.AppendLine("<tr>");
        builder.AppendLine($"<td>Cleanup</td>");
        builder.AppendLine($"<td>{scenarioResult.CleanupTotalDuration().ToTestFusionFormattedDuration()}</td>");
        builder.AppendLine($"<td>{scenarioResult.CleanupStartTime.ToLocalTime()}</td>");
        builder.AppendLine($"<td>{scenarioResult.CleanupEndTime.ToLocalTime()}</td>");
        builder.AppendLine("</tr>");

        builder.AppendLine("</tbody>");
        builder.AppendLine("</table>");

        builder.AppendLine($"<h2>Test Statistics</h2>");

        var chartDataList = new List<string>();
        int chartIndex = 0;

        // Add a canvas for the pie chart
        string chartId = $"chart_{chartIndex}";
        builder.AppendLine($"<div class='chart-container'><canvas id='{chartId}'></canvas></div>");

        // Prepare chart data for JavaScript
        chartDataList.Add($@"{{ 
            id: '{chartId}', 
            label: 'Request Status Distribution', 
            ok: {loadReportData.ScenarioResult.Ok.RequestCount}, 
            notOk: {loadReportData.ScenarioResult.Failed.RequestCount},
            requestCount: {loadReportData.ScenarioResult.RequestCount}
        }}");

        chartIndex++;

        // Table for the scenario
        builder.AppendLine("<table>");
        builder.AppendLine("<thead>");
        builder.AppendLine("<tr>");
        builder.AppendLine("<th>Step</th>");
        builder.AppendLine("<th>Type</th>");
        builder.AppendLine("<th>RPS</th>");
        builder.AppendLine("<th>Requests</th>");
        builder.AppendLine("<th>Mean (ms)</th>");
        builder.AppendLine("<th>Median (ms)</th>");
        builder.AppendLine("<th>P75 (ms)</th>");
        builder.AppendLine("<th>P99 (ms)</th>");
        builder.AppendLine("<th>Min (ms)</th>");
        builder.AppendLine("<th>Max (ms)</th>");
        builder.AppendLine("<th>StdDev (ms)</th>");
        builder.AppendLine("</tr>");
        builder.AppendLine("</thead>");
        builder.AppendLine("<tbody>");

        // All rows.
        var scenario = loadReportData.ScenarioResult;
        builder.AppendLine("<tr>");
        Rows(builder, "All steps", "Ok", scenario.Ok);
        builder.AppendLine("</tr>");
        builder.AppendLine("<tr>");
        Rows(builder, "", "Failed", scenario.Failed);
        builder.AppendLine("</tr>");

        foreach (var step in scenario.Steps)
        {
            // Step rows.
            builder.AppendLine("<tr>");
            Rows(builder, step.Value.Name, "Ok", step.Value.Ok);
            builder.AppendLine("</tr>");
            builder.AppendLine("<tr>");
            Rows(builder, "", "Failed", step.Value.Failed);
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody>");
        builder.AppendLine("</table>");

        // Table for snapshots
        builder.AppendLine("<h2>Snapshot Timeline</h2>");

        builder.AppendLine("<table>");
        builder.AppendLine("<thead>");
        builder.AppendLine("<tr>");
        builder.AppendLine("<th>Created</th>");
        builder.AppendLine("<th>Step</th>");
        builder.AppendLine("<th>Type</th>");
        builder.AppendLine("<th>RPS</th>");
        builder.AppendLine("<th>Requests</th>");
        builder.AppendLine("<th>Mean (ms)</th>");
        builder.AppendLine("<th>Median (ms)</th>");
        builder.AppendLine("<th>P75 (ms)</th>");
        builder.AppendLine("<th>P99 (ms)</th>");
        builder.AppendLine("<th>Min (ms)</th>");
        builder.AppendLine("<th>Max (ms)</th>");
        builder.AppendLine("<th>StdDev (ms)</th>");
        builder.AppendLine("</tr>");
        builder.AppendLine("</thead>");
        builder.AppendLine("<tbody>");

        var snapshots = InMemorySnapshotCollectorSinkPlugin.GetSnapshots(loadReportData.FeatureName, scenario.ScenarioName);

        foreach (var snapshot in snapshots)
        {
            builder.AppendLine("<tr class='snapshot'>");
            builder.AppendLine($"<td>{snapshot.Created:yyyy-MM-dd HH:mm:ss.fff}</td>");
            Rows(builder, "All steps", "Ok", snapshot.Ok);
            builder.AppendLine("</tr>");
            builder.AppendLine("<tr class='snapshot'>");
            builder.AppendLine($"<td></td>");
            Rows(builder, "", "Failed", snapshot.Failed);
            builder.AppendLine("</tr>");
         
            foreach (var step in scenario.Steps)
            {
                builder.AppendLine("<tr>");
                builder.AppendLine($"<td></td>");
                Rows(builder, step.Value.Name, "Ok", step.Value.Ok);
                builder.AppendLine("</tr>");
                builder.AppendLine("<tr>");
                builder.AppendLine($"<td></td>");
                Rows(builder, "", "Failed", step.Value.Failed);
                builder.AppendLine("</tr>");
            }
        }

        builder.AppendLine("</tbody>");
        builder.AppendLine("</table>");

        // Chart.js CDN and script
        builder.AppendLine("<script src='assets/scripts/chart.js'></script>");
        builder.AppendLine("<script>");
        builder.AppendLine("const chartData = [");
        builder.AppendLine(string.Join(",", chartDataList));
        builder.AppendLine("];");
        builder.AppendLine(@"
        chartData.forEach(data => {
            const ctx = document.getElementById(data.id).getContext('2d');
            new Chart(ctx, {
                type: 'pie',
                data: {
                    labels: [`OK: ${data.ok}`, `Not OK: ${data.notOk}`],
                    datasets: [{
                        data: [data.ok, data.notOk],
                        backgroundColor: ['#28a745', '#dc3545'],
                    }]
                },
                options: {
                    responsive: false,
                    plugins: {
                        title: {
                            display: true,
                            text: `Request Status Distribution | OK: ${data.ok} | Not OK: ${data.notOk} | Total: ${data.requestCount}`
                        },
                        legend: {
                            position: 'bottom'
                        }
                    }
                }
            });
        });
        function toggleDetails(id) {
            var rows = document.querySelectorAll('tr[id=' + id + ']');
            rows.forEach(function(row) {
                if (row.style.display === 'none') {
                    row.style.display = '';
                } else {
                    row.style.display = 'none';
                }
            });
        }
        ");
        builder.AppendLine("</script>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        return builder.ToString();
    }
    
    private void Rows(StringBuilder builder, string stepName, string type, Stats stats)
    {
        builder.AppendLine($"<td>{stepName}</td>");
        builder.AppendLine($"<td>{type}</td>");
        builder.AppendLine($"<td>{stats.RequestsPerSecond}</td>");
        builder.AppendLine($"<td>{stats.RequestCount}</td>");
        builder.AppendLine($"<td>{stats.ResponseTimeMean.ToTestFusionResponseTime()}</td>");
        builder.AppendLine($"<td>{stats.ResponseTimeMedian.ToTestFusionResponseTime()}</td>");
        builder.AppendLine($"<td>{stats.ResponseTimePercentile75.ToTestFusionResponseTime()}</td>");
        builder.AppendLine($"<td>{stats.ResponseTimePercentile99.ToTestFusionResponseTime()}</td>");
        builder.AppendLine($"<td>{stats.ResponseTimeMin.ToTestFusionResponseTime()}</td>");
        builder.AppendLine($"<td>{stats.ResponseTimeMax.ToTestFusionResponseTime()}</td>");
        builder.AppendLine($"<td>{stats.ResponseTimeStandardDeviation.ToTestFusionResponseTime()}</td>");
    }
}
