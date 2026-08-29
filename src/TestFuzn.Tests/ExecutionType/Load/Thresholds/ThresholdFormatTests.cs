using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Thresholds;

/// <summary>
/// Pins how thresholds and their verdicts are described: <see cref="ThresholdFormat"/>'s labels,
/// values and comparison symbols, the <see cref="Threshold.ToString"/> and
/// <see cref="ThresholdResult.ToString"/> clauses, and the <see cref="ThresholdViolationException"/>
/// message built from them. Hermetic — nothing is run.
/// </summary>
[TestClass]
public class ThresholdFormatTests : Test
{
    private static Threshold Maximum(ThresholdMetric metric, double limit)
    {
        return new Threshold(metric, limit, ThresholdComparison.LessThanOrEqualTo);
    }

    private static Threshold Minimum(ThresholdMetric metric, double limit)
    {
        return new Threshold(metric, limit, ThresholdComparison.GreaterThanOrEqualTo);
    }

    [Test]
    public async Task Verify_values_labels_and_symbols()
    {
        await Scenario()
            .Step("A value prints in the metric's unit: milliseconds and rps with at most one decimal, the error rate as a percentage with three significant digits", context =>
            {
                Assert.AreEqual("812.4 ms", ThresholdFormat.FormatValue(ThresholdMetric.ResponseTimePercentile95, 812.44));
                Assert.AreEqual("500 ms", ThresholdFormat.FormatValue(ThresholdMetric.ResponseTimePercentile99, 500));
                Assert.AreEqual("0.5 ms", ThresholdFormat.FormatValue(ThresholdMetric.ResponseTimeMean, 0.5));
                Assert.AreEqual("50 %", ThresholdFormat.FormatValue(ThresholdMetric.ErrorRate, 0.5));
                Assert.AreEqual("2.4 %", ThresholdFormat.FormatValue(ThresholdMetric.ErrorRate, 0.024));
                Assert.AreEqual("1.23 %", ThresholdFormat.FormatValue(ThresholdMetric.ErrorRate, 0.0123));
                // One failure in three thousand is not "0 %".
                Assert.AreEqual("0.0333 %", ThresholdFormat.FormatValue(ThresholdMetric.ErrorRate, 1.0 / 3000));
                Assert.AreEqual("0 %", ThresholdFormat.FormatValue(ThresholdMetric.ErrorRate, 0));
                Assert.AreEqual("100 %", ThresholdFormat.FormatValue(ThresholdMetric.ErrorRate, 1));
                Assert.AreEqual("42.4", ThresholdFormat.FormatValue(ThresholdMetric.RequestsPerSecond, 42.35));
                Assert.AreEqual("50", ThresholdFormat.FormatValue(ThresholdMetric.RequestsPerSecond, 50));
            })
            .Step("Every metric has its short label, and each comparison its required and its violated symbol", context =>
            {
                Assert.AreEqual("mean", ThresholdFormat.Label(ThresholdMetric.ResponseTimeMean));
                Assert.AreEqual("p95", ThresholdFormat.Label(ThresholdMetric.ResponseTimePercentile95));
                Assert.AreEqual("p99", ThresholdFormat.Label(ThresholdMetric.ResponseTimePercentile99));
                Assert.AreEqual("error rate", ThresholdFormat.Label(ThresholdMetric.ErrorRate));
                Assert.AreEqual("rps", ThresholdFormat.Label(ThresholdMetric.RequestsPerSecond));

                Assert.AreEqual("≤", ThresholdFormat.RequiredComparisonSymbol(ThresholdComparison.LessThanOrEqualTo));
                Assert.AreEqual("≥", ThresholdFormat.RequiredComparisonSymbol(ThresholdComparison.GreaterThanOrEqualTo));
                Assert.AreEqual(">", ThresholdFormat.ViolatedComparisonSymbol(ThresholdComparison.LessThanOrEqualTo));
                Assert.AreEqual("<", ThresholdFormat.ViolatedComparisonSymbol(ThresholdComparison.GreaterThanOrEqualTo));

                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ThresholdFormat.Label((ThresholdMetric)99));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ThresholdFormat.FormatValue((ThresholdMetric)99, 1));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ThresholdFormat.RequiredComparisonSymbol((ThresholdComparison)99));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ThresholdFormat.ViolatedComparisonSymbol((ThresholdComparison)99));
            })
            .Run();
    }

    [Test]
    public async Task Verify_threshold_and_result_clauses_and_the_violation_message()
    {
        await Scenario()
            .Step("A threshold reads as declared: label, required relation, limit", context =>
            {
                Assert.AreEqual("p95 ≤ 500 ms", Maximum(ThresholdMetric.ResponseTimePercentile95, 500).ToString());
                Assert.AreEqual("mean ≤ 250.5 ms", Maximum(ThresholdMetric.ResponseTimeMean, 250.5).ToString());
                Assert.AreEqual("error rate ≤ 1 %", Maximum(ThresholdMetric.ErrorRate, 0.01).ToString());
                Assert.AreEqual("rps ≥ 50", Minimum(ThresholdMetric.RequestsPerSecond, 50).ToString());
            })
            .Step("A result reads as one clause: the required relation when it held, the failed one when it was violated", context =>
            {
                var p95 = Maximum(ThresholdMetric.ResponseTimePercentile95, 500);
                Assert.AreEqual("p95 812 ms > 500 ms", new ThresholdResult(p95, 812, false).ToString());
                Assert.AreEqual("p95 320 ms ≤ 500 ms", new ThresholdResult(p95, 320, true).ToString());

                var rps = Minimum(ThresholdMetric.RequestsPerSecond, 50);
                Assert.AreEqual("rps 42 < 50", new ThresholdResult(rps, 42, false).ToString());
                Assert.AreEqual("rps 64 ≥ 50", new ThresholdResult(rps, 64, true).ToString());

                var noErrors = Maximum(ThresholdMetric.ErrorRate, 0);
                Assert.AreEqual("error rate 0.0333 % > 0 %", new ThresholdResult(noErrors, 1.0 / 3000, false).ToString());
                Assert.AreEqual("error rate 0 % ≤ 0 %", new ThresholdResult(noErrors, 0, true).ToString());
            })
            .Step("The violation message is the prefix and the violated results' clauses joined by a semicolon", context =>
            {
                var violations = new List<ThresholdResult>
                {
                    new ThresholdResult(Maximum(ThresholdMetric.ResponseTimePercentile95, 500), 812, false),
                    new ThresholdResult(Maximum(ThresholdMetric.ErrorRate, 0.01), 0.024, false)
                };

                var exception = new ThresholdViolationException(violations);
                Assert.AreEqual("Threshold violated: p95 812 ms > 500 ms; error rate 2.4 % > 1 %", exception.Message);
                Assert.StartsWith(ThresholdViolationException.MessagePrefix, exception.Message);
                Assert.AreSame(violations, exception.Violations);

                Assert.AreEqual("Threshold violated: rps 42 < 50", new ThresholdViolationException(new[] { new ThresholdResult(Minimum(ThresholdMetric.RequestsPerSecond, 50), 42, false) }).Message);
                Assert.ThrowsExactly<ArgumentNullException>(() => new ThresholdViolationException(null!));
                Assert.ThrowsExactly<ArgumentException>(() => new ThresholdViolationException(Array.Empty<ThresholdResult>()));
            })
            .Run();
    }
}
