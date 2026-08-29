using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// The mini gauge under a <see cref="StatTile"/>'s value — how a threshold shows on a tile:
/// <see cref="Current"/>, the current reading, against <see cref="Limit"/>, with
/// <see cref="LimitText"/> written after the bar as the caller formatted it ("500 ms") and
/// the warning band marked on the track. <see cref="Comparison"/> says which way the limit
/// binds, so a layout maps a declared threshold's comparison straight through. For a maximum
/// (<see cref="ThresholdComparison.LessThanOrEqualTo"/>, the default) the bar spans
/// 0 → Limit, the band is its top <c>1 − WarningFraction</c> — the evaluator's warning zone
/// [WarningFraction × Limit, Limit] — and a reading past the limit is overshoot. For a
/// minimum (<see cref="ThresholdComparison.GreaterThanOrEqualTo"/>) the bar spans
/// 0 → Limit / WarningFraction, so the same physical band is the evaluator's warning zone
/// [Limit, Limit / WarningFraction): a fill that ends below the band is a breach, one that
/// ends inside it a warning, and a full bar is ok — exceeding a minimum is fine, so a minimum
/// never shows overshoot. The fraction is <see cref="ThresholdEvaluator.WarningFraction"/>
/// unless set, so the band on the tile is the band the evaluator judges by; a fraction at 1
/// or above, at 0 or below, or NaN cannot size a band, so a minimum's bar then spans
/// 0 → Limit like a maximum's, and the band is drawn as for a maximum — none at 1 or above
/// or NaN, the whole track at 0 or below. A limit that is not finite or not positive gives
/// the bar no scale either way: it renders empty. <see cref="StatTileWidget"/> documents the
/// drawing.
/// </summary>
internal readonly struct StatTileGauge
{
    /// <summary>The current reading, in the limit's unit.</summary>
    public double Current { get; }

    /// <summary>The limit the reading is judged against: the bar's end for a maximum, the start of its warning band for a minimum.</summary>
    public double Limit { get; }

    /// <summary>The formatted limit written after the bar, e.g. "500 ms"; empty writes nothing.</summary>
    public string LimitText { get; }

    /// <summary>Which way the limit binds: a maximum (<see cref="ThresholdComparison.LessThanOrEqualTo"/>) unless set.</summary>
    public ThresholdComparison Comparison { get; init; }

    /// <summary>
    /// Where the warning band starts, as a fraction of the limit: <see cref="ThresholdEvaluator.WarningFraction"/>
    /// unless set. At 1 or above (or NaN) there is no band; at 0 or below the whole track is the band.
    /// </summary>
    public double WarningFraction { get; init; }

    public StatTileGauge(double current, double limit, string limitText)
    {
        if (limitText == null)
            throw new ArgumentNullException(nameof(limitText), "Limit text cannot be null.");

        Current = current;
        Limit = limit;
        LimitText = limitText;
        Comparison = ThresholdComparison.LessThanOrEqualTo;
        WarningFraction = ThresholdEvaluator.WarningFraction;
    }
}
