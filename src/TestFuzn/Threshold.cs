using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn;

/// <summary>
/// One declarative pass/fail criterion on a load test scenario's statistics, declared through
/// <see cref="LoadBuilder{TModel}.Thresholds"/>: a <see cref="Metric"/>, the <see cref="Limit"/>
/// it is held to and the <see cref="Comparison"/> that must hold between them. The limit is in
/// the metric's unit (see <see cref="ThresholdMetric"/>): milliseconds for the response-time
/// metrics, a 0..1 fraction for the error rate and requests per second for the request rate.
/// Thresholds are scenario-level and are evaluated once, on the scenario's cumulative
/// measurement statistics, when the load test completes (see <see cref="ThresholdResult"/>);
/// the standalone runner's live dashboard also tracks them while the test runs. Immutable.
/// </summary>
public sealed class Threshold
{
    /// <summary>Gets the statistic the threshold is declared on.</summary>
    public ThresholdMetric Metric { get; }

    /// <summary>
    /// Gets the limit the statistic is held to, in the metric's unit: milliseconds for the
    /// response-time metrics, a 0..1 fraction for <see cref="ThresholdMetric.ErrorRate"/> and
    /// requests per second for <see cref="ThresholdMetric.RequestsPerSecond"/>. Never negative.
    /// </summary>
    public double Limit { get; }

    /// <summary>Gets the relation between the statistic and <see cref="Limit"/> that must hold for the threshold to pass.</summary>
    public ThresholdComparison Comparison { get; }

    internal Threshold(ThresholdMetric metric, double limit, ThresholdComparison comparison)
    {
        if (!Enum.IsDefined(metric))
            throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown threshold metric.");
        if (!Enum.IsDefined(comparison))
            throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown threshold comparison.");
        if (double.IsNaN(limit) || double.IsInfinity(limit))
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be a finite number.");
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit cannot be negative.");
        if (metric == ThresholdMetric.ErrorRate && limit > 1)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "An error rate limit is a fraction from 0 to 1.");

        Metric = metric;
        Limit = limit;
        Comparison = comparison;
    }

    /// <summary>
    /// The threshold as declared: the metric's label, the relation that must hold and the limit
    /// in the metric's unit — "p95 ≤ 500 ms", "error rate ≤ 1 %", "rps ≥ 50".
    /// </summary>
    /// <returns>The threshold as one clause.</returns>
    public override string ToString()
    {
        return ThresholdFormat.Label(Metric) + " " + ThresholdFormat.RequiredComparisonSymbol(Comparison) + " " + ThresholdFormat.FormatValue(Metric, Limit);
    }
}
