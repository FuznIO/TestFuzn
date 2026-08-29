namespace Fuzn.TestFuzn.Internals.Thresholds;

/// <summary>
/// One declared <see cref="Fuzn.TestFuzn.Threshold"/> with its live reading at one sample,
/// published on <see cref="Fuzn.TestFuzn.Internals.Terminal.LiveMetricsSnapshot.Thresholds"/>:
/// <see cref="Current"/> is the metric's value over the newest closed interval of the
/// measurement phase — the same number the dashboard's interval tiles show — in the metric's
/// unit (milliseconds, a 0..1 fraction or requests per second), <see cref="State"/> the
/// Ok/Warning/Breached reading of it against the limit, and <see cref="BreachedFor"/> how long
/// the current uninterrupted breach has lasted, measured on the sample timestamps. Before the
/// measurement phase (and before its first interval closes) the reading is the placeholder —
/// Ok, a Current of 0, no breach — and after it the last measurement-phase reading stands
/// unchanged; <see cref="ThresholdEvaluator"/> documents all the rules. Immutable; a fresh
/// instance per judged sample, so a published snapshot never changes.
/// </summary>
internal sealed class LiveThreshold
{
    /// <summary>The declared threshold.</summary>
    public Threshold Threshold { get; }

    /// <summary>The metric's value over the newest closed measurement interval, in the metric's unit; zero in the placeholder state.</summary>
    public double Current { get; }

    /// <summary>The reading of <see cref="Current"/> against the threshold's limit.</summary>
    public ThresholdState State { get; }

    /// <summary>
    /// How long the current uninterrupted breach has lasted: zero unless <see cref="State"/> is
    /// <see cref="ThresholdState.Breached"/>, zero on the sample a breach begins, and the time
    /// since that sample afterwards.
    /// </summary>
    public TimeSpan BreachedFor { get; }

    public LiveThreshold(Threshold threshold, double current, ThresholdState state, TimeSpan breachedFor)
    {
        if (threshold == null)
            throw new ArgumentNullException(nameof(threshold), "Threshold cannot be null.");
        if (breachedFor < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(breachedFor), breachedFor, "Breached-for duration cannot be negative.");
        if (state != ThresholdState.Breached && breachedFor != TimeSpan.Zero)
            throw new ArgumentException("Only a breached threshold has a breached-for duration.", nameof(breachedFor));

        Threshold = threshold;
        Current = current;
        State = state;
        BreachedFor = breachedFor;
    }
}
