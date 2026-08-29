namespace Fuzn.TestFuzn;

/// <summary>
/// The scenario-level load statistic a <see cref="Threshold"/> is declared on. The response-time
/// metrics are measured over successful requests, as the <see cref="AssertStats"/> on
/// <see cref="AssertScenarioStats.Ok"/> are; the error rate over all requests. The unit a
/// threshold's <see cref="Threshold.Limit"/> and a result's <see cref="ThresholdResult.Current"/>
/// are expressed in follows the metric: milliseconds for the response-time metrics, a 0..1
/// fraction for <see cref="ErrorRate"/> and requests per second for <see cref="RequestsPerSecond"/>.
/// </summary>
public enum ThresholdMetric
{
    /// <summary>The mean response time of successful requests, in milliseconds.</summary>
    ResponseTimeMean,

    /// <summary>The 95th-percentile response time of successful requests, in milliseconds.</summary>
    ResponseTimePercentile95,

    /// <summary>The 99th-percentile response time of successful requests, in milliseconds.</summary>
    ResponseTimePercentile99,

    /// <summary>The share of failed requests among all requests, as a fraction from 0 to 1.</summary>
    ErrorRate,

    /// <summary>The request rate, in requests per second.</summary>
    RequestsPerSecond
}
