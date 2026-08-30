using System.Globalization;

namespace Fuzn.TestFuzn.Internals.Thresholds;

/// <summary>
/// How thresholds and their values are described — shared by <see cref="Threshold.ToString"/>,
/// <see cref="ThresholdResult.ToString"/> (and through it the
/// <see cref="ThresholdViolationException"/> message) and the dashboard: the metric's short
/// label (mean, p95, p99, error rate, rps), the comparison symbols — "≤" and "≥" wherever the
/// output can carry them, "&lt;=" and "&gt;=" where it stays ASCII — and a value in the metric's
/// unit printed in the invariant culture — a response time in milliseconds with at most one
/// decimal ("812.4 ms"), the error rate as a percentage with three significant digits ("50 %",
/// "2.4 %", "0.0333 %" — so one failure in thousands never reads as "0 %"), the request rate
/// bare with at most one decimal ("42").
/// </summary>
internal static class ThresholdFormat
{
    /// <summary>The metric's short label: mean, p95, p99, error rate or rps.</summary>
    public static string Label(ThresholdMetric metric)
    {
        switch (metric)
        {
            case ThresholdMetric.ResponseTimeMean:
                return "mean";
            case ThresholdMetric.ResponseTimePercentile95:
                return "p95";
            case ThresholdMetric.ResponseTimePercentile99:
                return "p99";
            case ThresholdMetric.ErrorRate:
                return "error rate";
            case ThresholdMetric.RequestsPerSecond:
                return "rps";
            default:
                throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown threshold metric.");
        }
    }

    /// <summary>
    /// A value in the metric's unit, in the invariant culture: "812.4 ms" for a response time
    /// (at most one decimal), "2.4 %" / "0.0333 %" for an error rate (the 0..1 fraction as a
    /// percentage with three significant digits), "42" for a request rate (at most one decimal).
    /// </summary>
    public static string FormatValue(ThresholdMetric metric, double value)
    {
        switch (metric)
        {
            case ThresholdMetric.ResponseTimeMean:
            case ThresholdMetric.ResponseTimePercentile95:
            case ThresholdMetric.ResponseTimePercentile99:
                return value.ToString("0.#", CultureInfo.InvariantCulture) + " ms";
            case ThresholdMetric.ErrorRate:
                return (value * 100).ToString("G3", CultureInfo.InvariantCulture) + " %";
            case ThresholdMetric.RequestsPerSecond:
                return value.ToString("0.#", CultureInfo.InvariantCulture);
            default:
                throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown threshold metric.");
        }
    }

    /// <summary>The symbol of the relation a threshold requires: "≤" for a maximum, "≥" for a minimum.</summary>
    public static string RequiredComparisonSymbol(ThresholdComparison comparison)
    {
        switch (comparison)
        {
            case ThresholdComparison.LessThanOrEqualTo:
                return "≤";
            case ThresholdComparison.GreaterThanOrEqualTo:
                return "≥";
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown threshold comparison.");
        }
    }

    /// <summary>
    /// <see cref="RequiredComparisonSymbol"/> in plain ASCII — "&lt;=" for a maximum, "&gt;=" for a
    /// minimum — for the outputs that stay ASCII rather than emitting "≤" and "≥": the MSTest
    /// adapter's summary, which writes through <see cref="Fuzn.TestFuzn.Contracts.Adapters.ITestFrameworkAdapter"/>
    /// to a test host's plain text log.
    /// </summary>
    public static string RequiredComparisonSymbolAscii(ThresholdComparison comparison)
    {
        switch (comparison)
        {
            case ThresholdComparison.LessThanOrEqualTo:
                return "<=";
            case ThresholdComparison.GreaterThanOrEqualTo:
                return ">=";
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown threshold comparison.");
        }
    }

    /// <summary>The symbol of the relation a violated threshold's value stood in to its limit: "&gt;" past a maximum, "&lt;" under a minimum.</summary>
    public static string ViolatedComparisonSymbol(ThresholdComparison comparison)
    {
        switch (comparison)
        {
            case ThresholdComparison.LessThanOrEqualTo:
                return ">";
            case ThresholdComparison.GreaterThanOrEqualTo:
                return "<";
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown threshold comparison.");
        }
    }
}
