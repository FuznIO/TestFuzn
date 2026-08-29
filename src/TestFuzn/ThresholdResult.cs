using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn;

/// <summary>
/// The verdict of one <see cref="Threshold"/> when its load test completed: the
/// <see cref="Current"/> value of the metric over the scenario's whole measurement phase (warmup
/// excluded, as the cumulative statistics are), in the metric's unit — the same unit as
/// <see cref="Threshold.Limit"/> — and whether the threshold's comparison held. Immutable.
/// </summary>
public sealed class ThresholdResult
{
    /// <summary>Gets the threshold this is the verdict of.</summary>
    public Threshold Threshold { get; }

    /// <summary>
    /// Gets the metric's cumulative value at completion, in the metric's unit: milliseconds
    /// for the response-time metrics, a 0..1 fraction for the error rate and requests per
    /// second for the request rate.
    /// </summary>
    public double Current { get; }

    /// <summary>Gets whether the threshold held — <see cref="Current"/> satisfied the threshold's comparison against its limit.</summary>
    public bool Passed { get; }

    internal ThresholdResult(Threshold threshold, double current, bool passed)
    {
        if (threshold == null)
            throw new ArgumentNullException(nameof(threshold), "Threshold cannot be null.");

        Threshold = threshold;
        Current = current;
        Passed = passed;
    }

    /// <summary>
    /// The verdict as one clause: the metric's label, the measured value, the relation it stood
    /// in to the limit and the limit, in the metric's unit — the required relation when the
    /// threshold held ("p95 320 ms ≤ 500 ms", "rps 64 ≥ 50"), the failed one when it was
    /// violated ("p95 812 ms &gt; 500 ms", "rps 42 &lt; 50"). A <see cref="ThresholdViolationException"/>
    /// message is the violated results' clauses joined by "; ".
    /// </summary>
    /// <returns>The verdict as one clause.</returns>
    public override string ToString()
    {
        var comparison = ThresholdFormat.ViolatedComparisonSymbol(Threshold.Comparison);
        if (Passed)
            comparison = ThresholdFormat.RequiredComparisonSymbol(Threshold.Comparison);

        return ThresholdFormat.Label(Threshold.Metric) + " " + ThresholdFormat.FormatValue(Threshold.Metric, Current) + " " + comparison + " " + ThresholdFormat.FormatValue(Threshold.Metric, Threshold.Limit);
    }
}
