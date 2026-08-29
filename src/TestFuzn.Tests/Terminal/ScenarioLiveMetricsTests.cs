using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class ScenarioLiveMetricsTests : Test
{
    private static ScenarioLiveMetrics CreateMetrics()
    {
        return new ScenarioLiveMetrics("Checkout flow", new ILoadConfiguration[]
        {
            new FixedLoadConfiguration(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60))
        });
    }

    [Test]
    public async Task Verify_initial_state_and_first_tick_baseline()
    {
        await Scenario()
            .Step("Before any Record the model publishes an empty init view", context =>
            {
                var metrics = CreateMetrics();

                var view = metrics.Current;
                Assert.AreEqual("Checkout flow", metrics.ScenarioName);
                Assert.AreEqual("Checkout flow", view.ScenarioName);
                Assert.AreEqual(LoadTestPhase.Init, view.Phase);
                Assert.AreEqual("init", view.PhaseLabel);
                Assert.AreEqual(TimeSpan.Zero, view.Duration);
                Assert.AreEqual(TimeSpan.FromSeconds(60), view.PlannedDuration);
                Assert.AreEqual(0.0, view.ProgressFraction);
                Assert.AreEqual(TimeSpan.FromSeconds(60), view.EstimatedTimeRemaining);
                Assert.IsEmpty(view.Samples);
                Assert.IsEmpty(view.Errors);
                Assert.AreEqual(0, view.DistinctErrorCount);
                Assert.IsEmpty(view.Steps);
            })
            .Step("The first Record only establishes the baseline and appends no sample", context =>
            {
                var metrics = CreateMetrics();
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 5), SyntheticLoadSnapshots.At(0));

                Assert.IsEmpty(metrics.Current.Samples);
                Assert.AreEqual(5, metrics.Current.RequestCountOk);
            })
            .Step("Subsequent Records append per-second deltas with the newest sample last", context =>
            {
                var metrics = CreateMetrics();
                metrics.Record(SyntheticLoadSnapshots.Snapshot(), SyntheticLoadSnapshots.At(0));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 10, failed: 1), SyntheticLoadSnapshots.At(1));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 25, failed: 3), SyntheticLoadSnapshots.At(2));

                var view = metrics.Current;
                Assert.HasCount(2, view.Samples);
                Assert.AreEqual(10, view.Samples[0].OkDelta);
                Assert.AreEqual(1, view.Samples[0].FailedDelta);
                Assert.AreEqual(11.0, view.Samples[0].RequestsPerSecond);
                Assert.AreEqual(15, view.Samples[1].OkDelta);
                Assert.AreEqual(2, view.Samples[1].FailedDelta);
                Assert.AreEqual(17.0, view.Samples[1].RequestsPerSecond);
                Assert.AreEqual(SyntheticLoadSnapshots.At(2), view.Samples[1].Timestamp);
            })
            .Step("The series lists mirror the samples for sparkline consumption", context =>
            {
                var metrics = CreateMetrics();
                metrics.Record(SyntheticLoadSnapshots.Snapshot(), SyntheticLoadSnapshots.At(0));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 10, failed: 1), SyntheticLoadSnapshots.At(1));

                var view = metrics.Current;
                Assert.HasCount(1, view.RequestsPerSecondSeries);
                Assert.AreEqual(11.0, view.RequestsPerSecondSeries[0]);
                Assert.AreEqual(10.0, view.OkDeltaSeries[0]);
                Assert.AreEqual(1.0, view.FailedDeltaSeries[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_interval_math_and_non_advancing_timestamps()
    {
        await Scenario()
            .Step("A Record whose timestamp does not advance appends nothing and loses no requests (synthetic clock)", context =>
            {
                var metrics = CreateMetrics();
                metrics.Record(SyntheticLoadSnapshots.Snapshot(), SyntheticLoadSnapshots.At(0));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 7), SyntheticLoadSnapshots.At(0));
                Assert.IsEmpty(metrics.Current.Samples);

                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 20), SyntheticLoadSnapshots.At(2));
                var sample = Assert.ContainsSingle(metrics.Current.Samples);
                Assert.AreEqual(20, sample.OkDelta);
                Assert.AreEqual(10.0, sample.RequestsPerSecond);
            })
            .Step("Current-interval RPS divides the delta by the interval's actual length", context =>
            {
                var metrics = CreateMetrics();
                metrics.Record(SyntheticLoadSnapshots.Snapshot(), SyntheticLoadSnapshots.At(0));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 5), SyntheticLoadSnapshots.At(0.5));

                Assert.AreEqual(10.0, Assert.ContainsSingle(metrics.Current.Samples).RequestsPerSecond);
            })
            .Run();
    }

    [Test]
    public async Task Verify_counter_reset_clamps_and_rebaselines()
    {
        await Scenario()
            .Step("A backwards-moving counter clamps its delta to zero and re-baselines (synthetic — real collector counters never decrease)", context =>
            {
                var metrics = CreateMetrics();
                metrics.Record(SyntheticLoadSnapshots.Snapshot(), SyntheticLoadSnapshots.At(0));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 10, failed: 1), SyntheticLoadSnapshots.At(1));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 4, failed: 2), SyntheticLoadSnapshots.At(2));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 6, failed: 3), SyntheticLoadSnapshots.At(3));

                var view = metrics.Current;
                Assert.HasCount(3, view.Samples);
                Assert.AreEqual(0, view.Samples[1].OkDelta);
                Assert.AreEqual(1, view.Samples[1].FailedDelta);
                Assert.AreEqual(2, view.Samples[2].OkDelta);
                Assert.AreEqual(1, view.Samples[2].FailedDelta);
            })
            .Run();
    }

    [Test]
    public async Task Verify_ring_buffer_wraparound()
    {
        await Scenario()
            .Step("Past capacity the oldest samples drop and order stays oldest to newest (synthetic counters)", context =>
            {
                var metrics = CreateMetrics();

                // Cumulative ok is the triangle number of the tick, so the delta at tick t is t.
                var appendedSamples = ScenarioLiveMetrics.SampleCapacity + 5;
                for (var tick = 0; tick <= appendedSamples; tick++)
                    metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: tick * (tick + 1) / 2), SyntheticLoadSnapshots.At(tick));

                var view = metrics.Current;
                Assert.HasCount(ScenarioLiveMetrics.SampleCapacity, view.Samples);
                Assert.AreEqual(6, view.Samples[0].OkDelta);
                Assert.AreEqual(appendedSamples, view.Samples[view.Samples.Count - 1].OkDelta);
                Assert.AreEqual(6.0, view.OkDeltaSeries[0]);
                Assert.HasCount(ScenarioLiveMetrics.SampleCapacity, view.RequestsPerSecondSeries);
            })
            .Run();
    }

    [Test]
    public async Task Verify_interval_p95_pass_through_and_stats_copy()
    {
        await Scenario()
            .Step("Each sample carries the per-interval Ok p95 of the snapshot that closed it, not the cumulative one", context =>
            {
                var metrics = CreateMetrics();
                metrics.Record(SyntheticLoadSnapshots.Snapshot(), SyntheticLoadSnapshots.At(0));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 10, okPercentile95Ms: 100, intervalPercentile95Ms: 120), SyntheticLoadSnapshots.At(1));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 20, okPercentile95Ms: 110, intervalPercentile95Ms: 250), SyntheticLoadSnapshots.At(2));

                var view = metrics.Current;
                Assert.AreEqual(TimeSpan.FromMilliseconds(120), view.Samples[0].ResponseTimePercentile95);
                Assert.AreEqual(TimeSpan.FromMilliseconds(250), view.Samples[1].ResponseTimePercentile95);
                Assert.AreEqual(120.0, view.ResponseTimePercentile95Series[0]);
                Assert.AreEqual(250.0, view.ResponseTimePercentile95Series[1]);
                Assert.AreEqual(TimeSpan.FromMilliseconds(110), view.Ok.ResponseTimePercentile95);
            })
            .Step("The full Ok and Failed percentile spread is copied into the view", context =>
            {
                var snapshot = SyntheticLoadSnapshots.Snapshot();
                snapshot.Ok = new Stats();
                snapshot.Ok.RequestCount = 100;
                snapshot.Ok.RequestsPerSecond = 10;
                snapshot.Ok.ResponseTimeMin = TimeSpan.FromMilliseconds(5);
                snapshot.Ok.ResponseTimeMax = TimeSpan.FromMilliseconds(900);
                snapshot.Ok.ResponseTimeMean = TimeSpan.FromMilliseconds(50);
                snapshot.Ok.ResponseTimeStandardDeviation = TimeSpan.FromMilliseconds(12);
                snapshot.Ok.ResponseTimeMedian = TimeSpan.FromMilliseconds(45);
                snapshot.Ok.ResponseTimePercentile75 = TimeSpan.FromMilliseconds(60);
                snapshot.Ok.ResponseTimePercentile95 = TimeSpan.FromMilliseconds(80);
                snapshot.Ok.ResponseTimePercentile99 = TimeSpan.FromMilliseconds(200);
                snapshot.Failed.RequestCount = 7;
                snapshot.Failed.ResponseTimePercentile95 = TimeSpan.FromMilliseconds(1500);

                var metrics = CreateMetrics();
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(0));

                var view = metrics.Current;
                Assert.AreEqual(100, view.Ok.RequestCount);
                Assert.AreEqual(10, view.Ok.RequestsPerSecond);
                Assert.AreEqual(TimeSpan.FromMilliseconds(5), view.Ok.ResponseTimeMin);
                Assert.AreEqual(TimeSpan.FromMilliseconds(900), view.Ok.ResponseTimeMax);
                Assert.AreEqual(TimeSpan.FromMilliseconds(50), view.Ok.ResponseTimeMean);
                Assert.AreEqual(TimeSpan.FromMilliseconds(12), view.Ok.ResponseTimeStandardDeviation);
                Assert.AreEqual(TimeSpan.FromMilliseconds(45), view.Ok.ResponseTimeMedian);
                Assert.AreEqual(TimeSpan.FromMilliseconds(60), view.Ok.ResponseTimePercentile75);
                Assert.AreEqual(TimeSpan.FromMilliseconds(80), view.Ok.ResponseTimePercentile95);
                Assert.AreEqual(TimeSpan.FromMilliseconds(200), view.Ok.ResponseTimePercentile99);
                Assert.AreEqual(7, view.Failed.RequestCount);
                Assert.AreEqual(TimeSpan.FromMilliseconds(1500), view.Failed.ResponseTimePercentile95);
            })
            .Run();
    }

    [Test]
    public async Task Verify_error_ticker_dedupe_recency_and_counts()
    {
        await Scenario()
            .Step("Errors surface with counts and refresh recency only when their count changes", context =>
            {
                var metrics = CreateMetrics();

                var first = SyntheticLoadSnapshots.Snapshot(failed: 3);
                var checkout = SyntheticLoadSnapshots.Step("Checkout", failed: 3);
                SyntheticLoadSnapshots.AddError(checkout, "HTTP 500", 3);
                SyntheticLoadSnapshots.AddStep(first, checkout);
                metrics.Record(first, SyntheticLoadSnapshots.At(1));

                var entry = Assert.ContainsSingle(metrics.Current.Errors);
                Assert.AreEqual("Checkout", entry.StepName);
                Assert.AreEqual("HTTP 500", entry.Message);
                Assert.AreEqual(3, entry.Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), entry.LastSeen);

                var second = SyntheticLoadSnapshots.Snapshot(failed: 4);
                var checkoutAgain = SyntheticLoadSnapshots.Step("Checkout", failed: 3);
                SyntheticLoadSnapshots.AddError(checkoutAgain, "HTTP 500", 3);
                var login = SyntheticLoadSnapshots.Step("Login", failed: 1);
                SyntheticLoadSnapshots.AddError(login, "Timeout", 1);
                SyntheticLoadSnapshots.AddStep(second, checkoutAgain);
                SyntheticLoadSnapshots.AddStep(second, login);
                metrics.Record(second, SyntheticLoadSnapshots.At(2));

                var view = metrics.Current;
                Assert.HasCount(2, view.Errors);
                Assert.AreEqual(2, view.DistinctErrorCount);
                Assert.AreEqual("Timeout", view.Errors[0].Message);
                Assert.AreEqual("HTTP 500", view.Errors[1].Message);
                Assert.AreEqual(SyntheticLoadSnapshots.At(1), view.Errors[1].LastSeen);

                var third = SyntheticLoadSnapshots.Snapshot(failed: 6);
                var checkoutThird = SyntheticLoadSnapshots.Step("Checkout", failed: 5);
                SyntheticLoadSnapshots.AddError(checkoutThird, "HTTP 500", 5);
                SyntheticLoadSnapshots.AddStep(third, checkoutThird);
                metrics.Record(third, SyntheticLoadSnapshots.At(3));

                view = metrics.Current;
                Assert.AreEqual("HTTP 500", view.Errors[0].Message);
                Assert.AreEqual(5, view.Errors[0].Count);
                Assert.AreEqual(SyntheticLoadSnapshots.At(3), view.Errors[0].LastSeen);
            })
            .Step("The same message in different steps stays distinct, but same-named steps sum", context =>
            {
                var metrics = CreateMetrics();

                var snapshot = SyntheticLoadSnapshots.Snapshot(failed: 5);
                var checkout = SyntheticLoadSnapshots.Step("Checkout", failed: 2);
                SyntheticLoadSnapshots.AddError(checkout, "HTTP 500", 2);
                var nestedCheckout = SyntheticLoadSnapshots.Step("Checkout", failed: 3);
                SyntheticLoadSnapshots.AddError(nestedCheckout, "HTTP 500", 3);
                checkout.Steps.Add(nestedCheckout);
                var login = SyntheticLoadSnapshots.Step("Login", failed: 1);
                SyntheticLoadSnapshots.AddError(login, "HTTP 500", 1);
                SyntheticLoadSnapshots.AddStep(snapshot, checkout);
                SyntheticLoadSnapshots.AddStep(snapshot, login);
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(1));

                var view = metrics.Current;
                Assert.HasCount(2, view.Errors);
                var checkoutEntry = view.Errors.Single(error => error.StepName == "Checkout");
                Assert.AreEqual(5, checkoutEntry.Count);
                var loginEntry = view.Errors.Single(error => error.StepName == "Login");
                Assert.AreEqual(1, loginEntry.Count);
            })
            .Step("Errors in nested sub-steps surface under the sub-step's name", context =>
            {
                var metrics = CreateMetrics();

                var snapshot = SyntheticLoadSnapshots.Snapshot(failed: 1);
                var parent = SyntheticLoadSnapshots.Step("Parent step");
                var child = SyntheticLoadSnapshots.Step("Child step", failed: 1);
                SyntheticLoadSnapshots.AddError(child, "boom", 1);
                parent.Steps.Add(child);
                SyntheticLoadSnapshots.AddStep(snapshot, parent);
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(1));

                var entry = Assert.ContainsSingle(metrics.Current.Errors);
                Assert.AreEqual("Child step", entry.StepName);
                Assert.AreEqual("boom", entry.Message);
            })
            .Step("The published list is bounded to the most recently active distinct errors, and the distinct count says how many the tracker holds beyond it", context =>
            {
                var metrics = CreateMetrics();

                var snapshot = SyntheticLoadSnapshots.Snapshot(failed: 25);
                var step = SyntheticLoadSnapshots.Step("Checkout", failed: 25);
                for (var index = 1; index <= ScenarioLiveMetrics.ErrorCapacity + 5; index++)
                    SyntheticLoadSnapshots.AddError(step, "error-" + index, 1);
                SyntheticLoadSnapshots.AddStep(snapshot, step);
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(1));

                var view = metrics.Current;
                Assert.HasCount(ScenarioLiveMetrics.ErrorCapacity, view.Errors);
                Assert.AreEqual(ScenarioLiveMetrics.ErrorCapacity + 5, view.DistinctErrorCount);
                var messages = view.Errors.Select(error => error.Message).ToList();
                Assert.Contains("error-" + (ScenarioLiveMetrics.ErrorCapacity + 5), messages);
                Assert.DoesNotContain("error-1", messages);
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_rows_map_top_level_steps_in_order()
    {
        await Scenario()
            .Step("Each top-level step becomes one row with counts, skips, mean and p95", context =>
            {
                var snapshot = SyntheticLoadSnapshots.Snapshot(ok: 150, failed: 5);
                var login = SyntheticLoadSnapshots.Step("Login", ok: 100);
                login.Ok.ResponseTimeMean = TimeSpan.FromMilliseconds(50);
                login.Ok.ResponseTimePercentile95 = TimeSpan.FromMilliseconds(80);
                var checkout = SyntheticLoadSnapshots.Step("Checkout", ok: 50, failed: 5);
                checkout.SkippedCount = 3;
                SyntheticLoadSnapshots.AddStep(snapshot, login);
                SyntheticLoadSnapshots.AddStep(snapshot, checkout);

                var metrics = CreateMetrics();
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(0));

                var view = metrics.Current;
                Assert.HasCount(2, view.Steps);
                Assert.AreEqual("Login", view.Steps[0].Name);
                Assert.AreEqual(100, view.Steps[0].RequestCountOk);
                Assert.AreEqual(0, view.Steps[0].RequestCountFailed);
                Assert.AreEqual(0, view.Steps[0].SkippedCount);
                Assert.AreEqual(TimeSpan.FromMilliseconds(50), view.Steps[0].ResponseTimeMean);
                Assert.AreEqual(TimeSpan.FromMilliseconds(80), view.Steps[0].ResponseTimePercentile95);
                Assert.AreEqual("Checkout", view.Steps[1].Name);
                Assert.AreEqual(50, view.Steps[1].RequestCountOk);
                Assert.AreEqual(5, view.Steps[1].RequestCountFailed);
                Assert.AreEqual(3, view.Steps[1].SkippedCount);

                // No interval has closed and measurement never started, so both rates are zero.
                Assert.AreEqual(0.0, view.Steps[0].RequestsPerSecond);
                Assert.AreEqual(0.0, view.Steps[0].AverageRequestsPerSecond);
            })
            .Step("A step first seen mid-run gets an interval rate counted from zero (synthetic — real top-level steps exist from the start)", context =>
            {
                var metrics = CreateMetrics();
                metrics.Record(SyntheticLoadSnapshots.Snapshot(), SyntheticLoadSnapshots.At(0));

                var snapshot = SyntheticLoadSnapshots.Snapshot(ok: 6);
                SyntheticLoadSnapshots.AddStep(snapshot, SyntheticLoadSnapshots.Step("Late step", ok: 6));
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(2));

                var row = Assert.ContainsSingle(metrics.Current.Steps);
                Assert.AreEqual("Late step", row.Name);
                Assert.AreEqual(3.0, row.RequestsPerSecond, 1e-9);
            })
            .Step("A snapshot missing stats or steps is tolerated with explicit defaults", context =>
            {
                var bare = new ScenarioLoadResult();
                bare.ScenarioName = "Checkout flow";
                bare.WarmupRequestCountOk = 5;

                var metrics = CreateMetrics();
                metrics.Record(bare, SyntheticLoadSnapshots.At(0));
                Assert.AreEqual(0, metrics.Current.RequestCountOk);
                Assert.IsEmpty(metrics.Current.Steps);

                var bareLater = new ScenarioLoadResult();
                bareLater.ScenarioName = "Checkout flow";
                bareLater.WarmupRequestCountOk = 9;
                metrics.Record(bareLater, SyntheticLoadSnapshots.At(1));

                Assert.AreEqual(4, Assert.ContainsSingle(metrics.Current.Samples).OkDelta);
            })
            .Run();
    }

    [Test]
    public async Task Verify_published_snapshots_are_immutable_and_reads_stay_coherent()
    {
        await Scenario()
            .Step("A captured view never changes when later Records publish new ones", context =>
            {
                var metrics = CreateMetrics();
                metrics.Record(SyntheticLoadSnapshots.Snapshot(), SyntheticLoadSnapshots.At(0));
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 10), SyntheticLoadSnapshots.At(1));

                var captured = metrics.Current;
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 30), SyntheticLoadSnapshots.At(2));

                Assert.AreNotSame(captured, metrics.Current);
                Assert.HasCount(1, captured.Samples);
                Assert.AreEqual(10, captured.Samples[0].OkDelta);
                Assert.HasCount(2, metrics.Current.Samples);
            })
            .Step("A captured error count is immune to increments on the collector's shared ErrorEntry instance", context =>
            {
                var metrics = CreateMetrics();

                var snapshot = SyntheticLoadSnapshots.Snapshot(failed: 3);
                var checkout = SyntheticLoadSnapshots.Step("Checkout", failed: 3);
                SyntheticLoadSnapshots.AddError(checkout, "HTTP 500", 3);
                SyntheticLoadSnapshots.AddStep(snapshot, checkout);
                metrics.Record(snapshot, SyntheticLoadSnapshots.At(1));

                var capturedEntry = Assert.ContainsSingle(metrics.Current.Errors);
                Assert.AreEqual(3, capturedEntry.Count);

                // The collector keeps incrementing the same ErrorEntry instance on other
                // threads; the published copy must not alias it.
                var sourceEntry = checkout.Errors["HTTP 500"];
                Interlocked.Increment(ref sourceEntry.Count);
                Interlocked.Increment(ref sourceEntry.Count);

                Assert.AreEqual(5, sourceEntry.Count);
                Assert.AreEqual(3, capturedEntry.Count);
                Assert.AreEqual(3, Assert.ContainsSingle(metrics.Current.Errors).Count);
            })
            .Step("A reader concurrent with the ingest loop only ever sees coherent ticks", async context =>
            {
                var metrics = CreateMetrics();
                var tickCount = 1000;
                var minimumReadCount = 1000;
                var violations = 0;
                var readCount = 0;

                var writer = Task.Run(() =>
                {
                    // Ok grows by 2 and Failed by 3 every one-second tick, so every coherent
                    // view shows exactly these deltas in every sample and series entry.
                    for (var tick = 0; tick <= tickCount; tick++)
                        metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: tick * 2, failed: tick * 3), SyntheticLoadSnapshots.At(tick));
                });

                var reader = Task.Run(() =>
                {
                    // The read floor keeps the reader racing the writer for real instead of
                    // exiting after a handful of reads when the writer finishes quickly.
                    do
                    {
                        var view = metrics.Current;
                        readCount++;

                        if (view.Samples.Count != view.OkDeltaSeries.Count
                            || view.Samples.Count != view.FailedDeltaSeries.Count
                            || view.Samples.Count != view.RequestsPerSecondSeries.Count
                            || view.Samples.Count != view.ResponseTimePercentile95Series.Count
                            || view.Samples.Count != view.ResponseTimeMedianSeries.Count
                            || view.Samples.Count != view.ResponseTimePercentile99Series.Count
                            || view.Samples.Count != view.LatencyBucketSeries.Count)
                            violations++;

                        for (var index = 0; index < view.Samples.Count; index++)
                        {
                            var sample = view.Samples[index];
                            if (sample.OkDelta != 2 || sample.FailedDelta != 3 || sample.RequestsPerSecond != 5.0)
                                violations++;
                            if (view.OkDeltaSeries[index] != 2.0 || view.FailedDeltaSeries[index] != 3.0)
                                violations++;
                        }
                    } while (!writer.IsCompleted || readCount < minimumReadCount);
                });

                await Task.WhenAll(writer, reader);

                Assert.AreEqual(0, violations);
                Assert.IsGreaterThanOrEqualTo(minimumReadCount, readCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_record_requires_utc_timestamps()
    {
        await Scenario()
            .Step("A DateTimeKind.Local timestamp is rejected; Utc and Unspecified are accepted", context =>
            {
                var metrics = CreateMetrics();
                var local = DateTime.SpecifyKind(SyntheticLoadSnapshots.At(0), DateTimeKind.Local);
                Assert.ThrowsExactly<ArgumentException>(() => metrics.Record(SyntheticLoadSnapshots.Snapshot(), local));
                Assert.IsEmpty(metrics.Current.Samples);

                metrics.Record(SyntheticLoadSnapshots.Snapshot(), SyntheticLoadSnapshots.At(0));
                var unspecified = DateTime.SpecifyKind(SyntheticLoadSnapshots.At(1), DateTimeKind.Unspecified);
                metrics.Record(SyntheticLoadSnapshots.Snapshot(ok: 5), unspecified);

                Assert.AreEqual(5, Assert.ContainsSingle(metrics.Current.Samples).OkDelta);
            })
            .Run();
    }
}
