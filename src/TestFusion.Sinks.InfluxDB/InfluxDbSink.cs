using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using TestFusion.Contracts.Sinks;
using TestFusion.Results.Load;

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

    public async Task WriteMetrics(string testRunId, string scenarioName, ScenarioLoadResult metrics)
    {
        var points = new List<PointData>();

        // Scenario-level metrics
        var scenarioPoint = PointData
            .Measurement("scenario_metrics")
            .Tag("test_run_id", testRunId)
            .Tag("feature_name", metrics.FeatureName)
            .Tag("scenario_name", metrics.ScenarioName)
            .Field("request_count", metrics.RequestCount)
            .Field("total_execution_duration_ms", metrics.TotalExecutionDuration.TotalMilliseconds)
            .Field("requests_per_second", metrics.RequestsPerSecond)

            // OK metrics
            .Field("ok_request_count", metrics.Ok.RequestCount)
            .Field("ok_requests_per_second", metrics.Ok.RequestsPerSecond)
            .Field("ok_response_time_min_ms", metrics.Ok.ResponseTimeMin.TotalMilliseconds)
            .Field("ok_response_time_max_ms", metrics.Ok.ResponseTimeMax.TotalMilliseconds)
            .Field("ok_response_time_mean_ms", metrics.Ok.ResponseTimeMean.TotalMilliseconds)
            .Field("ok_response_time_stddev_ms", metrics.Ok.ResponseTimeStandardDeviation.TotalMilliseconds)
            .Field("ok_response_time_median_ms", metrics.Ok.ResponseTimeMedian.TotalMilliseconds)
            .Field("ok_response_time_percentile_75_ms", metrics.Ok.ResponseTimePercentile75.TotalMilliseconds)
            .Field("ok_response_time_percentile_95_ms", metrics.Ok.ResponseTimePercentile95.TotalMilliseconds)
            .Field("ok_response_time_percentile_99_ms", metrics.Ok.ResponseTimePercentile99.TotalMilliseconds)
            .Field("ok_total_execution_duration_ms", metrics.Ok.TotalExecutionDuration.TotalMilliseconds)

            // Failed metrics
            .Field("failed_request_count", metrics.Failed.RequestCount)
            .Field("failed_requests_per_second", metrics.Failed.RequestsPerSecond)
            .Field("failed_response_time_min_ms", metrics.Failed.ResponseTimeMin.TotalMilliseconds)
            .Field("failed_response_time_max_ms", metrics.Failed.ResponseTimeMax.TotalMilliseconds)
            .Field("failed_response_time_mean_ms", metrics.Failed.ResponseTimeMean.TotalMilliseconds)
            .Field("failed_response_time_stddev_ms", metrics.Failed.ResponseTimeStandardDeviation.TotalMilliseconds)
            .Field("failed_response_time_median_ms", metrics.Failed.ResponseTimeMedian.TotalMilliseconds)
            .Field("failed_response_time_percentile_75_ms", metrics.Failed.ResponseTimePercentile75.TotalMilliseconds)
            .Field("failed_response_time_percentile_95_ms", metrics.Failed.ResponseTimePercentile95.TotalMilliseconds)
            .Field("failed_response_time_percentile_99_ms", metrics.Failed.ResponseTimePercentile99.TotalMilliseconds)
            .Field("failed_total_execution_duration_ms", metrics.Failed.TotalExecutionDuration.TotalMilliseconds)

            .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        points.Add(scenarioPoint);

        // Step-level metrics
        foreach (var step in metrics.Steps)
        {
            var stepMetrics = step.Value;
            var stepPoint = PointData
                .Measurement("step_metrics")
                .Tag("test_run_id", testRunId)
                .Tag("feature_name", metrics.FeatureName)
                .Tag("scenario_name", scenarioName)
                .Tag("step_name", step.Key)
                .Field("total_execution_duration_ms", stepMetrics.TotalExecutionDuration.TotalMilliseconds)

                // OK metrics
                .Field("ok_request_count", stepMetrics.Ok.RequestCount)
                .Field("ok_requests_per_second", metrics.Ok.RequestsPerSecond)
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
                .Field("failed_requests_per_second", metrics.Failed.RequestsPerSecond)
                .Field("failed_response_time_min_ms", stepMetrics.Failed.ResponseTimeMin.TotalMilliseconds)
                .Field("failed_response_time_max_ms", stepMetrics.Failed.ResponseTimeMax.TotalMilliseconds)
                .Field("failed_response_time_mean_ms", stepMetrics.Failed.ResponseTimeMean.TotalMilliseconds)
                .Field("failed_response_time_stddev_ms", stepMetrics.Failed.ResponseTimeStandardDeviation.TotalMilliseconds)
                .Field("failed_response_time_median_ms", stepMetrics.Failed.ResponseTimeMedian.TotalMilliseconds)
                .Field("failed_response_time_percentile_75_ms", stepMetrics.Failed.ResponseTimePercentile75.TotalMilliseconds)
                .Field("failed_response_time_percentile_95_ms", stepMetrics.Failed.ResponseTimePercentile95.TotalMilliseconds)
                .Field("failed_response_time_percentile_99_ms", stepMetrics.Failed.ResponseTimePercentile99.TotalMilliseconds)
                .Field("failed_total_execution_duration_ms", stepMetrics.Failed.TotalExecutionDuration.TotalMilliseconds)

                .Timestamp(metrics.Created, WritePrecision.Ns);

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
