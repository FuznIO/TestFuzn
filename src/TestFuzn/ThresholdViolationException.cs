namespace Fuzn.TestFuzn;

/// <summary>
/// The failure of a load test whose scenario violated one or more of its declared thresholds
/// (see <see cref="LoadBuilder{TModel}.Thresholds"/>) when it completed. The message starts
/// with "Threshold violated: " and lists every violation — one <see cref="ThresholdResult.ToString"/>
/// clause per violated threshold in declaration order, joined by "; ", each the metric's label,
/// its measured value, the comparison that failed and the limit, in the metric's unit — e.g.
/// <c>Threshold violated: p95 812 ms &gt; 500 ms; error rate 2.4 % &gt; 1 %</c> — and
/// <see cref="Violations"/> carries the failed <see cref="ThresholdResult"/>s behind it. Raised
/// where an <see cref="LoadBuilder{TModel}.AssertWhenDone"/> failure is: the scenario is marked
/// failed with this exception as its assert-when-done failure, the run completes its cleanup
/// and summary, and the test method then observes the exception.
/// </summary>
public sealed class ThresholdViolationException : Exception
{
    /// <summary>What every message starts with, ahead of the violation clauses.</summary>
    internal const string MessagePrefix = "Threshold violated: ";

    /// <summary>Gets the results of the violated thresholds, in declaration order; never empty.</summary>
    public IReadOnlyList<ThresholdResult> Violations { get; }

    internal ThresholdViolationException(IReadOnlyList<ThresholdResult> violations)
        : base(BuildMessage(violations))
    {
        Violations = violations;
    }

    private static string BuildMessage(IReadOnlyList<ThresholdResult> violations)
    {
        if (violations == null)
            throw new ArgumentNullException(nameof(violations), "Violations cannot be null.");
        if (violations.Count == 0)
            throw new ArgumentException("At least one violation is required.", nameof(violations));

        var clauses = new string[violations.Count];
        for (var index = 0; index < clauses.Length; index++)
            clauses[index] = violations[index].ToString();

        return MessagePrefix + string.Join("; ", clauses);
    }
}
