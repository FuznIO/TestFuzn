using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Tests.Cluster;

[TestClass]
[Tags("unit")]
public class StatsMergeTests : Test
{
    [Test]
    public async Task Verify_merged_stats_match_one_collector_that_saw_every_sample()
    {
        await Scenario()
            .Step("Counts, min, max and percentiles of two workers equal those of one machine", context =>
            {
                var single = new StatsCollector();
                var workerA = new StatsCollector();
                var workerB = new StatsCollector();
                var start = DateTime.UtcNow;
                for (var index = 1; index <= 1000; index++)
                {
                    var duration = TimeSpan.FromMilliseconds(index % 97 + 1);
                    single.Record(duration, start, start);
                    (index % 2 == 0 ? workerA : workerB).Record(duration, start, start);
                }

                var merged = new StatsCollector();
                merged.Add(workerA.GetState());
                merged.Add(workerB.GetState());

                var expected = single.GetCurrentResult();
                var actual = merged.GetCurrentResult();
                Assert.AreEqual(expected.Count, actual.Count);
                Assert.AreEqual(expected.DurationMin, actual.DurationMin);
                Assert.AreEqual(expected.DurationMax, actual.DurationMax);
                Assert.AreEqual(expected.DurationMedian, actual.DurationMedian);
                Assert.AreEqual(expected.DurationPercentile90, actual.DurationPercentile90);
                Assert.AreEqual(expected.DurationPercentile95, actual.DurationPercentile95);
                Assert.AreEqual(expected.DurationPercentile99, actual.DurationPercentile99);
                Assert.AreEqual(expected.TotalExecutionDuration, actual.TotalExecutionDuration);
            })
            .Step("The rate of merged stats uses the measurement duration", context =>
            {
                var merged = new StatsCollector();
                merged.Add(State(300, 10_000));
                merged.SetPerSecond(TimeSpan.FromSeconds(10));
                Assert.AreEqual(30, merged.GetCurrentResult().PerSecond);
            });
    }

    [Test]
    public async Task Verify_scenario_states_merge_steps_and_errors()
    {
        await Scenario()
            .Step("Two workers' scenario states merge into one result with summed steps and errors", context =>
            {
                var workerA = RecordedCollector(okCount: 3, failedCount: 1);
                var workerB = RecordedCollector(okCount: 2, failedCount: 2);

                var controller = new ScenarioLoadCollector(ScenarioWithStep());
                controller.LoadMerged(new[] { workerA.GetState(), workerB.GetState() }, TimeSpan.FromSeconds(1));

                var result = controller.GetCurrentResult(true);
                Assert.AreEqual(8, result.Count);
                Assert.AreEqual(5, result.Ok.Count);
                Assert.AreEqual(3, result.Failed.Count);

                var step = result.Steps["Call"];
                Assert.AreEqual(5, step.Ok.Count);
                Assert.AreEqual(3, step.Failed.Count);
                Assert.AreEqual(3, step.Errors!["Boom"].Count);
            })
            .Step("The phase and simulations travel with the state", context =>
            {
                var state = RecordedCollector(okCount: 1, failedCount: 0).GetState();
                Assert.AreEqual(ScenarioPhase.Measurement, state.Phase);
                Assert.HasCount(1, state.Simulations);
                Assert.AreEqual("One Time Load - Count: 5 ", state.Simulations[0].Description);
            });
    }

    [Test]
    public async Task Verify_histogram_precision_and_range()
    {
        await Scenario()
            .Step("Values below 2048 ticks are exact", context =>
            {
                var histogram = new Histogram();
                for (var value = 0; value < 2048; value++)
                    histogram.Record(value);

                Assert.AreEqual(1023, histogram.ValueAtPercentile(50));
                Assert.AreEqual(2047, histogram.MaxValue());
            })
            .Step("Percentiles are within 0.1 % of the exact values, from 1 tick to 8 hours", context =>
            {
                var random = new Random(1);
                var values = new List<long>();
                var histogram = new Histogram();
                for (var index = 0; index < 100_000; index++)
                {
                    var value = (long) Math.Pow(TimeSpan.FromHours(8).Ticks, random.NextDouble());
                    values.Add(value);
                    histogram.Record(value);
                }

                values.Sort();
                foreach (var percentile in new[] { 50.0, 75, 95, 99, 99.9 })
                {
                    var exact = values[(int) (percentile / 100 * values.Count + 0.5) - 1];
                    var actual = histogram.ValueAtPercentile(percentile);
                    Assert.IsGreaterThanOrEqualTo(exact, actual);
                    Assert.IsLessThanOrEqualTo((long) (exact * 1.001) + 1, actual);
                }
            })
            .Step("A step of 10 minutes is recorded, and the percentiles stay within min and max", context =>
            {
                var collector = new StatsCollector();
                var start = DateTime.UtcNow;
                collector.Record(TimeSpan.FromMinutes(10), start, start);

                var stats = collector.GetCurrentResult();
                Assert.AreEqual(TimeSpan.FromMinutes(10), stats.DurationPercentile99);
                Assert.AreEqual(TimeSpan.FromMinutes(10), stats.DurationMedian);
            })
            .Step("A million samples of 40 to 60 ms encode small", context =>
            {
                var random = new Random(2);
                var histogram = new Histogram();
                for (var index = 0; index < 1_000_000; index++)
                    histogram.Record(random.NextInt64(TimeSpan.FromMilliseconds(40).Ticks, TimeSpan.FromMilliseconds(60).Ticks));

                Assert.IsLessThan(4096, histogram.Encode().Length);
            });
    }

    [Test]
    public async Task Verify_histograms_round_trip_and_subtract()
    {
        await Scenario()
            .Step("An encoded histogram decodes to the same values", context =>
            {
                var histogram = new Histogram();
                for (var value = 1; value <= 5000; value++)
                    histogram.Record(value * 37L);

                var decoded = Histogram.Decode(histogram.Encode());
                Assert.AreEqual(histogram.TotalCount, decoded.TotalCount);
                Assert.AreEqual(histogram.ValueAtPercentile(95), decoded.ValueAtPercentile(95));
                Assert.AreEqual(histogram.MaxValue(), decoded.MaxValue());
                Assert.AreEqual(histogram.StandardDeviation(), decoded.StandardDeviation());
            })
            .Step("Subtracting the previous cumulative histogram leaves the interval", context =>
            {
                var previous = new Histogram();
                previous.Record(10_000, 100);

                var current = new Histogram();
                current.Record(10_000, 100);
                current.Record(5_000_000, 10);

                var interval = Histogram.Subtract(current, previous);
                Assert.AreEqual(10, interval.TotalCount);
                Assert.IsGreaterThan(4_000_000, interval.ValueAtPercentile(50));
            })
            .Step("Interval points show the spike a cumulative p95 hides", context =>
            {
                var previous = new ScenarioStatsState { Name = "S", Ok = State(100, 10_000) };
                var current = new ScenarioStatsState { Name = "S", Ok = State(100, 10_000, spikes: 10) };

                var point = IntervalStats.Points(current, previous, 2000)[0];
                Assert.AreEqual("S", point.Path);
                Assert.AreEqual(10, point.OkCount);
                Assert.AreEqual(2000, point.TimeMs);
                Assert.IsGreaterThan(400.0, point.P95Ms!.Value);
            });
    }

    private static StatsState State(int count, long ticks, int spikes = 0)
    {
        var histogram = new Histogram();
        histogram.Record(ticks, count);
        if (spikes > 0)
            histogram.Record(5_000_000, spikes);

        return new StatsState { Count = count + spikes, TotalTicks = ticks * count + 5_000_000L * spikes, Histogram = histogram.Encode() };
    }

    private static Scenario ScenarioWithStep()
    {
        var scenario = new Scenario("S");
        scenario.Steps.Add(new Step { Name = "Call", Id = "call" });
        scenario.SimulationsInternal.Add(new OneTimeLoadConfiguration(5));
        return scenario;
    }

    private static ScenarioLoadCollector RecordedCollector(int okCount, int failedCount)
    {
        var scenario = ScenarioWithStep();
        var collector = new ScenarioLoadCollector(scenario);
        collector.CacheSimulationDescriptions(scenario);
        collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, DateTime.UtcNow);

        for (var index = 0; index < okCount + failedCount; index++)
        {
            var isOk = index < okCount;
            var iteration = new IterationResult();
            iteration.ExecuteStartTime = DateTime.UtcNow;
            iteration.ExecuteEndTime = iteration.ExecuteStartTime.AddMilliseconds(5);
            iteration.StepResults.Add("Call", new StepStandardResult
            {
                Name = "Call",
                Status = isOk ? StepStatus.Passed : StepStatus.Failed,
                Duration = TimeSpan.FromMilliseconds(5),
                Exception = isOk ? null! : new InvalidOperationException("Boom")
            });

            collector.RecordMeasurement(isOk ? TestStatus.Passed : TestStatus.Failed, iteration);
        }

        return collector;
    }
}
