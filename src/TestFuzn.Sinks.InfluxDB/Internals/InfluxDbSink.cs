using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Sinks.InfluxDB.Internals;

internal class InfluxDbSink : ISinkPlugin
{
    private InfluxDBClient _client;
    private InfluxDbSinkConfiguration _config;

    public InfluxDbSink(InfluxDbSinkConfiguration config)
    {
        _config = config;
    }

    public Task InitSuite()
    {
        _client = new InfluxDBClient(_config.Url, _config.Token);
        return Task.CompletedTask;
    }

    public async Task WriteStats(string testRunId, string groupName, string testName, ScenarioLoadResult scenarioResult)
    {
        var points = new List<PointData>();

        // Scenario-level metrics
        var scenarioPoint = PointData
            .Measurement("scenario_metrics")
            .Tag("test_run_id", testRunId)
            .Tag("group_name", groupName)
            .Tag("test_name", testName)
            .Tag("scenario_name", scenarioResult.ScenarioName)
            .Field("request_count", scenarioResult.RequestCount)
            .Field("total_execution_duration_ms", scenarioResult.TotalExecutionDuration.TotalMilliseconds)
            .Field("requests_per_second", scenarioResult.RequestsPerSecond)

            // OK metrics
            .Field("ok_request_count", scenarioResult.Ok.RequestCount)
            .Field("ok_requests_per_second", scenarioResult.Ok.RequestsPerSecond)
            .Field("ok_response_time_min_ms", scenarioResult.Ok.ResponseTimeMin.TotalMilliseconds)
            .Field("ok_response_time_max_ms", scenarioResult.Ok.ResponseTimeMax.TotalMilliseconds)
            .Field("ok_response_time_mean_ms", scenarioResult.Ok.ResponseTimeMean.TotalMilliseconds)
            .Field("ok_response_time_stddev_ms", scenarioResult.Ok.ResponseTimeStandardDeviation.TotalMilliseconds)
            .Field("ok_response_time_median_ms", scenarioResult.Ok.ResponseTimeMedian.TotalMilliseconds)
            .Field("ok_response_time_percentile_75_ms", scenarioResult.Ok.ResponseTimePercentile75.TotalMilliseconds)
            .Field("ok_response_time_percentile_95_ms", scenarioResult.Ok.ResponseTimePercentile95.TotalMilliseconds)
            .Field("ok_response_time_percentile_99_ms", scenarioResult.Ok.ResponseTimePercentile99.TotalMilliseconds)
            .Field("ok_total_execution_duration_ms", scenarioResult.Ok.TotalExecutionDuration.TotalMilliseconds)

            // Failed metrics
            .Field("failed_request_count", scenarioResult.Failed.RequestCount)
            .Field("failed_requests_per_second", scenarioResult.Failed.RequestsPerSecond)
            .Field("failed_response_time_min_ms", scenarioResult.Failed.ResponseTimeMin.TotalMilliseconds)
            .Field("failed_response_time_max_ms", scenarioResult.Failed.ResponseTimeMax.TotalMilliseconds)
            .Field("failed_response_time_mean_ms", scenarioResult.Failed.ResponseTimeMean.TotalMilliseconds)
            .Field("failed_response_time_stddev_ms", scenarioResult.Failed.ResponseTimeStandardDeviation.TotalMilliseconds)
            .Field("failed_response_time_median_ms", scenarioResult.Failed.ResponseTimeMedian.TotalMilliseconds)
            .Field("failed_response_time_percentile_75_ms", scenarioResult.Failed.ResponseTimePercentile75.TotalMilliseconds)
            .Field("failed_response_time_percentile_95_ms", scenarioResult.Failed.ResponseTimePercentile95.TotalMilliseconds)
            .Field("failed_response_time_percentile_99_ms", scenarioResult.Failed.ResponseTimePercentile99.TotalMilliseconds)
            .Field("failed_total_execution_duration_ms", scenarioResult.Failed.TotalExecutionDuration.TotalMilliseconds)

            .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        points.Add(scenarioPoint);

        // Step-level metrics
        foreach (var step in scenarioResult.Steps)
        {
            WritStepStats(step.Value);
        }

        void WritStepStats(StepLoadResult stepResult)
        {
            var stepPoint = PointData
                .Measurement("step_metrics")
                .Tag("test_run_id", testRunId)
                .Tag("group_name", groupName)
                .Tag("test_name", testName)
                .Tag("scenario_name", scenarioResult.ScenarioName)
                .Tag("step_name", stepResult.Name)
                .Field("total_execution_duration_ms", stepResult.TotalExecutionDuration.TotalMilliseconds)

                // OK metrics
                .Field("ok_request_count", stepResult.Ok.RequestCount)
                .Field("ok_requests_per_second", stepResult.Ok.RequestsPerSecond)
                .Field("ok_response_time_min_ms", stepResult.Ok.ResponseTimeMin.TotalMilliseconds)
                .Field("ok_response_time_max_ms", stepResult.Ok.ResponseTimeMax.TotalMilliseconds)
                .Field("ok_response_time_mean_ms", stepResult.Ok.ResponseTimeMean.TotalMilliseconds)
                .Field("ok_response_time_stddev_ms", stepResult.Ok.ResponseTimeStandardDeviation.TotalMilliseconds)
                .Field("ok_response_time_median_ms", stepResult.Ok.ResponseTimeMedian.TotalMilliseconds)
                .Field("ok_response_time_percentile_75_ms", stepResult.Ok.ResponseTimePercentile75.TotalMilliseconds)
                .Field("ok_response_time_percentile_95_ms", stepResult.Ok.ResponseTimePercentile95.TotalMilliseconds)
                .Field("ok_response_time_percentile_99_ms", stepResult.Ok.ResponseTimePercentile99.TotalMilliseconds)
                .Field("ok_total_execution_duration_ms", stepResult.Ok.TotalExecutionDuration.TotalMilliseconds)

                // Failed metrics
                .Field("failed_request_count", stepResult.Failed.RequestCount)
                .Field("failed_requests_per_second", stepResult.Failed.RequestsPerSecond)
                .Field("failed_response_time_min_ms", stepResult.Failed.ResponseTimeMin.TotalMilliseconds)
                .Field("failed_response_time_max_ms", stepResult.Failed.ResponseTimeMax.TotalMilliseconds)
                .Field("failed_response_time_mean_ms", stepResult.Failed.ResponseTimeMean.TotalMilliseconds)
                .Field("failed_response_time_stddev_ms", stepResult.Failed.ResponseTimeStandardDeviation.TotalMilliseconds)
                .Field("failed_response_time_median_ms", stepResult.Failed.ResponseTimeMedian.TotalMilliseconds)
                .Field("failed_response_time_percentile_75_ms", stepResult.Failed.ResponseTimePercentile75.TotalMilliseconds)
                .Field("failed_response_time_percentile_95_ms", stepResult.Failed.ResponseTimePercentile95.TotalMilliseconds)
                .Field("failed_response_time_percentile_99_ms", stepResult.Failed.ResponseTimePercentile99.TotalMilliseconds)
                .Field("failed_total_execution_duration_ms", stepResult.Failed.TotalExecutionDuration.TotalMilliseconds)
                .Timestamp(scenarioResult.Created, WritePrecision.Ns);

            points.Add(stepPoint);

            if (stepResult.Steps == null || stepResult.Steps.Count == 0)
                return;

            foreach (var innerStep in stepResult.Steps)
            {
                WritStepStats(innerStep);
            }
        }

        var writeApi = _client.GetWriteApiAsync();
        await writeApi.WritePointsAsync(points, _config.Bucket, _config.Org);
    }

    public Task CleanupSuite()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }
}
