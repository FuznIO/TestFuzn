using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using TestFusion.Contracts.Sinks;
using TestFusion.Contracts.Results.Load;

namespace TestFusion.Sinks.InfluxDB;

public class InfluxDbSink : ISinkPlugin
{
    private InfluxDBClient _client;
    private InfluxDbSinkConfiguration _config;

    public InfluxDbSink(InfluxDbSinkConfiguration config)
    {
        _config = config;
    }

    public Task InitGlobal()
    {
        _client = new InfluxDBClient(_config.Url, _config.Token);
        return Task.CompletedTask;
    }

    public async Task WriteMetrics(string testRunId, string featureName, ScenarioLoadResult results)
    {
        var points = new List<PointData>();

        // Scenario-level metrics
        var scenarioPoint = PointData
            .Measurement("scenario_metrics")
            .Tag("test_run_id", testRunId)
            .Tag("feature_name", featureName)
            .Tag("scenario_name", results.ScenarioName)
            .Field("request_count", results.RequestCount)
            .Field("total_execution_duration_ms", results.TotalExecutionDuration.TotalMilliseconds)
            .Field("requests_per_second", results.RequestsPerSecond)

            // OK metrics
            .Field("ok_request_count", results.Ok.RequestCount)
            .Field("ok_requests_per_second", results.Ok.RequestsPerSecond)
            .Field("ok_response_time_min_ms", results.Ok.ResponseTimeMin.TotalMilliseconds)
            .Field("ok_response_time_max_ms", results.Ok.ResponseTimeMax.TotalMilliseconds)
            .Field("ok_response_time_mean_ms", results.Ok.ResponseTimeMean.TotalMilliseconds)
            .Field("ok_response_time_stddev_ms", results.Ok.ResponseTimeStandardDeviation.TotalMilliseconds)
            .Field("ok_response_time_median_ms", results.Ok.ResponseTimeMedian.TotalMilliseconds)
            .Field("ok_response_time_percentile_75_ms", results.Ok.ResponseTimePercentile75.TotalMilliseconds)
            .Field("ok_response_time_percentile_95_ms", results.Ok.ResponseTimePercentile95.TotalMilliseconds)
            .Field("ok_response_time_percentile_99_ms", results.Ok.ResponseTimePercentile99.TotalMilliseconds)
            .Field("ok_total_execution_duration_ms", results.Ok.TotalExecutionDuration.TotalMilliseconds)

            // Failed metrics
            .Field("failed_request_count", results.Failed.RequestCount)
            .Field("failed_requests_per_second", results.Failed.RequestsPerSecond)
            .Field("failed_response_time_min_ms", results.Failed.ResponseTimeMin.TotalMilliseconds)
            .Field("failed_response_time_max_ms", results.Failed.ResponseTimeMax.TotalMilliseconds)
            .Field("failed_response_time_mean_ms", results.Failed.ResponseTimeMean.TotalMilliseconds)
            .Field("failed_response_time_stddev_ms", results.Failed.ResponseTimeStandardDeviation.TotalMilliseconds)
            .Field("failed_response_time_median_ms", results.Failed.ResponseTimeMedian.TotalMilliseconds)
            .Field("failed_response_time_percentile_75_ms", results.Failed.ResponseTimePercentile75.TotalMilliseconds)
            .Field("failed_response_time_percentile_95_ms", results.Failed.ResponseTimePercentile95.TotalMilliseconds)
            .Field("failed_response_time_percentile_99_ms", results.Failed.ResponseTimePercentile99.TotalMilliseconds)
            .Field("failed_total_execution_duration_ms", results.Failed.TotalExecutionDuration.TotalMilliseconds)

            .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        points.Add(scenarioPoint);

        // Step-level metrics
        foreach (var step in results.Steps)
        {
            var stepMetrics = step.Value;
            var stepPoint = PointData
                .Measurement("step_metrics")
                .Tag("test_run_id", testRunId)
                .Tag("feature_name", featureName)
                .Tag("scenario_name", results.ScenarioName)
                .Tag("step_name", step.Key)
                .Field("total_execution_duration_ms", stepMetrics.TotalExecutionDuration.TotalMilliseconds)

                // OK metrics
                .Field("ok_request_count", stepMetrics.Ok.RequestCount)
                .Field("ok_requests_per_second", results.Ok.RequestsPerSecond)
                .Field("ok_response_time_min_ms", stepMetrics.Ok.ResponseTimeMin.TotalMilliseconds)
                .Field("ok_response_time_max_ms", stepMetrics.Ok.ResponseTimeMax.TotalMilliseconds)
                .Field("ok_response_time_mean_ms", stepMetrics.Ok.ResponseTimeMean.TotalMilliseconds)
                .Field("ok_response_time_stddev_ms", stepMetrics.Ok.ResponseTimeStandardDeviation.TotalMilliseconds)
                .Field("ok_response_time_median_ms", stepMetrics.Ok.ResponseTimeMedian.TotalMilliseconds)
                .Field("ok_response_time_percentile_75_ms", stepMetrics.Ok.ResponseTimePercentile75.TotalMilliseconds)
                .Field("ok_response_time_percentile_95_ms", stepMetrics.Ok.ResponseTimePercentile95.TotalMilliseconds)
                .Field("ok_response_time_percentile_99_ms", stepMetrics.Ok.ResponseTimePercentile99.TotalMilliseconds)
                .Field("ok_total_execution_duration_ms", stepMetrics.Ok.TotalExecutionDuration.TotalMilliseconds)

                // Failed metrics
                .Field("failed_request_count", stepMetrics.Failed.RequestCount)
                .Field("failed_requests_per_second", results.Failed.RequestsPerSecond)
                .Field("failed_response_time_min_ms", stepMetrics.Failed.ResponseTimeMin.TotalMilliseconds)
                .Field("failed_response_time_max_ms", stepMetrics.Failed.ResponseTimeMax.TotalMilliseconds)
                .Field("failed_response_time_mean_ms", stepMetrics.Failed.ResponseTimeMean.TotalMilliseconds)
                .Field("failed_response_time_stddev_ms", stepMetrics.Failed.ResponseTimeStandardDeviation.TotalMilliseconds)
                .Field("failed_response_time_median_ms", stepMetrics.Failed.ResponseTimeMedian.TotalMilliseconds)
                .Field("failed_response_time_percentile_75_ms", stepMetrics.Failed.ResponseTimePercentile75.TotalMilliseconds)
                .Field("failed_response_time_percentile_95_ms", stepMetrics.Failed.ResponseTimePercentile95.TotalMilliseconds)
                .Field("failed_response_time_percentile_99_ms", stepMetrics.Failed.ResponseTimePercentile99.TotalMilliseconds)
                .Field("failed_total_execution_duration_ms", stepMetrics.Failed.TotalExecutionDuration.TotalMilliseconds)

                .Timestamp(results.Created, WritePrecision.Ns);

            points.Add(stepPoint);
        }

        var writeApi = _client.GetWriteApiAsync();
        await writeApi.WritePointsAsync(points, _config.Bucket, _config.Org);
    }

    public Task CleanupGlobal()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }
}
