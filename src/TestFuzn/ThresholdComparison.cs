namespace Fuzn.TestFuzn;

/// <summary>
/// The relation a <see cref="Threshold"/> requires between its metric's value and its
/// <see cref="Threshold.Limit"/> — the relation that must hold for the threshold to pass.
/// </summary>
public enum ThresholdComparison
{
    /// <summary>
    /// The value must stay at or below the limit — a maximum, as the response-time and
    /// error-rate thresholds are. Violated when the value exceeds the limit.
    /// </summary>
    LessThanOrEqualTo,

    /// <summary>
    /// The value must reach at least the limit — a minimum, as the request-rate threshold is.
    /// Violated when the value falls below the limit.
    /// </summary>
    GreaterThanOrEqualTo
}
