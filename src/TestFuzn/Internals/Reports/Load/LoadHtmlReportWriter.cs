using System.Text;
using Fuzn.TestFuzn.Internals.State;
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

        var b = new StringBuilder();

        b.AppendLine("<!DOCTYPE html>");
        b.AppendLine("<html lang='en'>");
        b.AppendLine("<head>");
        b.AppendLine("<meta charset='UTF-8'>");
        b.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        b.AppendLine("<title>TestFusion - Load Test Report</title>");
        b.AppendLine("<link rel='stylesheet' href='assets/styles/testfuzn.css'>");
        b.AppendLine("</head>");
        b.AppendLine("<body>");

        // Header
        b.AppendLine($"<h1>TestFusion - Load Test Report</h>");
        b.AppendLine($"<h2>Test Metadata</h2>");
        b.AppendLine($"<h3>Test Info</h3>");
        b.AppendLine("<table>");
        b.AppendLine("<tbody>");
        b.AppendLine("<tr>");
        b.AppendLine("<th>Property</th>");
        b.AppendLine("<th>Value</th>");
        b.AppendLine("</tr");
        b.AppendLine($"<tr><td>Test Run Id</td><td>{loadReportData.TestRunId}</td></tr>");
        b.AppendLine($"<tr><td>Feature Name</td><td>{loadReportData.FeatureName}</td></tr>");
        b.AppendLine($"<tr><td>Scenario Name</td><td>{loadReportData.ScenarioResult.ScenarioName}</td></tr>");
        b.AppendLine("</tbody>");
        b.AppendLine("</table>");
        
        
        b.AppendLine($"<h3>Phase Timings</h3>");

        b.AppendLine("<table>");
        b.AppendLine("<tbody>");
        b.AppendLine("<tr>");
        b.AppendLine("<th>Phase</th>");
        b.AppendLine("<th>Duration</th>");
        b.AppendLine("<th>Started</th>");
        b.AppendLine("<th>Ended</th>");
        b.AppendLine("</tr>");
        // Init
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Init</td>");
        b.AppendLine($"<td>{scenarioResult.InitTotalDuration().ToTestFusionFormattedDuration()}</td>");
        b.AppendLine($"<td>{scenarioResult.InitStartTime.ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.InitEndTime.ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        // Warmup
        if (loadReportData.ScenarioResult.HasWarmupStep())
        {
            b.AppendLine("<tr>");
            b.AppendLine($"<td>Warmup</td>");
            b.AppendLine($"<td>{loadReportData.ScenarioResult.WarmupTotalDuration().ToTestFusionFormattedDuration()}</td>");
            b.AppendLine($"<td>{loadReportData.ScenarioResult.WarmupStartTime.ToLocalTime()}</td>");
            b.AppendLine($"<td>{loadReportData.ScenarioResult.WarmupEndTime.ToLocalTime()}</td>");
            b.AppendLine("</tr>");
        }
        // Execution
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Execution</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementTotalDuration().ToTestFusionFormattedDuration()}</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementStartTime.ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.MeasurementEndTime.ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        // Cleanup
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Cleanup</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupTotalDuration().ToTestFusionFormattedDuration()}</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupStartTime.ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.CleanupEndTime.ToLocalTime()}</td>");
        b.AppendLine("</tr>");
        // Test Run
        b.AppendLine("<tr>");
        b.AppendLine($"<td>Total Test Run</td>");
        b.AppendLine($"<td>{scenarioResult.TestRunTotalDuration().ToTestFusionFormattedDuration()}</td>");
        b.AppendLine($"<td>{scenarioResult.StartTime().ToLocalTime()}</td>");
        b.AppendLine($"<td>{scenarioResult.EndTime().ToLocalTime()}</td>");
        b.AppendLine("</tr>");

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");

        b.AppendLine($"<h2>Test Statistics</h2>");

        var chartDataList = new List<string>();
        int chartIndex = 0;

        // Add a canvas for the pie chart
        string chartId = $"chart_{chartIndex}";
        b.AppendLine($"<div class='chart-container'><canvas id='{chartId}'></canvas></div>");

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
        b.AppendLine("<table>");
        b.AppendLine("<thead>");
        b.AppendLine("<tr>");
        b.AppendLine("<th>Step</th>");
        b.AppendLine("<th>Type</th>");
        b.AppendLine("<th>RPS</th>");
        b.AppendLine("<th>Requests</th>");
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
        b.AppendLine("<tr>");
        Rows(b, "All steps", "Ok", scenario.Ok);
        b.AppendLine("</tr>");
        b.AppendLine("<tr>");
        Rows(b, "", "Failed", scenario.Failed);
        b.AppendLine("</tr>");

        foreach (var step in scenario.Steps)
        {
            // Step rows.
            b.AppendLine("<tr>");
            Rows(b, step.Value.Name, "Ok", step.Value.Ok);
            b.AppendLine("</tr>");
            b.AppendLine("<tr>");
            Rows(b, "", "Failed", step.Value.Failed);
            b.AppendLine("</tr>");
        }

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");

        // Table for snapshots
        b.AppendLine("<h2>Snapshot Timeline</h2>");

        b.AppendLine("<table>");
        b.AppendLine("<thead>");
        b.AppendLine("<tr>");
        b.AppendLine("<th>Created</th>");
        b.AppendLine("<th>Step</th>");
        b.AppendLine("<th>Type</th>");
        b.AppendLine("<th>RPS</th>");
        b.AppendLine("<th>Requests</th>");
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

        var snapshots = InMemorySnapshotCollectorSinkPlugin.GetSnapshots(loadReportData.FeatureName, scenario.ScenarioName);

        foreach (var snapshot in snapshots)
        {
            b.AppendLine("<tr class='snapshot'>");
            b.AppendLine($"<td>{snapshot.Created:yyyy-MM-dd HH:mm:ss.fff}</td>");
            Rows(b, "All steps", "Ok", snapshot.Ok);
            b.AppendLine("</tr>");
            b.AppendLine("<tr class='snapshot'>");
            b.AppendLine($"<td></td>");
            Rows(b, "", "Failed", snapshot.Failed);
            b.AppendLine("</tr>");
         
            foreach (var step in scenario.Steps)
            {
                b.AppendLine("<tr>");
                b.AppendLine($"<td></td>");
                Rows(b, step.Value.Name, "Ok", step.Value.Ok);
                b.AppendLine("</tr>");
                b.AppendLine("<tr>");
                b.AppendLine($"<td></td>");
                Rows(b, "", "Failed", step.Value.Failed);
                b.AppendLine("</tr>");
            }
        }

        b.AppendLine("</tbody>");
        b.AppendLine("</table>");

        // Chart.js CDN and script
        b.AppendLine("<script src='assets/scripts/chart.js'></script>");
        b.AppendLine("<script>");
        b.AppendLine("const chartData = [");
        b.AppendLine(string.Join(",", chartDataList));
        b.AppendLine("];");
        b.AppendLine(@"
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
        b.AppendLine("</script>");
        b.AppendLine("</body>");
        b.AppendLine("</html>");

        return b.ToString();
    }
    
    private void Rows(StringBuilder b, string stepName, string type, Stats stats)
    {
        b.AppendLine($"<td>{stepName}</td>");
        b.AppendLine($"<td>{type}</td>");
        b.AppendLine($"<td>{stats.RequestsPerSecond}</td>");
        b.AppendLine($"<td>{stats.RequestCount}</td>");
        b.AppendLine($"<td>{stats.ResponseTimeMean.ToTestFusionResponseTime()}</td>");
        b.AppendLine($"<td>{stats.ResponseTimeMedian.ToTestFusionResponseTime()}</td>");
        b.AppendLine($"<td>{stats.ResponseTimePercentile75.ToTestFusionResponseTime()}</td>");
        b.AppendLine($"<td>{stats.ResponseTimePercentile99.ToTestFusionResponseTime()}</td>");
        b.AppendLine($"<td>{stats.ResponseTimeMin.ToTestFusionResponseTime()}</td>");
        b.AppendLine($"<td>{stats.ResponseTimeMax.ToTestFusionResponseTime()}</td>");
        b.AppendLine($"<td>{stats.ResponseTimeStandardDeviation.ToTestFusionResponseTime()}</td>");
    }
}
