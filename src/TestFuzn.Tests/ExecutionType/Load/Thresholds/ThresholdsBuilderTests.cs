namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Thresholds;

/// <summary>
/// Pins the declarative thresholds API from the builder down to the scenario's configuration:
/// what <c>.Load().Thresholds(...)</c> stores as typed <see cref="Threshold"/>s, in which order,
/// and what it rejects. Hermetic — nothing is run.
/// </summary>
[TestClass]
public class ThresholdsBuilderTests : Test
{
    [Test]
    public async Task Verify_thresholds_become_typed_configuration_on_the_scenario()
    {
        await Scenario()
            .Step("Each builder call becomes one typed threshold on the scenario, in declaration order, with the limit in the metric's unit", context =>
            {
                var scenarioBuilder = Scenario("Declares thresholds")
                    .Step("Succeed", iterationContext => { })
                    .Load().Simulations((scenarioContext, simulations) => simulations.OneTimeLoad(1))
                    .Load().Thresholds(thresholds => thresholds
                        .ResponseTimeMean(TimeSpan.FromMilliseconds(250))
                        .ResponseTimePercentile95(TimeSpan.FromMilliseconds(500))
                        .ResponseTimePercentile99(TimeSpan.FromSeconds(1))
                        .ErrorRate(0.01)
                        .RequestsPerSecond(minimum: 50));

                var declared = scenarioBuilder.Scenario.Thresholds;
                Assert.HasCount(5, declared);
                AssertThreshold(declared[0], ThresholdMetric.ResponseTimeMean, 250, ThresholdComparison.LessThanOrEqualTo);
                AssertThreshold(declared[1], ThresholdMetric.ResponseTimePercentile95, 500, ThresholdComparison.LessThanOrEqualTo);
                AssertThreshold(declared[2], ThresholdMetric.ResponseTimePercentile99, 1000, ThresholdComparison.LessThanOrEqualTo);
                AssertThreshold(declared[3], ThresholdMetric.ErrorRate, 0.01, ThresholdComparison.LessThanOrEqualTo);
                AssertThreshold(declared[4], ThresholdMetric.RequestsPerSecond, 50, ThresholdComparison.GreaterThanOrEqualTo);
            })
            .Step("Thresholds accumulate across calls, and a scenario without a Thresholds call declares none", context =>
            {
                var scenarioBuilder = Scenario("Declares thresholds twice")
                    .Step("Succeed", iterationContext => { })
                    .Load().Simulations((scenarioContext, simulations) => simulations.OneTimeLoad(1))
                    .Load().Thresholds(thresholds => thresholds.ErrorRate(0.05))
                    .Load().Thresholds(thresholds => thresholds.RequestsPerSecond(minimum: 10));

                var declared = scenarioBuilder.Scenario.Thresholds;
                Assert.HasCount(2, declared);
                AssertThreshold(declared[0], ThresholdMetric.ErrorRate, 0.05, ThresholdComparison.LessThanOrEqualTo);
                AssertThreshold(declared[1], ThresholdMetric.RequestsPerSecond, 10, ThresholdComparison.GreaterThanOrEqualTo);

                var plain = Scenario("Declares no thresholds")
                    .Step("Succeed", iterationContext => { })
                    .Load().Simulations((scenarioContext, simulations) => simulations.OneTimeLoad(1));
                Assert.IsEmpty(plain.Scenario.Thresholds);
            })
            .Run();
    }

    [Test]
    public async Task Verify_invalid_declarations_are_rejected()
    {
        await Scenario()
            .Step("A metric can be declared once per scenario", context =>
            {
                var scenarioBuilder = Scenario("Declares a metric twice")
                    .Step("Succeed", iterationContext => { })
                    .Load().Thresholds(thresholds => thresholds.ErrorRate(0.01));

                Assert.ThrowsExactly<InvalidOperationException>(() => scenarioBuilder.Load().Thresholds(thresholds => thresholds.ErrorRate(0.02)));
                Assert.ThrowsExactly<InvalidOperationException>(() => new ThresholdsBuilder(new List<Threshold>()).ResponseTimePercentile95(TimeSpan.FromSeconds(1)).ResponseTimePercentile95(TimeSpan.FromSeconds(2)));
                // The rejected declaration leaves the earlier one in place.
                Assert.AreEqual(0.01, Assert.ContainsSingle(scenarioBuilder.Scenario.Thresholds).Limit);
            })
            .Step("Limits must be non-negative, an error rate a fraction from 0 to 1, and the action non-null", context =>
            {
                var builder = new ThresholdsBuilder(new List<Threshold>());
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.ResponseTimeMean(TimeSpan.FromMilliseconds(-1)));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.ResponseTimePercentile95(TimeSpan.FromTicks(-1)));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.ResponseTimePercentile99(TimeSpan.FromSeconds(-5)));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.ErrorRate(-0.1));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.ErrorRate(1.5));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.ErrorRate(double.NaN));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.RequestsPerSecond(minimum: -1));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.RequestsPerSecond(minimum: double.NaN));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.RequestsPerSecond(minimum: double.PositiveInfinity));
                Assert.ThrowsExactly<ArgumentNullException>(() => new ThresholdsBuilder(null!));

                var scenarioBuilder = Scenario("Declares nothing").Step("Succeed", iterationContext => { });
                Assert.ThrowsExactly<ArgumentNullException>(() => scenarioBuilder.Load().Thresholds(null!));

                // Zero limits are allowed: no failure tolerated, any rate accepted.
                builder.ErrorRate(0).RequestsPerSecond(minimum: 0).ResponseTimeMean(TimeSpan.Zero);
            })
            .Step("A threshold itself rejects a limit outside the metric's range", context =>
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Threshold(ThresholdMetric.ErrorRate, 1.01, ThresholdComparison.LessThanOrEqualTo));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Threshold(ThresholdMetric.RequestsPerSecond, -1, ThresholdComparison.GreaterThanOrEqualTo));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Threshold(ThresholdMetric.ResponseTimeMean, double.PositiveInfinity, ThresholdComparison.LessThanOrEqualTo));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Threshold((ThresholdMetric)99, 1, ThresholdComparison.LessThanOrEqualTo));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Threshold(ThresholdMetric.ErrorRate, 0.5, (ThresholdComparison)99));
            })
            .Run();
    }

    private static void AssertThreshold(Threshold actual, ThresholdMetric expectedMetric, double expectedLimit, ThresholdComparison expectedComparison)
    {
        Assert.AreEqual(expectedMetric, actual.Metric);
        Assert.AreEqual(expectedLimit, actual.Limit);
        Assert.AreEqual(expectedComparison, actual.Comparison);
    }
}
