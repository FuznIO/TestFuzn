namespace Fuzn.TestFuzn.Internals.Thresholds;

/// <summary>
/// The live reading of a <see cref="Threshold"/> at one dashboard sample, from the newest closed
/// interval's value against the limit — the rule is on <see cref="ThresholdEvaluator"/>.
/// </summary>
internal enum ThresholdState
{
    /// <summary>The value is comfortably inside the limit.</summary>
    Ok,

    /// <summary>The value is inside the limit but within the warning band next to it: at 80 % of a maximum or more, at 125 % of a minimum or less.</summary>
    Warning,

    /// <summary>The value is past the limit: above a maximum, below a minimum.</summary>
    Breached
}
