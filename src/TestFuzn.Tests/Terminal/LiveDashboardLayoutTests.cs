using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Internals.Thresholds;

namespace Fuzn.TestFuzn.Tests.Terminal;

/// <summary>
/// Golden frames for <see cref="LiveDashboardLayout"/>, derived by hand from the synthetic
/// snapshots below. The tile row shares the width less the one-column gaps equally, the
/// leftover columns widening the first boxes (five tiles at 120 columns: 116 / 5 = 23 and one
/// over, so boxes of 24, 23, 23, 23, 23 with inner widths 20, 19, 19, 19, 19); a gauge's bar
/// has the inner width less its caps, a space and its limit text in cells, filled with whole
/// cells and one partial cell of the rounded eighths (▎▍▌▋▊▉); a braille trend packs two
/// samples per column at four levels, newest at the right; the timeline shares its columns by
/// duration and puts the marker at floor(share of the current segment elapsed × its columns).
/// A chart body is derived from the chart widget's rules: a level is 1 + round(position ×
/// (levels − 1)) over the shared finite range (failed's zeros put the requests floor at 0, the
/// p50 line the latency floor at its own value), a sample stretched over the body columns
/// reads column c as position c × (samples − 1) / (columns − 1) between its two neighbours,
/// an area fills every level up to its own, a line paints each column's own level (plus, in
/// braille, the levels strictly between it and a neighbour's that are nearer to its own — a
/// tie to the older column), a cell any line passes shows only the line's dots in the line's
/// style whatever areas painted there, an area-only cell takes the style of the area owning
/// most of its dots (a tie to the later series), and a braille cell packs two columns of four
/// levels (left dots 0x40, 0x04, 0x02, 0x01 and right dots 0x80, 0x20, 0x10, 0x08, bottom-up)
/// — so ⣀ is one level in both columns, ⣤ two, ⣶ three, ⣿ four, and ⢀ / ⣠ / ⣴ / ⣾ a column
/// one level short of its right neighbour. The rich ramp's boundary columns are worked in the
/// comments; the columns between follow the interpolation. The heatmap tests in this file are worked in
/// sixths: a cell's step is ceil(count × 6 / max(total, the median total)). The chart, heatmap
/// and height-order tests continue in LiveDashboardLayoutTests.Charts.cs.
/// </summary>
[TestClass]
public partial class LiveDashboardLayoutTests : Test
{
    private const string Dim = "2";
    private const string Bold = "1";
    private const string Green = "38;5;2";
    private const string Red = "38;5;9";
    private const string Yellow = "38;5;11";
    private const string BoldGreen = "1;38;5;2";
    private const string BoldRed = "1;38;5;9";
    private const string BoldYellow = "1;38;5;11";
    private const string BoldAccent = "1;38;2;255;157;61";
    private const string Percentile99 = "38;2;255;92;0";
    private const string Percentile95 = "38;2;255;157;61";
    private const string Median = "38;2;255;207;107";
    private const string HeatStep1 = "38;2;58;20;0";
    private const string HeatStep4 = "38;2;255;149;54";
    private const string HeatStep5 = "38;2;255;207;107";
    private const string HeatStep6 = "38;2;255;255;255";

    private const string RequestsChartTitle = "requests — ok / failed";
    private const string LatencyChartTitle = "latency — p99 / p95 / p50";
    private const string HeatmapTitle = "latency heatmap";
    private const string StepsHeaderOnly = "step  count  rps  mean  p95  failed  fail%";

    /// <summary>The rich snapshot's plan: a 60 s warmup, a 120 s ramp and a 120 s steady simulation — 300 s, matching its planned duration.</summary>
    private static SimulationPlanEntry[] ThreePhasePlan()
    {
        return new[]
        {
            new SimulationPlanEntry("Fixed Load 20 rps", TimeSpan.FromSeconds(60), isWarmup: true),
            new SimulationPlanEntry("Gradual Load 10→50 rps", TimeSpan.FromSeconds(120), isWarmup: false),
            new SimulationPlanEntry("Fixed Load 50 rps", TimeSpan.FromSeconds(120), isWarmup: false)
        };
    }

    /// <summary>A 10 s warmup followed by a count-based simulation: the plan's total is unknown.</summary>
    private static SimulationPlanEntry[] IndeterminatePlan()
    {
        return new[]
        {
            new SimulationPlanEntry("Fixed Load 50 rps", TimeSpan.FromSeconds(10), isWarmup: true),
            new SimulationPlanEntry("One Time Load 500 iterations", null, isWarmup: false)
        };
    }

    /// <summary>The steady snapshots' plan: a 10 s warmup and a 50 s measurement simulation — 60 s.</summary>
    private static SimulationPlanEntry[] SteadyPlan()
    {
        return new[]
        {
            new SimulationPlanEntry("Fixed Load 100 rps", TimeSpan.FromSeconds(10), isWarmup: true),
            new SimulationPlanEntry("Fixed Load 100 rps", TimeSpan.FromSeconds(50), isWarmup: false)
        };
    }

    /// <summary>
    /// A mid-run snapshot with every dashboard element populated: a determinate three-phase
    /// plan 135 s into its 300 s (0.45, in the ramp), warmup counts, full percentile spreads,
    /// ten ring samples with their series (one short of a delta) — the ok deltas the samples'
    /// own ramp 88 → 141 with a failed request in three of them, the median flat at 35 ms and
    /// the p99 flat at 95 ms around the falling p95, and every interval's requests split
    /// eight into the ≤ 100 ms bucket and the rest into the ≤ 50 ms bucket — a 0.7 % error
    /// rate on the newest interval, two steps and two errors. The golden frames below are
    /// derived from these values by hand.
    /// </summary>
    private static LiveMetricsSnapshot RichSnapshot()
    {
        var okDeltas = new[] { 88, 96, 104, 111, 120, 128, 133, 138, 140, 141 };
        var buckets = new IReadOnlyList<int>[okDeltas.Length];
        for (var index = 0; index < okDeltas.Length; index++)
            buckets[index] = Counts((5, okDeltas[index] - 8), (6, 8));

        return new LiveMetricsSnapshot
        {
            ScenarioName = "Checkout flow",
            Phase = LoadTestPhase.Measurement,
            PhaseLabel = "sim 1/2: Gradual Load 10→50 rps",
            Duration = TimeSpan.FromSeconds(135),
            PlannedDuration = TimeSpan.FromSeconds(300),
            PlannedMeasurementDuration = TimeSpan.FromSeconds(240),
            ProgressFraction = 0.45,
            EstimatedTimeRemaining = TimeSpan.FromSeconds(165),
            IsCompleted = false,
            Status = TestStatus.Passed,
            RequestCountOk = 12480,
            RequestCountFailed = 32,
            WarmupRequestCountOk = 1200,
            WarmupRequestCountFailed = 3,
            RequestsPerSecond = 142,
            Ok = new LiveStats
            {
                RequestCount = 12480,
                RequestsPerSecond = 142,
                ResponseTimeMin = TimeSpan.FromMilliseconds(12),
                ResponseTimeMean = TimeSpan.FromMilliseconds(38),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(35),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(48),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(72),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(94),
                ResponseTimeMax = TimeSpan.FromMilliseconds(312)
            },
            Failed = new LiveStats
            {
                RequestCount = 32,
                RequestsPerSecond = 1,
                ResponseTimeMin = TimeSpan.FromMilliseconds(88),
                ResponseTimeMean = TimeSpan.FromMilliseconds(102),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(99),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(110),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(140),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(160),
                ResponseTimeMax = TimeSpan.FromMilliseconds(201)
            },
            IntervalRequestCount = 142,
            ErrorRate = 0.007,
            IntervalRequestsPerSecond = 142,
            Samples = new[]
            {
                Sample(1, 88, 0, 45), Sample(2, 96, 0, 44), Sample(3, 104, 0, 42), Sample(4, 111, 1, 41), Sample(5, 120, 0, 40),
                Sample(6, 128, 0, 40), Sample(7, 133, 1, 39), Sample(8, 138, 0, 39), Sample(9, 140, 0, 38), Sample(10, 141, 1, 38)
            },
            RequestsPerSecondSeries = new double[] { 88, 96, 104, 112, 120, 128, 134, 138, 140, 142 },
            OkDeltaSeries = new double[] { 88, 96, 104, 111, 120, 128, 133, 138, 140, 141 },
            FailedDeltaSeries = new double[] { 0, 0, 0, 1, 0, 0, 1, 0, 0, 1 },
            ResponseTimePercentile95Series = new double[] { 45, 44, 42, 41, 40, 40, 39, 39, 38, 38 },
            ResponseTimeMedianSeries = Repeat(35, 10),
            ResponseTimePercentile99Series = Repeat(95, 10),
            LatencyBucketSeries = buckets,
            PlanEntries = ThreePhasePlan(),
            Errors = new[]
            {
                new LiveErrorEntry { StepName = "Checkout", Message = "Connection refused (localhost:7058)", Count = 30 },
                new LiveErrorEntry { StepName = "Add to cart", Message = "Timeout after 30s", Count = 2 }
            },
            Steps = new[]
            {
                new LiveStepMetrics
                {
                    Name = "Add to cart",
                    RequestCountOk = 6250,
                    RequestCountFailed = 2,
                    RequestsPerSecond = 71.4,
                    AverageRequestsPerSecond = 69.5,
                    ResponseTimeMean = TimeSpan.FromMilliseconds(18),
                    ResponseTimePercentile95 = TimeSpan.FromMilliseconds(40)
                },
                new LiveStepMetrics
                {
                    Name = "Checkout",
                    RequestCountOk = 6230,
                    RequestCountFailed = 30,
                    RequestsPerSecond = 9.6,
                    AverageRequestsPerSecond = 69.2,
                    ResponseTimeMean = TimeSpan.FromMilliseconds(58),
                    ResponseTimePercentile95 = TimeSpan.FromMilliseconds(90)
                }
            }
        };
    }

    /// <summary>A warming-up snapshot with a count-based (indeterminate) plan and no data yet.</summary>
    private static LiveMetricsSnapshot IndeterminateSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Browse catalog",
            Phase = LoadTestPhase.Warmup,
            PhaseLabel = "warmup: Fixed Load 50 rps",
            Duration = TimeSpan.FromSeconds(42),
            PlanEntries = IndeterminatePlan()
        };
    }

    /// <summary>A completed snapshot failed by an assert, carrying the failure reason.</summary>
    private static LiveMetricsSnapshot FailedSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Checkout flow",
            PhaseLabel = "completed",
            Duration = TimeSpan.FromSeconds(300),
            IsCompleted = true,
            Status = TestStatus.Failed,
            StatusDetail = "Assert.IsLessThan failed. p95 too high: 240 ms"
        };
    }

    /// <summary>
    /// A long run with 9-digit counts, a 7-digit current rate and hour-class response times —
    /// the widest cells the requests table has to fit without truncating a number.
    /// </summary>
    private static LiveMetricsSnapshot HugeSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Huge numbers",
            PhaseLabel = "Fixed Load 10000000 rps",
            Duration = TimeSpan.FromHours(30),
            Ok = new LiveStats
            {
                RequestCount = 987654321,
                ResponseTimeMin = TimeSpan.FromMilliseconds(3600000),
                ResponseTimeMean = TimeSpan.FromMilliseconds(7200000),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(5400000),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(6000000),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(7200000),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(8000000),
                ResponseTimeMax = TimeSpan.FromMilliseconds(9000000)
            },
            Failed = new LiveStats
            {
                RequestCount = 123456789,
                ResponseTimeMin = TimeSpan.FromMilliseconds(100000),
                ResponseTimeMean = TimeSpan.FromMilliseconds(500000),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(400000),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(600000),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(800000),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(900000),
                ResponseTimeMax = TimeSpan.FromMilliseconds(950000)
            },
            Samples = new[] { Sample(1, 9999999, 1, 7200000) },
            RequestsPerSecondSeries = new double[] { 10000000 },
            OkDeltaSeries = new double[] { 9999999 },
            FailedDeltaSeries = new double[] { 1 },
            ResponseTimePercentile95Series = new double[] { 7200000 },
            ResponseTimeMedianSeries = new double[] { 5400000 },
            ResponseTimePercentile99Series = new double[] { 8000000 },
            LatencyBucketSeries = new IReadOnlyList<int>[] { Counts((14, 9999999)) }
        };
    }

    /// <summary>
    /// A ramp whose newest interval runs at 50 rps (45 ok + 5 failed) while the lifetime
    /// averages sit at 30/3 — the two measures disagree, so a frame shows which one it uses.
    /// </summary>
    private static LiveMetricsSnapshot RampSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Ramp",
            PhaseLabel = "Gradual Load 10→50 rps",
            Duration = TimeSpan.FromSeconds(60),
            RequestsPerSecond = 33,
            Ok = new LiveStats
            {
                RequestCount = 1800,
                RequestsPerSecond = 30,
                ResponseTimeMin = TimeSpan.FromMilliseconds(10),
                ResponseTimeMean = TimeSpan.FromMilliseconds(20),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(20),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(25),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(40),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(60),
                ResponseTimeMax = TimeSpan.FromMilliseconds(90)
            },
            Failed = new LiveStats
            {
                RequestCount = 180,
                RequestsPerSecond = 3,
                ResponseTimeMin = TimeSpan.FromMilliseconds(15),
                ResponseTimeMean = TimeSpan.FromMilliseconds(45),
                ResponseTimeMedian = TimeSpan.FromMilliseconds(45),
                ResponseTimePercentile75 = TimeSpan.FromMilliseconds(55),
                ResponseTimePercentile95 = TimeSpan.FromMilliseconds(70),
                ResponseTimePercentile99 = TimeSpan.FromMilliseconds(80),
                ResponseTimeMax = TimeSpan.FromMilliseconds(95)
            },
            Samples = new[] { Sample(1, 10, 0, 40), Sample(2, 20, 0, 40), Sample(3, 28, 2, 40), Sample(4, 38, 2, 40), Sample(5, 45, 5, 40) },
            RequestsPerSecondSeries = new double[] { 10, 20, 30, 40, 50 },
            OkDeltaSeries = new double[] { 10, 20, 28, 38, 45 },
            FailedDeltaSeries = new double[] { 0, 0, 2, 2, 5 },
            ResponseTimePercentile95Series = new double[] { 40, 40, 40, 40, 40 },
            ResponseTimeMedianSeries = Repeat(20, 5),
            ResponseTimePercentile99Series = Repeat(60, 5),
            LatencyBucketSeries = new IReadOnlyList<int>[] { Counts((5, 10)), Counts((5, 20)), Counts((5, 28)), Counts((5, 38)), Counts((5, 45)) },
            Steps = new[]
            {
                new LiveStepMetrics
                {
                    Name = "Add to cart",
                    RequestCountOk = 1800,
                    RequestCountFailed = 180,
                    RequestsPerSecond = 50,
                    AverageRequestsPerSecond = 30,
                    ResponseTimeMean = TimeSpan.FromMilliseconds(20),
                    ResponseTimePercentile95 = TimeSpan.FromMilliseconds(40)
                }
            }
        };
    }

    /// <summary>A snapshot whose only content is one step, for pinning single cells of the step table.</summary>
    private static LiveMetricsSnapshot StepSnapshot(int okCount, int failedCount, double requestsPerSecond = 0)
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "S",
            Steps = new[]
            {
                new LiveStepMetrics
                {
                    Name = "S",
                    RequestCountOk = okCount,
                    RequestCountFailed = failedCount,
                    RequestsPerSecond = requestsPerSecond,
                    ResponseTimeMean = TimeSpan.FromMilliseconds(1),
                    ResponseTimePercentile95 = TimeSpan.FromMilliseconds(2)
                }
            }
        };
    }

    /// <summary>Eleven samples at the given value followed by the newest: one more than the delta's distance, so a delta reads the newest against the first.</summary>
    private static double[] SteadySeries(double steady, double newest)
    {
        var series = new double[LiveDashboardLayout.DeltaSampleDistance + 2];
        for (var index = 0; index < series.Length - 1; index++)
            series[index] = steady;

        series[series.Length - 1] = newest;
        return series;
    }

    /// <summary>
    /// A steady measurement run 12 s into a 10 s warmup + 50 s plan (progress 0.2, 48 s to go)
    /// with the given rps and p95 series (a ring sample per entry, the ok deltas the whole
    /// rates, no failed requests; the median half the p95 and the p99 1.2 times it, every
    /// interval's requests in the bucket its p95 falls in — see <see cref="LatencySeries"/>),
    /// the given error rate on the newest interval, 1200 measurement and 100 warmup requests,
    /// the newest interval's latency (median 100, p95 412, p99 480 ms) and the given declared
    /// thresholds.
    /// </summary>
    private static LiveMetricsSnapshot SteadySnapshot(double[] requestsPerSecond, double[] responseTimePercentile95, double errorRate = 0, LiveThreshold[]? thresholds = null)
    {
        var samples = new LiveMetricsSample[requestsPerSecond.Length];
        var okDeltas = new int[requestsPerSecond.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            if (double.IsFinite(requestsPerSecond[index]) && requestsPerSecond[index] > 0)
                okDeltas[index] = (int)requestsPerSecond[index];

            var percentile95 = 0.0;
            if (double.IsFinite(responseTimePercentile95[index]) && responseTimePercentile95[index] > 0)
                percentile95 = responseTimePercentile95[index];

            samples[index] = Sample(index + 1, okDeltas[index], 0, percentile95);
        }

        if (thresholds == null)
            thresholds = Array.Empty<LiveThreshold>();

        var latency = LatencySeries(responseTimePercentile95, okDeltas);
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Steady",
            Phase = LoadTestPhase.Measurement,
            PhaseLabel = "Fixed Load 100 rps",
            Duration = TimeSpan.FromSeconds(12),
            PlannedDuration = TimeSpan.FromSeconds(60),
            PlannedMeasurementDuration = TimeSpan.FromSeconds(50),
            ProgressFraction = 0.2,
            EstimatedTimeRemaining = TimeSpan.FromSeconds(48),
            RequestCountOk = 1195,
            RequestCountFailed = 5,
            WarmupRequestCountOk = 100,
            Ok = new LiveStats { RequestCount = 1195 },
            Failed = new LiveStats { RequestCount = 5 },
            IntervalRequestCount = samples[samples.Length - 1].OkDelta,
            ErrorRate = errorRate,
            IntervalRequestsPerSecond = requestsPerSecond[requestsPerSecond.Length - 1],
            IntervalLatency = new IntervalLatency(112, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(412), TimeSpan.FromMilliseconds(480), new int[LatencyBuckets.Count]),
            Thresholds = thresholds,
            Samples = samples,
            RequestsPerSecondSeries = requestsPerSecond,
            OkDeltaSeries = ToDoubles(okDeltas),
            FailedDeltaSeries = new double[okDeltas.Length],
            ResponseTimePercentile95Series = responseTimePercentile95,
            ResponseTimeMedianSeries = latency.Median,
            ResponseTimePercentile99Series = latency.Percentile99,
            LatencyBucketSeries = latency.Buckets,
            PlanEntries = SteadyPlan()
        };
    }

    /// <summary>
    /// The steady run with four declared thresholds in three states: p95 ≤ 500 ms reading 412
    /// (Warning), error rate ≤ 1 % reading 0.5 % (Ok), rps ≥ 150 reading 112 (Breached for
    /// 3 s) and p99 ≤ 800 ms reading 480 (Ok) — the last adds a tile of its own.
    /// </summary>
    private static LiveMetricsSnapshot ThresholdSnapshot()
    {
        return SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 412), errorRate: 0.005, thresholds: new[]
        {
            Judged(ThresholdMetric.ResponseTimePercentile95, 500, ThresholdComparison.LessThanOrEqualTo, 412, ThresholdState.Warning),
            Judged(ThresholdMetric.ErrorRate, 0.01, ThresholdComparison.LessThanOrEqualTo, 0.005, ThresholdState.Ok),
            Judged(ThresholdMetric.RequestsPerSecond, 150, ThresholdComparison.GreaterThanOrEqualTo, 112, ThresholdState.Breached, breachedForSeconds: 3),
            Judged(ThresholdMetric.ResponseTimePercentile99, 800, ThresholdComparison.LessThanOrEqualTo, 480, ThresholdState.Ok)
        });
    }

    private static LiveThreshold Judged(ThresholdMetric metric, double limit, ThresholdComparison comparison, double current, ThresholdState state, int breachedForSeconds = 0)
    {
        return new LiveThreshold(new Threshold(metric, limit, comparison), current, state, TimeSpan.FromSeconds(breachedForSeconds));
    }

    /// <summary>
    /// A steady run 130 samples long — more than twice the trend window — whose p95 series is
    /// 70 samples at 1000 ms, then 59 at 100 ms and the newest at 250 ms: a spike against the
    /// trailing 60 (their median is 100 ms) that is none against the whole series (median
    /// 1000 ms). The rps series holds 100 throughout, a ring sample per entry, and the newest
    /// interval's latency is 250 ms across the board.
    /// </summary>
    private static LiveMetricsSnapshot LongRunSnapshot()
    {
        var responseTimes = Repeat(1000, 70).Concat(Repeat(100, 59)).Concat(new double[] { 250 }).ToArray();
        var samples = new LiveMetricsSample[responseTimes.Length];
        var okDeltas = new int[responseTimes.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            okDeltas[index] = 100;
            samples[index] = Sample(index + 1, 100, 0, responseTimes[index]);
        }

        var latency = LatencySeries(responseTimes, okDeltas);
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Long run",
            Phase = LoadTestPhase.Measurement,
            PhaseLabel = "Fixed Load 100 rps",
            Duration = TimeSpan.FromSeconds(130),
            RequestCountOk = 13000,
            Ok = new LiveStats { RequestCount = 13000 },
            IntervalRequestCount = 100,
            IntervalRequestsPerSecond = 100,
            IntervalLatency = new IntervalLatency(100, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), new int[LatencyBuckets.Count]),
            Samples = samples,
            RequestsPerSecondSeries = Repeat(100, responseTimes.Length),
            OkDeltaSeries = Repeat(100, responseTimes.Length),
            FailedDeltaSeries = new double[responseTimes.Length],
            ResponseTimePercentile95Series = responseTimes,
            ResponseTimeMedianSeries = latency.Median,
            ResponseTimePercentile99Series = latency.Percentile99,
            LatencyBucketSeries = latency.Buckets
        };
    }

    /// <summary>
    /// The view the console manager shows for a scenario before its live model exists (its
    /// init placeholder): the name, the init phase, the elapsed time and the declared
    /// thresholds in their placeholder state — every one Ok with a Current of 0 — and nothing
    /// else: no sample, no series, no plan. The same four thresholds as
    /// <see cref="ThresholdSnapshot"/>, so the p99 one adds its tile from the first frame on.
    /// </summary>
    private static LiveMetricsSnapshot InitSnapshot()
    {
        var thresholds = new[]
        {
            new Threshold(ThresholdMetric.ResponseTimePercentile95, 500, ThresholdComparison.LessThanOrEqualTo),
            new Threshold(ThresholdMetric.ErrorRate, 0.01, ThresholdComparison.LessThanOrEqualTo),
            new Threshold(ThresholdMetric.RequestsPerSecond, 150, ThresholdComparison.GreaterThanOrEqualTo),
            new Threshold(ThresholdMetric.ResponseTimePercentile99, 800, ThresholdComparison.LessThanOrEqualTo)
        };

        return new LiveMetricsSnapshot
        {
            ScenarioName = "Checkout flow",
            Phase = LoadTestPhase.Init,
            PhaseLabel = "init",
            Duration = TimeSpan.FromSeconds(2),
            Thresholds = ThresholdEvaluator.InitialStates(thresholds)
        };
    }

    /// <summary>A one-second ring sample closed at the given second; its rate is the combined delta.</summary>
    private static LiveMetricsSample Sample(int second, int okDelta, int failedDelta, double percentile95Milliseconds)
    {
        var timestamp = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc).AddSeconds(second);
        return new LiveMetricsSample(timestamp, okDelta, failedDelta, okDelta + failedDelta, TimeSpan.FromMilliseconds(percentile95Milliseconds));
    }

    [Test]
    public async Task Verify_dashboard_golden_frame_at_120x40()
    {
        // Five boxes of 24, 23, 23, 23, 23 (inner 20, 19, 19, 19, 19). The rps trend's levels
        // 1 1 2 2 3 3 4 4 4 4 pair into ⣀⣤⣶⣿⣿ and the p95 trend's 4 4 3 2 2 2 1 1 1 1 into
        // ⣿⣦⣤⣀⣀, each at the right of its box. "12512 warmup 1203" (17) fits the 19-wide
        // requests box; the elapsed gauge has 19 − 2 − 7 = 10 cells, 0.45 × 10 = 4.5 — four
        // full cells and a four-eighths cell — with no warning band and "2m 45s" after it.
        // The timeline splits 120 columns 24 / 48 / 48; 135 s is 75 s into the ramp, so the
        // marker is column 24 + floor(75 / 120 × 48) = 54 — the "0" of "50" in the ramp's
        // label, which sits on columns 37-58 — and the columns before it are filled.
        await Scenario()
            .Step("The frame pads to the window height with the footer on the last row", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine("q quit", 6, lines[39]);
                AssertMaximumWidth(120, lines);
            })
            .Step("The title line carries name, status and the compact logo top-right", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine("Checkout flow  ● Running" + Spaces(83) + "  ⚡ TestFuzn", 120, lines[0]);
            })
            .Step("The tile row shows rps, p95, errors, requests and elapsed with trends, units and the progress gauge", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine(Row(Top(24), Top(23), Top(23), Top(23), Top(23)), 120, lines[1]);
                AssertLine(Row(Box("rps" + Spaces(17)), Box("p95" + Spaces(16)), Box("errors" + Spaces(13)), Box("requests" + Spaces(11)), Box("elapsed" + Spaces(12))), 120, lines[2]);
                AssertLine(Row(Box("142" + Spaces(17)), Box("38 ms" + Spaces(14)), Box("0.7 %" + Spaces(14)), Box("12512 warmup 1203" + Spaces(2)), Box("00:02:15 / 5m" + Spaces(6))), 120, lines[3]);
                AssertLine(Row(Box(Spaces(15) + "⣀⣤⣶⣿⣿"), Box(Spaces(14) + "⣿⣦⣤⣀⣀"), Box(Spaces(19)), Box(Spaces(19)), Box("▕████▌·····▏ 2m 45s")), 120, lines[4]);
                AssertLine(Row(Bottom(24), Bottom(23), Bottom(23), Bottom(23), Bottom(23)), 120, lines[5]);
            })
            .Step("The timeline fills the warmup and the ramp up to the marker inside the ramp's label, with every label inside its segment", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine("== Fixed Load 20 rps ===" + "############ Gradual Load 10→5▼ rps ------------" + "-------------- Fixed Load 50 rps ---------------", 120, lines[6]);
                AssertLine(string.Empty, 0, lines[7]);
            })
            .Step("The chart panels sit side by side: the ok ramp over the failed floor on the left, the latency bands under the median line on the right", context =>
            {
                // Requests: the failed series' zeros floor the scale at 0 and the newest ok
                // count tops it at 141, so the 24 levels are 6.13 requests each and the ten
                // ok samples sit at levels 15, 17, 18, 19, 21, 22, 23, 24, 24, 24 — all above
                // level 12, so rows 3-5 are solid. The axis "141" / "71" (the midpoint 70.5
                // rounded away from zero) / "0" is 4 columns and the annotation " ▶ 141" 6,
                // so the body is 45 characters = 90 columns with sample i at column 9.89 i.
                // Row 2 (levels 13-16) is ⣶ in its first cell alone — columns 0 and 1 read
                // 88 and 88.8, level 15 — and full from column 2 (v ≥ 88.9). Row 1 (17-20)
                // starts at column 9, the first with v ≥ 95.0, as the right-hand dot ⢀; row 0
                // (21-24) at column 40, the first with v ≥ 119.5; and level 24 (v ≥ 137.9)
                // holds from column 70, the last ten cells. Latency: the flat p50 floors the
                // scale at 35 and the p99 band tops it at 95, so the p99 and p95 areas paint
                // every level of every column, and the median — a line at the floor, level 1
                // in every column, no neighbour to join — takes every bottom-row cell: a cell
                // a line passes shows only the line's dots, ⣀, over the fill. The rows above
                // are solid; the axis "95 ms" / "65 ms" / "35 ms" is 6 columns and the
                // annotation " ▶ 95 ms" (the newest p99) 8, so the body is 41.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle, 59), 120, lines[8]);
                AssertLine(Box("141┤" + Spaces(20) + "⣀⣀⣀⣠⣤⣤⣤⣤⣴⣶⣶⣶⣶⣶⣶" + Glyphs('⣿', 10) + " ▶ 141") + "  " + Box("95 ms┤" + Glyphs('⣿', 41) + " ▶ 95 ms"), 120, lines[9]);
                AssertLine(Box("   │" + Spaces(4) + "⢀⣀⣀⣀⣠⣤⣤⣤⣴⣶⣶⣶⣾" + Glyphs('⣿', 28) + Spaces(6)) + "  " + Box("     │" + Glyphs('⣿', 41) + Spaces(8)), 120, lines[10]);
                AssertLine(Box(" 71┤⣶" + Glyphs('⣿', 44) + Spaces(6)) + "  " + Box("65 ms┤" + Glyphs('⣿', 41) + Spaces(8)), 120, lines[11]);
                AssertLine(Box("   │" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("     │" + Glyphs('⣿', 41) + Spaces(8)), 120, lines[12]);
                AssertLine(Box("   │" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("     │" + Glyphs('⣿', 41) + Spaces(8)), 120, lines[13]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 45) + Spaces(6)) + "  " + Box("35 ms┤" + Glyphs('⣀', 41) + Spaces(8)), 120, lines[14]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, lines[15]);
            })
            .Step("TrueColor: the legend words, the bands, the median line and the annotations carry their series' colours over a dim axis, and a failed request stays sub-cell", context =>
            {
                // A failed count of 1 is level 1 on the 0..141 scale — the dot the zeros
                // paint too — so the failed area owns two of the bottom cell's eight dots
                // and the ok area six: the requests body is green throughout. The latency
                // bottom row is the median line's, in its own colour, whatever the p95 band
                // (levels 2 to 5: 38 to 45 ms on the 35..95 scale) painted under it; above
                // it p95 reaches level 5 only in columns 0-7 (v ≥ 44.13), two dots of row 4's
                // first cells against p99's eight, so every upper row is p99's.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.TrueColor);

                Assert.Contains(Sgr(BoldAccent, "requests") + " " + Sgr(Dim, "—") + " " + Sgr(Green, "ok") + " " + Sgr(Dim, "/") + " " + Sgr(Red, "failed"), lines[8].Text);
                Assert.Contains(Sgr(BoldAccent, "latency") + " " + Sgr(Dim, "—") + " " + Sgr(Percentile99, "p99") + " " + Sgr(Dim, "/") + " " + Sgr(Percentile95, "p95") + " " + Sgr(Dim, "/") + " " + Sgr(Median, "p50"), lines[8].Text);
                Assert.Contains(Sgr(Green, "⣀⣀⣀⣠⣤⣤⣤⣤⣴⣶⣶⣶⣶⣶⣶" + Glyphs('⣿', 10)) + Sgr(Green, " ▶ 141"), lines[9].Text);
                Assert.Contains(Sgr(Dim, "95 ms┤") + Sgr(Percentile99, Glyphs('⣿', 41)) + Sgr(Percentile99, " ▶ 95 ms"), lines[9].Text);
                Assert.Contains(Spaces(5) + Sgr(Dim, "│") + Sgr(Percentile99, Glyphs('⣿', 41)), lines[13].Text);
                Assert.Contains("  " + Sgr(Dim, "0┤") + Sgr(Green, Glyphs('⣿', 45)), lines[14].Text);
                Assert.Contains(Sgr(Dim, "35 ms┤") + Sgr(Median, Glyphs('⣀', 41)), lines[14].Text);
                Assert.DoesNotContain(Sgr(Red, "⣿"), lines[14].Text);
            })
            .Step("The heatmap panel spans the width under the charts with the newest samples in the rightmost columns", context =>
            {
                // Eight rows for fifteen buckets: every pair below the slowest merges under
                // its slower label — "≤ 50 ms" holds buckets 4-5 and "≤ 200 ms" 6-7. The
                // widest label is 8 columns, so the body is 116 − 9 = 107 and the ten samples
                // fill its last ten. Each interval's eight ≤ 100 ms requests are under a sixth
                // of any total: step 1, a dot. Its ≤ 50 ms count is the rest, weighted by the
                // interval's total against the median total (120 + 128) / 2 = 124: 80 × 6 /
                // 124 = 3.9 → 4 (+), 88, 96 and 103 → 5 (*), and from 120 requests on each
                // share is over five sixths of its own total: 6 (#).
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine(PanelTop(HeatmapTitle, 120), 120, lines[16]);
                AssertLine(Box("  > 30 s " + Spaces(107)), 120, lines[17]);
                AssertLine(Box("  ≤ 30 s " + Spaces(107)), 120, lines[18]);
                AssertLine(Box("   ≤ 5 s " + Spaces(107)), 120, lines[19]);
                AssertLine(Box("   ≤ 1 s " + Spaces(107)), 120, lines[20]);
                AssertLine(Box("≤ 200 ms " + Spaces(97) + ".........."), 120, lines[21]);
                AssertLine(Box(" ≤ 50 ms " + Spaces(97) + "+***######"), 120, lines[22]);
                AssertLine(Box(" ≤ 10 ms " + Spaces(107)), 120, lines[23]);
                AssertLine(Box("  ≤ 2 ms " + Spaces(107)), 120, lines[24]);
                AssertLine(Bottom(120), 120, lines[25]);

                var styled = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.TrueColor);
                Assert.Contains(Sgr(Dim, "≤ 200 ms") + " " + Spaces(97) + Sgr(HeatStep1, Glyphs('█', 10)), styled[21].Text);
                Assert.Contains(Sgr(Dim, "≤ 50 ms") + " " + Spaces(97) + Sgr(HeatStep4, "█") + Sgr(HeatStep5, "███") + Sgr(HeatStep6, "██████"), styled[22].Text);
            })
            .Step("The requests panel shows the warmup headline, the current rates and the full spread", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine("╭─ Requests " + new string('─', 107) + "╮", 120, lines[26]);
                AssertLine("│ warmup 1200 ok · 3 failed" + Spaces(91) + " │", 120, lines[27]);
                AssertLine("│         count  rps    min    mean    p50     p75     p95     p99     max" + Spaces(44) + " │", 120, lines[28]);
                AssertLine("│ ok      12480  141  12 ms   38 ms  35 ms   48 ms   72 ms   94 ms  312 ms" + Spaces(44) + " │", 120, lines[29]);
                AssertLine("│ failed     32  1.0  88 ms  102 ms  99 ms  110 ms  140 ms  160 ms  201 ms" + Spaces(44) + " │", 120, lines[30]);
                AssertLine(Bottom(120), 120, lines[31]);
            })
            .Step("The drop order gives the two step rows back to fit the frame: the step table keeps its column headings and the error ticker both errors, newest first", context =>
            {
                // 8 header rows, 8 chart rows, 10 heatmap rows, 6 requests rows, a 5-row step
                // table and a 4-row error ticker are 41 against the 39 rows above the footer:
                // the two step rows are the first to go, and a table without rows is as wide
                // as its headings.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None);

                AssertLine(PanelTop("Steps", 120), 120, lines[32]);
                AssertLine(Box(StepsHeaderOnly + Spaces(74)), 120, lines[33]);
                AssertLine(Bottom(120), 120, lines[34]);
                AssertLine(PanelTop("Errors", 120), 120, lines[35]);
                AssertLine("│ 30× Checkout · Connection refused (localhost:7058)" + Spaces(66) + " │", 120, lines[36]);
                AssertLine("│  2× Add to cart · Timeout after 30s" + Spaces(81) + " │", 120, lines[37]);
                AssertLine(Bottom(120), 120, lines[38]);
            })
            .Step("One row taller, the step rows come back: the step table shows counts, current rate, latencies and the fail% mini-bar", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 42, ColorMode.None);

                AssertLine("│ Add to cart   6252   71  18 ms  40 ms       2  █░░░░ <0.1%" + Spaces(58) + " │", 120, lines[34]);
                AssertLine("│ Checkout      6260  9.6  58 ms  90 ms      30  █░░░░ 0.5%" + Spaces(59) + " │", 120, lines[35]);
                AssertLine("│ 30× Checkout · Connection refused (localhost:7058)" + Spaces(66) + " │", 120, lines[38]);
                AssertLine("q quit", 6, lines[41]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_dashboard_golden_frame_at_100x30()
    {
        // Boxes of 20, 19, 19, 19, 19 (inner 16, 15, 15, 15, 15): "12512 warmup 1203" no
        // longer fits the requests box, so its unit goes; the elapsed gauge has 15 − 9 = 6
        // cells, 0.45 × 6 = 2.7 — two full cells and a six-eighths cell. The timeline splits
        // 20 / 40 / 40 with the marker on column 20 + floor(75 / 120 × 40) = 45, the "5" of
        // "50" (the ramp's label sits on columns 29-50). The charts are still side by side,
        // in 49-column panels holding 45-column bodies of six rows (the height is exactly 30):
        // the rps body is 35 characters = 70 columns with sample i at column 7.67 i, so row 0
        // (levels 21-24, v ≥ 119.5) starts at column 31 — the right-hand dot of cell 15 — and
        // row 2 (13-16) is ⣾ in its first cell alone, column 0 at level 15 and column 1 (89.0)
        // at 16; the latency body is 31 characters, solid over the median line's ⣀ bottom
        // row. No heatmap below 36 rows. The
        // section's 8 + 8 + 6 + 5 + 4 = 31 rows are two over the 29 above the footer, so the
        // two step rows go: the drop order at 100×30.
        await Scenario()
            .Step("The layout keeps its structure at 100 columns with the logo still shown, the charts six rows tall side by side and the step rows given back", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 100, 30, ColorMode.None);

                Assert.HasCount(30, lines);
                AssertLine("Checkout flow  ● Running" + Spaces(63) + "  ⚡ TestFuzn", 100, lines[0]);
                AssertLine(Row(Top(20), Top(19), Top(19), Top(19), Top(19)), 100, lines[1]);
                AssertLine(Row(Box("rps" + Spaces(13)), Box("p95" + Spaces(12)), Box("errors" + Spaces(9)), Box("requests" + Spaces(7)), Box("elapsed" + Spaces(8))), 100, lines[2]);
                AssertLine(Row(Box("142" + Spaces(13)), Box("38 ms" + Spaces(10)), Box("0.7 %" + Spaces(10)), Box("12512" + Spaces(10)), Box("00:02:15 / 5m" + Spaces(2))), 100, lines[3]);
                AssertLine(Row(Box(Spaces(11) + "⣀⣤⣶⣿⣿"), Box(Spaces(10) + "⣿⣦⣤⣀⣀"), Box(Spaces(15)), Box(Spaces(15)), Box("▕██▊···▏ 2m 45s")), 100, lines[4]);
                AssertLine(Row(Bottom(20), Bottom(19), Bottom(19), Bottom(19), Bottom(19)), 100, lines[5]);
                AssertLine(" Fixed Load 20 rps =" + "######## Gradual Load 10→▼0 rps --------" + "---------- Fixed Load 50 rps -----------", 100, lines[6]);
                AssertLine(PanelTop(RequestsChartTitle, 49) + "  " + PanelTop(LatencyChartTitle, 49), 100, lines[8]);
                AssertLine(Box("141┤" + Spaces(15) + "⢀⣀⣀⣠⣤⣤⣤⣴⣶⣶⣶⣶" + Glyphs('⣿', 8) + " ▶ 141") + "  " + Box("95 ms┤" + Glyphs('⣿', 31) + " ▶ 95 ms"), 100, lines[9]);
                AssertLine(Box("   │" + Spaces(3) + "⢀⣀⣀⣠⣤⣤⣴⣶⣶⣶" + Glyphs('⣿', 22) + Spaces(6)) + "  " + Box("     │" + Glyphs('⣿', 31) + Spaces(8)), 100, lines[10]);
                AssertLine(Box(" 71┤⣾" + Glyphs('⣿', 34) + Spaces(6)) + "  " + Box("65 ms┤" + Glyphs('⣿', 31) + Spaces(8)), 100, lines[11]);
                AssertLine(Box("   │" + Glyphs('⣿', 35) + Spaces(6)) + "  " + Box("     │" + Glyphs('⣿', 31) + Spaces(8)), 100, lines[12]);
                AssertLine(Box("   │" + Glyphs('⣿', 35) + Spaces(6)) + "  " + Box("     │" + Glyphs('⣿', 31) + Spaces(8)), 100, lines[13]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 35) + Spaces(6)) + "  " + Box("35 ms┤" + Glyphs('⣀', 31) + Spaces(8)), 100, lines[14]);
                AssertLine(Bottom(49) + "  " + Bottom(49), 100, lines[15]);
                AssertLine(PanelTop("Requests", 100), 100, lines[16]);
                AssertLine("│         count  rps    min    mean    p50     p75     p95     p99     max" + Spaces(24) + " │", 100, lines[18]);
                AssertLine(Bottom(100), 100, lines[21]);
                AssertLine(PanelTop("Steps", 100), 100, lines[22]);
                AssertLine(Box(StepsHeaderOnly + Spaces(54)), 100, lines[23]);
                AssertLine(Bottom(100), 100, lines[24]);
                AssertLine(PanelTop("Errors", 100), 100, lines[25]);
                AssertLine("│ 30× Checkout · Connection refused (localhost:7058)" + Spaces(46) + " │", 100, lines[26]);
                AssertLine("│  2× Add to cart · Timeout after 30s" + Spaces(61) + " │", 100, lines[27]);
                AssertLine(Bottom(100), 100, lines[28]);
                AssertLine("q quit", 6, lines[29]);
                AssertMaximumWidth(100, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain(HeatmapTitle, line.Text);
            })
            .Step("One column narrower the charts stack: at 99 columns each panel spans the width with its own axis and annotation, the requests chart above the latency chart", context =>
            {
                // At 99×40 the header is the same 8 rows (five boxes of 19; the timeline's
                // 19-column warmup segment still holds its label), the two stacked panels
                // 16, and the heatmap, the requests panel and the two tables 10 + 6 + 5 + 4:
                // 49 against 39, so the step rows, the error rows, both tables and then the
                // heatmap go — both charts stay, at rows 8-15 and 16-23, the requests panel
                // at 24-29, and nine blank rows pad down to the footer.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 99, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine(PanelTop(RequestsChartTitle, 99), 99, lines[8]);
                Assert.StartsWith("│ 141┤", lines[9].Text);
                Assert.EndsWith(" ▶ 141 │", lines[9].Text);
                AssertLine(Bottom(99), 99, lines[15]);
                AssertLine(PanelTop(LatencyChartTitle, 99), 99, lines[16]);
                Assert.StartsWith("│ 95 ms┤", lines[17].Text);
                Assert.EndsWith(" ▶ 95 ms │", lines[17].Text);
                AssertLine(Bottom(99), 99, lines[23]);
                AssertLine(PanelTop("Requests", 99), 99, lines[24]);
                AssertLine(Bottom(99), 99, lines[29]);
                AssertLine(string.Empty, 0, lines[30]);
                AssertLine("q quit", 6, lines[39]);
                AssertMaximumWidth(99, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_dashboard_golden_frame_at_80x24()
    {
        // Boxes of 16, 15, 15, 15, 15 (inner 12, 11, 11, 11, 11) and, at 24 rows, no trends:
        // neither unit fits its box and the elapsed gauge would need 13 columns, so every tile
        // is two lines. The timeline splits 16 / 32 / 32 with the marker on column
        // 16 + floor(75 / 120 × 32) = 36 — the arrow of "10→50", the label sitting on 21-42 —
        // and the warmup label, which needs 19 columns of 16, goes to the legend line. Below
        // 100 columns the charts stack at the full width and below 30 rows their bodies are
        // four rows: 8 header rows, two 6-row chart panels, 6 requests rows, 5 step rows and
        // 4 error rows are 35 against 23, so the drop order takes the step rows, the error
        // rows, both tables and then the latency chart — the rps chart stays, then the
        // requests panel, and two blank rows pad down to the footer. The rps body is 66
        // characters = 132 columns at 16 levels (8.8 requests each): the ten samples sit at
        // levels 10, 11, 12, 13, 14, 15, 15, 16, 16, 16, so rows 2 and 3 (levels 1-8) are
        // solid; row 1 (9-12) is ⣤ in cell 0 (columns 0 and 1 at level 10), ⣴ in cell 1
        // (column 3 reads 89.6, level 11) and full from column 20 (v ≥ 98.7); row 0 (13-16,
        // v ≥ 108.1) starts at column 38, in the 20th cell, and level 16 (v ≥ 136.3) holds
        // from column 97 — the right half of the ⣾ cell — and the last 17 cells whole.
        await Scenario()
            .Step("At 80 columns and 24 rows the tiles drop their trends and units, the timeline grows a legend, and only the four-row rps chart survives the drop order", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 80, 24, ColorMode.None);

                Assert.HasCount(24, lines);
                AssertLine("Checkout flow  ● Running" + Spaces(43) + "  ⚡ TestFuzn", 80, lines[0]);
                AssertLine(Row(Top(16), Top(15), Top(15), Top(15), Top(15)), 80, lines[1]);
                AssertLine(Row(Box("rps" + Spaces(9)), Box("p95" + Spaces(8)), Box("errors" + Spaces(5)), Box("requests" + Spaces(3)), Box("elapsed" + Spaces(4))), 80, lines[2]);
                AssertLine(Row(Box("142" + Spaces(9)), Box("38 ms" + Spaces(6)), Box("0.7 %" + Spaces(6)), Box("12512" + Spaces(6)), Box("00:02:15" + Spaces(3))), 80, lines[3]);
                AssertLine(Row(Bottom(16), Bottom(15), Bottom(15), Bottom(15), Bottom(15)), 80, lines[4]);
                AssertLine(new string('=', 16) + "#### Gradual Load 10▼50 rps ----" + "------ Fixed Load 50 rps -------", 80, lines[5]);
                AssertLine("Fixed Load 20 rps" + Spaces(63), 80, lines[6]);
                AssertLine(string.Empty, 0, lines[7]);
                AssertLine(PanelTop(RequestsChartTitle, 80), 80, lines[8]);
                AssertLine(Box("141┤" + Spaces(19) + Glyphs('⣀', 8) + "⣠" + Glyphs('⣤', 7) + "⣴" + Glyphs('⣶', 12) + "⣾" + Glyphs('⣿', 17) + " ▶ 141"), 80, lines[9]);
                AssertLine(Box(" 71┤⣤⣴" + Glyphs('⣶', 8) + Glyphs('⣿', 56) + Spaces(6)), 80, lines[10]);
                AssertLine(Box("   │" + Glyphs('⣿', 66) + Spaces(6)), 80, lines[11]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 66) + Spaces(6)), 80, lines[12]);
                AssertLine(Bottom(80), 80, lines[13]);
                AssertLine("╭─ Requests " + new string('─', 67) + "╮", 80, lines[14]);
                AssertLine("│         count  rps    min    mean    p50     p75     p95     p99     max" + Spaces(4) + " │", 80, lines[16]);
                AssertLine(Bottom(80), 80, lines[19]);
                AssertLine(string.Empty, 0, lines[20]);
                AssertLine(string.Empty, 0, lines[22]);
                AssertLine("q quit", 6, lines[23]);
                AssertMaximumWidth(80, lines);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain(LatencyChartTitle, line.Text);
                    Assert.DoesNotContain("Steps", line.Text);
                    Assert.DoesNotContain("Errors", line.Text);
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_dashboard_golden_frame_at_60x20()
    {
        // Below 64 columns the tiles wrap: rps, p95 and errors in boxes of 20, 19, 19 (inner
        // 16, 15, 15), then requests and elapsed in boxes of 30 and 29 (inner 26 and 25), wide
        // enough for both units and the gauge — 25 − 9 = 16 cells, 0.45 × 16 = 7.2: seven full
        // cells and a two-eighths cell. The timeline splits 12 / 24 / 24 with the marker on
        // column 12 + floor(75 / 120 × 24) = 27, the "0" of "10" (the ramp's label fits its 24
        // columns exactly, on 13-34); the warmup label goes to the legend. The header block
        // is 13 rows and the requests panel 6, all the 19 rows above the footer: the drop
        // order takes every table and both stacked charts, so no chart shows at 60×20.
        await Scenario()
            .Step("At 60 columns the tiles wrap onto two rows and keep every unit and the gauge, and the drop order leaves the requests panel alone under the header", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 60, 20, ColorMode.None);

                Assert.HasCount(20, lines);
                AssertLine("Checkout flow  ● Running", 24, lines[0]);
                AssertLine(Row(Top(20), Top(19), Top(19)), 60, lines[1]);
                AssertLine(Row(Box("rps" + Spaces(13)), Box("p95" + Spaces(12)), Box("errors" + Spaces(9))), 60, lines[2]);
                AssertLine(Row(Box("142" + Spaces(13)), Box("38 ms" + Spaces(10)), Box("0.7 %" + Spaces(10))), 60, lines[3]);
                AssertLine(Row(Bottom(20), Bottom(19), Bottom(19)), 60, lines[4]);
                AssertLine(Row(Top(30), Top(29)), 60, lines[5]);
                AssertLine(Row(Box("requests" + Spaces(18)), Box("elapsed" + Spaces(18))), 60, lines[6]);
                AssertLine(Row(Box("12512 warmup 1203" + Spaces(9)), Box("00:02:15 / 5m" + Spaces(12))), 60, lines[7]);
                AssertLine(Row(Box(Spaces(26)), Box("▕███████▎········▏ 2m 45s")), 60, lines[8]);
                AssertLine(Row(Bottom(30), Bottom(29)), 60, lines[9]);
                AssertLine(new string('=', 12) + " Gradual Load 1▼→50 rps " + "-- Fixed Load 50 rps ---", 60, lines[10]);
                AssertLine("Fixed Load 20 rps" + Spaces(43), 60, lines[11]);
                AssertLine(string.Empty, 0, lines[12]);
                AssertLine("╭─ Requests " + new string('─', 47) + "╮", 60, lines[13]);
                AssertLine("│ warmup 1200 ok · 3 failed" + Spaces(31) + " │", 60, lines[14]);
                AssertLine(Bottom(60), 60, lines[18]);
                AssertLine("q quit", 6, lines[19]);
                AssertMaximumWidth(60, lines);

                foreach (var line in lines)
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_thresholds_turn_tiles_into_gauges_and_add_tiles()
    {
        // Six boxes of 20, 19, 19, 19, 19, 19 (inner 16, 15 × 5). The rps gauge is a minimum:
        // 16 − 2 − 8 = 6 cells over 0 → 150 / 0.8 = 187.5, 112 / 187.5 × 6 = 3.58 — three full
        // cells and a five-eighths cell — with the band on the last cell; the p95 gauge's
        // 412 / 500 × 6 = 4.94 rounds up to five full cells; the errors gauge has 15 − 6 = 9
        // cells, 0.5 × 9 = 4.5, the band from cell round(7.2) = 7; the elapsed gauge has
        // 15 − 6 = 9 cells, 0.2 × 9 = 1.8; the p99 gauge's 480 / 800 × 6 = 3.6. The deltas
        // read the newest sample against the first: +12 rps and +372 ms. The twelve-sample
        // trends pair the eleven low samples and the high newest into ⣀⣀⣀⣀⣀⣸.
        await Scenario()
            .Step("Color mode None: the p95, errors and rps tiles carry their gauges and a p99 tile is added after elapsed", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { ThresholdSnapshot() }, 120, 40, ColorMode.None);

                AssertLine(Row(Top(20), Top(19), Top(19), Top(19), Top(19), Top(19)), 120, lines[1]);
                AssertLine(Row(Box("rps" + Spaces(9) + "▲ 12"), Box("p95" + Spaces(4) + "▲ 372 ms"), Box("errors" + Spaces(9)), Box("requests" + Spaces(7)), Box("elapsed" + Spaces(8)), Box("p99" + Spaces(12))), 120, lines[2]);
                AssertLine(Row(Box("112" + Spaces(13)), Box("412 ms" + Spaces(9)), Box("0.5 %" + Spaces(10)), Box("1200 warmup 100"), Box("00:00:12 / 1m" + Spaces(2)), Box("480 ms" + Spaces(9))), 120, lines[3]);
                AssertLine(Row(Box("▕███▋·░▏ 150 rps"), Box("▕█████░▏ 500 ms"), Box("▕████▌··░░▏ 1 %"), Box(Spaces(15)), Box("▕█▊·······▏ 48s"), Box("▕███▋·░▏ 800 ms")), 120, lines[4]);
                AssertLine(Row(Box(Spaces(10) + "⣀⣀⣀⣀⣀⣸"), Box(Spaces(9) + "⣀⣀⣀⣀⣀⣸"), Box(Spaces(15)), Box(Spaces(15)), Box(Spaces(15)), Box(Spaces(15))), 120, lines[5]);
                AssertLine(Row(Bottom(20), Bottom(19), Bottom(19), Bottom(19), Bottom(19), Bottom(19)), 120, lines[6]);
                AssertLine(" Fixed Load 100 rps " + "####▼" + new string('-', 35) + " Fixed Load 100 rps " + new string('-', 40), 120, lines[7]);
                AssertLine(string.Empty, 0, lines[8]);
                AssertMaximumWidth(120, lines);
            })
            .Step("TrueColor colours each tile by its threshold's state — breached red, warning yellow, ok green — and the deltas by their direction", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { ThresholdSnapshot() }, 120, 40, ColorMode.TrueColor);

                Assert.Contains(Sgr(Green, "▲ 12"), lines[2].Text);
                Assert.Contains(Sgr(Red, "▲ 372 ms"), lines[2].Text);
                Assert.Contains(Sgr(BoldRed, "112"), lines[3].Text);
                Assert.Contains(Sgr(BoldYellow, "412") + " " + Sgr(Dim, "ms"), lines[3].Text);
                Assert.Contains(Sgr(BoldGreen, "0.5") + " " + Sgr(Dim, "%"), lines[3].Text);
                Assert.Contains(Sgr(BoldGreen, "480") + " " + Sgr(Dim, "ms"), lines[3].Text);
                Assert.Contains(Sgr(Dim, "▕") + Sgr(Red, "███▋") + Sgr(Dim, "·░▏ 150 rps"), lines[4].Text);
            })
            .Step("A single p95 threshold changes only the p95 tile: no tile is added and the others keep their heuristics", context =>
            {
                var snapshot = SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 412), errorRate: 0.005, thresholds: new[]
                {
                    Judged(ThresholdMetric.ResponseTimePercentile95, 500, ThresholdComparison.LessThanOrEqualTo, 412, ThresholdState.Breached)
                });

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 120, 40, ColorMode.TrueColor);

                AssertLine(Row(Top(24), Top(23), Top(23), Top(23), Top(23)), 120, lines[1]);
                Assert.Contains(Sgr(BoldRed, "412") + " " + Sgr(Dim, "ms"), lines[3].Text);
                Assert.Contains(Sgr(Bold, "112"), lines[3].Text);
                Assert.Contains(Sgr(BoldYellow, "0.5") + " " + Sgr(Dim, "%"), lines[3].Text);
                Assert.DoesNotContain("p99", lines[2].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_heuristic_states_without_thresholds()
    {
        // TrueColor at 120 columns; the value line is row 3. A tile's value is bold in its
        // state's colour: green Ok, yellow Warning, red Critical, bare bold for Neutral.
        await Scenario()
            .Step("The error rate is Ok at zero, Warning up to one percent inclusive and Critical above it", context =>
            {
                Assert.Contains(Sgr(BoldGreen, "0.0") + " " + Sgr(Dim, "%"), ValueLine(SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 36), errorRate: 0)));
                Assert.Contains(Sgr(BoldYellow, "0.5") + " " + Sgr(Dim, "%"), ValueLine(SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 36), errorRate: 0.005)));
                Assert.Contains(Sgr(BoldYellow, "1.0") + " " + Sgr(Dim, "%"), ValueLine(SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 36), errorRate: 0.01)));
                Assert.Contains(Sgr(BoldRed, "1.0") + " " + Sgr(Dim, "%"), ValueLine(SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 36), errorRate: 0.0101)));
                Assert.Contains(Sgr(BoldRed, "2.0") + " " + Sgr(Dim, "%"), ValueLine(SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 36), errorRate: 0.02)));
            })
            .Step("An interval without requests has no failed share: the errors tile shows no data, Neutral, never a green 0.0 %", context =>
            {
                // The newest sample's rate of 0 is 0 requests in its interval; the rps tile keeps its true 0.0.
                var line = ValueLine(SteadySnapshot(SteadySeries(100, 0), SteadySeries(40, 36), errorRate: 0));

                Assert.Contains(Box(Sgr(Bold, "—") + Spaces(18)), line);
                Assert.Contains(Box(Sgr(Bold, "0.0") + Spaces(17)), line);
                Assert.DoesNotContain(Sgr(BoldGreen, "0.0"), line);
            })
            .Step("A declared error-rate threshold keeps its state and gauge as reported on such an interval; only the value is no data", context =>
            {
                // The errors box is 23 (inner 19): a 13-cell bar, empty at a Current of 0, with
                // the band from cell round(0.8 × 13) = 10.
                var snapshot = SteadySnapshot(SteadySeries(100, 0), SteadySeries(40, 36), errorRate: 0, thresholds: new[]
                {
                    Judged(ThresholdMetric.ErrorRate, 0.01, ThresholdComparison.LessThanOrEqualTo, 0, ThresholdState.Ok)
                });

                var plain = LiveDashboardLayout.Render(new[] { snapshot }, 120, 0, ColorMode.None);
                Assert.Contains(Box("—" + Spaces(18)), plain[3].Text);
                Assert.Contains(Box("▕··········░░░▏ 1 %"), plain[4].Text);

                Assert.Contains(Box(Sgr(BoldGreen, "—") + Spaces(18)), ValueLine(snapshot));
            })
            .Step("The p95 is Warning above twice the trailing median and Neutral at it, and rps is always Neutral", context =>
            {
                Assert.Contains(Sgr(BoldYellow, "90") + " " + Sgr(Dim, "ms"), ValueLine(SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 90))));
                Assert.Contains(Sgr(Bold, "80") + " " + Sgr(Dim, "ms"), ValueLine(SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 80))));
                Assert.Contains(Sgr(Bold, "112"), ValueLine(SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 90))));
            })
            .Step("The median needs ten latencies: nine leave the spike Neutral, ten make it Warning", context =>
            {
                Assert.Contains(Sgr(Bold, "90") + " " + Sgr(Dim, "ms"), ValueLine(SteadySnapshot(Repeat(100, 9), Repeat(40, 8).Concat(new double[] { 90 }).ToArray())));
                Assert.Contains(Sgr(BoldYellow, "90") + " " + Sgr(Dim, "ms"), ValueLine(SteadySnapshot(Repeat(100, 10), Repeat(40, 9).Concat(new double[] { 90 }).ToArray())));
            })
            .Step("Idle intervals are left out of the median: twelve zeros before nine 40 ms samples do not drag it down", context =>
            {
                var responseTimes = Repeat(0, 12).Concat(Repeat(40, 9)).Concat(new double[] { 50 }).ToArray();

                Assert.Contains(Sgr(Bold, "50") + " " + Sgr(Dim, "ms"), ValueLine(SteadySnapshot(Repeat(100, responseTimes.Length), responseTimes)));
            })
            .Step("No sample yet is Neutral no data on the rps, p95 and errors tiles", context =>
            {
                var line = ValueLine(new LiveMetricsSnapshot { ScenarioName = "Fresh" });

                Assert.Contains(Box(Sgr(Bold, "—") + Spaces(19)), line);
                Assert.Contains(Box(Sgr(Bold, "—") + Spaces(18)), line);
            })
            .Run();
    }

    private static string ValueLine(LiveMetricsSnapshot snapshot)
    {
        return LiveDashboardLayout.Render(new[] { snapshot }, 120, 0, ColorMode.TrueColor)[3].Text;
    }

    private static double[] Repeat(double value, int count)
    {
        var series = new double[count];
        Array.Fill(series, value);
        return series;
    }

    [Test]
    public async Task Verify_trend_and_median_use_only_the_trailing_60_samples()
    {
        await Scenario()
            .Step("The spike heuristic's median is taken over the trailing 60 samples, not the whole series", context =>
            {
                // The trailing 60 are 59 × 100 ms and the 250 ms newest: sorted, the 30th and
                // 31st are both 100, so the median is 100 ms and 250 > 2 × 100 is Warning. Over
                // all 130 the 65th and 66th sorted are both 1000 ms — a median that would leave
                // 250 Neutral, bare bold.
                var lines = LiveDashboardLayout.Render(new[] { LongRunSnapshot() }, 120, 40, ColorMode.TrueColor);

                Assert.Contains(Sgr(BoldYellow, "250") + " " + Sgr(Dim, "ms"), lines[3].Text);
            })
            .Step("The trend holds the trailing 60 samples, newest at the right: a flat run with a single raised glyph at its end", context =>
            {
                // At 120 columns a 19-wide tile windows only 38 samples in braille, so the width
                // that tells the windows apart is one whose boxes hold more than 60: at 179 the
                // five boxes are 35 (inner 31), 62 sample slots. The trend's 60 samples fill the
                // 30 right columns and leave the first blank; 100 ms is the minimum (level 1, ⣀
                // per pair) and 250 the maximum (level 4 in the last column's right half: ⣸).
                // The whole series would put the newest 62 there instead — two at 1000 ms
                // lifting the scale, flattening the 100s to the bottom and drawing a full-height
                // ⣿ at the left. The steady rps trend has no spread: the middle level, ⣤, after
                // its blank column.
                var lines = LiveDashboardLayout.Render(new[] { LongRunSnapshot() }, 179, 40, ColorMode.None);

                AssertLine(Row(Top(35), Top(35), Top(35), Top(35), Top(35)), 179, lines[1]);
                AssertLine(Row(Box("100" + Spaces(28)), Box("250 ms" + Spaces(25)), Box("0.0 %" + Spaces(26)), Box("13000" + Spaces(26)), Box("00:02:10" + Spaces(23))), 179, lines[3]);
                AssertLine(Row(Box(" " + new string('⣤', 30)), Box(" " + new string('⣀', 29) + "⣸"), Box(Spaces(31)), Box(Spaces(31)), Box(Spaces(31))), 179, lines[4]);
                AssertLine(Row(Bottom(35), Bottom(35), Bottom(35), Bottom(35), Bottom(35)), 179, lines[5]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_init_placeholder_frame_shows_no_data_over_empty_gauges()
    {
        // The console manager's first frame: six boxes of 20, 19, 19, 19, 19, 19 (inner 16,
        // 15 × 5) with no sample, no series and no plan. Every declared threshold is in its
        // placeholder state — Ok, Current 0 — so its gauge is an empty track with the band
        // marked: the rps, p95 and p99 limits ("150 rps", "500 ms", "800 ms") leave 6 cells
        // with the band on the last, round(0.8 × 6) = 5, and "1 %" leaves 9 with the band from
        // cell round(7.2) = 7. The trends are blank rows and the elapsed tile has no gauge.
        // Without a plan there is no timeline, so the init phase follows the badge on the
        // title line; without a series the charts are blank bodies behind tick-only axes (no
        // finite value, no labels, no annotation) under titles that still carry the declared
        // limits, and the heatmap is its labels over a blank body.
        await Scenario()
            .Step("Color mode None: the rps, p95, errors and p99 tiles show no data over empty gauges, no timeline row follows the tiles, and the phase sits on the title line", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { InitSnapshot() }, 120, 40, ColorMode.None);

                Assert.HasCount(40, lines);
                AssertLine("Checkout flow  ● Running · init" + Spaces(76) + "  ⚡ TestFuzn", 120, lines[0]);
                AssertLine(Row(Top(20), Top(19), Top(19), Top(19), Top(19), Top(19)), 120, lines[1]);
                AssertLine(Row(Box("rps" + Spaces(13)), Box("p95" + Spaces(12)), Box("errors" + Spaces(9)), Box("requests" + Spaces(7)), Box("elapsed" + Spaces(8)), Box("p99" + Spaces(12))), 120, lines[2]);
                AssertLine(Row(Box("—" + Spaces(15)), Box("—" + Spaces(14)), Box("—" + Spaces(14)), Box("0" + Spaces(14)), Box("00:00:02" + Spaces(7)), Box("—" + Spaces(14))), 120, lines[3]);
                AssertLine(Row(Box("▕·····░▏ 150 rps"), Box("▕·····░▏ 500 ms"), Box("▕·······░░▏ 1 %"), Box(Spaces(15)), Box(Spaces(15)), Box("▕·····░▏ 800 ms")), 120, lines[4]);
                AssertLine(Row(Box(Spaces(16)), Box(Spaces(15)), Box(Spaces(15)), Box(Spaces(15)), Box(Spaces(15)), Box(Spaces(15))), 120, lines[5]);
                AssertLine(Row(Bottom(20), Bottom(19), Bottom(19), Bottom(19), Bottom(19), Bottom(19)), 120, lines[6]);
                AssertLine(string.Empty, 0, lines[7]);
                AssertLine(PanelTop(RequestsChartTitle, 59) + "  " + PanelTop(LatencyChartTitle + " · limits 500 ms / 800 ms", 59), 120, lines[8]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("┤" + Spaces(54)), 120, lines[9]);
                AssertLine(Box("│" + Spaces(54)) + "  " + Box("│" + Spaces(54)), 120, lines[10]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("┤" + Spaces(54)), 120, lines[11]);
                AssertLine(Box("│" + Spaces(54)) + "  " + Box("│" + Spaces(54)), 120, lines[13]);
                AssertLine(Box("┤" + Spaces(54)) + "  " + Box("┤" + Spaces(54)), 120, lines[14]);
                AssertLine(Bottom(59) + "  " + Bottom(59), 120, lines[15]);
                AssertLine(PanelTop(HeatmapTitle, 120), 120, lines[16]);
                AssertLine(Box("  > 30 s " + Spaces(107)), 120, lines[17]);
                AssertLine(Box("  ≤ 2 ms " + Spaces(107)), 120, lines[24]);
                AssertLine(Bottom(120), 120, lines[25]);
                AssertLine(PanelTop("Requests", 120), 120, lines[26]);
                AssertLine(Bottom(120), 120, lines[30]);
                AssertLine(string.Empty, 0, lines[31]);
                AssertLine("q quit", 6, lines[39]);
                AssertMaximumWidth(120, lines);
            })
            .Step("TrueColor: the same frame, every no-data value in the placeholder's Ok green, the request count bare bold and the phase in its accent", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { InitSnapshot() }, 120, 40, ColorMode.TrueColor);

                Assert.HasCount(40, lines);
                Assert.StartsWith(Sgr(Bold, "Checkout flow") + "  " + Sgr(Yellow, "● Running") + " " + Sgr(Dim, "·") + " " + Sgr("38;2;255;207;107", "init"), lines[0].Text);
                Assert.Contains(Box(Sgr(BoldGreen, "—") + Spaces(15)), lines[3].Text);
                Assert.Contains(Box(Sgr(BoldGreen, "—") + Spaces(14)) + " " + Box(Sgr(BoldGreen, "—") + Spaces(14)), lines[3].Text);
                Assert.Contains(Box(Sgr(Bold, "0") + Spaces(14)), lines[3].Text);
                AssertLine(string.Empty, 0, lines[7]);
                Assert.Contains(Sgr(Dim, "· limits") + " " + Sgr(Yellow, "500 ms") + " " + Sgr(Dim, "/") + " " + Sgr(Red, "800 ms"), lines[8].Text);
                Assert.AreEqual(6, lines[39].Width);
                AssertMaximumWidth(120, lines);
            })
            .Run();
    }

    [Test]
    public async Task Verify_idle_interval_p95_is_no_data_while_zero_rate_is_a_reading()
    {
        // Twelve samples at 120 columns: the p95 tile (inner 19) and the rps tile (inner 20).
        // Eleven equal samples render at the middle level (two braille dots) and a gap is
        // blank, so ⣤⣤⣤⣤⣤ ends in ⡄ when the newest sample is the gap and starts with it
        // when the second is; a zero rate is the lowest level under eleven at the top: ⣿⣿⣿⣿⣿⣇.
        await Scenario()
            .Step("A zero newest p95 shows no data with no unit, hides the delta and leaves a gap at the end of the trend", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 0)) }, 120, 0, ColorMode.None);

                Assert.Contains(Box("p95" + Spaces(16)), lines[2].Text);
                Assert.Contains(Box("—" + Spaces(18)), lines[3].Text);
                Assert.Contains(Box(Spaces(13) + "⣤⣤⣤⣤⣤⡄"), lines[4].Text);
            })
            .Step("A zero p95 at the delta's other end hides the delta too, and leaves its gap in the trend", context =>
            {
                var responseTimes = SteadySeries(40, 40);
                responseTimes[1] = 0;

                var lines = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 112), responseTimes) }, 120, 0, ColorMode.None);

                Assert.Contains(Box("p95" + Spaces(16)), lines[2].Text);
                Assert.Contains(Box("40 ms" + Spaces(14)), lines[3].Text);
                Assert.Contains(Box(Spaces(13) + "⡄⣤⣤⣤⣤⣤"), lines[4].Text);
            })
            .Step("A zero newest rate is a true reading: 0.0 with a red drop of 100 and the trend dipping to the bottom", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 0), SteadySeries(40, 40)) }, 120, 0, ColorMode.None);

                Assert.Contains(Box("rps" + Spaces(12) + "▼ 100"), lines[2].Text);
                Assert.Contains(Box("0.0" + Spaces(17)), lines[3].Text);
                Assert.Contains(Box(Spaces(14) + "⣿⣿⣿⣿⣿⣇"), lines[4].Text);

                var styled = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 0), SteadySeries(40, 40)) }, 120, 0, ColorMode.TrueColor);
                Assert.Contains(Sgr(Red, "▼ 100"), styled[2].Text);
            })
            .Step("A p95 drop reads as a green fall and a rise as a red climb, in whole milliseconds", context =>
            {
                var falling = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 36)) }, 120, 0, ColorMode.TrueColor);
                Assert.Contains(Sgr(Dim, "p95") + Spaces(10) + Sgr(Green, "▼ 4 ms"), falling[2].Text);

                var rising = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 112), SteadySeries(40, 45.4)) }, 120, 0, ColorMode.TrueColor);
                Assert.Contains(Sgr(Dim, "p95") + Spaces(10) + Sgr(Red, "▲ 5 ms"), rising[2].Text);
            })
            .Step("A delta needs a sample ten back: ten samples show none, eleven show it", context =>
            {
                var ten = LiveDashboardLayout.Render(new[] { SteadySnapshot(Repeat(100, 9).Concat(new double[] { 112 }).ToArray(), Repeat(40, 10)) }, 120, 0, ColorMode.None);
                Assert.Contains(Box("rps" + Spaces(17)), ten[2].Text);

                var eleven = LiveDashboardLayout.Render(new[] { SteadySnapshot(Repeat(100, 10).Concat(new double[] { 112 }).ToArray(), Repeat(40, 11)) }, 120, 0, ColorMode.None);
                Assert.Contains(Box("rps" + Spaces(13) + "▲ 12"), eleven[2].Text);
            })
            .Step("A change that rounds to zero in the tile's unit shows no delta, while the first step up does", context =>
            {
                // +0.04 rps rounds to 0.0 and +0.4 ms to 0 ms — the numbers the tiles would show —
                // so a steady run carries no permanent ▲ 0; +0.1 rps and +0.6 ms are a step each.
                var steady = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 100.04), SteadySeries(40, 40.4)) }, 120, 0, ColorMode.None);
                Assert.Contains(Box("rps" + Spaces(17)), steady[2].Text);
                Assert.Contains(Box("p95" + Spaces(16)), steady[2].Text);

                var stepped = LiveDashboardLayout.Render(new[] { SteadySnapshot(SteadySeries(100, 100.1), SteadySeries(40, 40.6)) }, 120, 0, ColorMode.None);
                Assert.Contains(Box("rps" + Spaces(12) + "▲ 0.1"), stepped[2].Text);
                Assert.Contains(Box("p95" + Spaces(10) + "▲ 1 ms"), stepped[2].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_tile_width_rules()
    {
        await Scenario()
            .Step("From 80 columns the tiles carry trends (boxes of 16, 15, 15, 15, 15); at 79 they do not (five boxes of 15)", context =>
            {
                var withTrends = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 80, 40, ColorMode.None);
                AssertLine(Row(Box(Spaces(7) + "⣀⣤⣶⣿⣿"), Box(Spaces(6) + "⣿⣦⣤⣀⣀"), Box(Spaces(11)), Box(Spaces(11)), Box(Spaces(11))), 80, withTrends[4]);
                AssertLine(Row(Bottom(16), Bottom(15), Bottom(15), Bottom(15), Bottom(15)), 80, withTrends[5]);

                var without = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 79, 40, ColorMode.None);
                AssertLine(Row(Bottom(15), Bottom(15), Bottom(15), Bottom(15), Bottom(15)), 79, without[4]);
            })
            .Step("At 64 columns five boxes of 12 hold every label and value on one row", context =>
            {
                Assert.AreEqual(64, LiveDashboardLayout.MinimumWidthForSingleTileRow(5));

                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 64, 40, ColorMode.None);

                AssertLine(Row(Box("rps     "), Box("p95     "), Box("errors  "), Box("requests"), Box("elapsed ")), 64, lines[2]);
                AssertLine(Row(Box("142     "), Box("38 ms   "), Box("0.7 %   "), Box("12512   "), Box("00:02:15")), 64, lines[3]);
                AssertLine(Row(Bottom(12), Bottom(12), Bottom(12), Bottom(12), Bottom(12)), 64, lines[4]);
            })
            .Step("At 63 columns the tiles wrap: three boxes of 21, 20, 20, then two of 31 with room for the units and an 18-cell gauge", context =>
            {
                // 0.45 × 18 = 8.1: eight full cells, the remainder below two eighths drawing nothing.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 63, 40, ColorMode.None);

                AssertLine(Row(Box("rps" + Spaces(14)), Box("p95" + Spaces(13)), Box("errors" + Spaces(10))), 63, lines[2]);
                AssertLine(Row(Bottom(21), Bottom(20), Bottom(20)), 63, lines[4]);
                AssertLine(Row(Top(31), Top(31)), 63, lines[5]);
                AssertLine(Row(Box("requests" + Spaces(19)), Box("elapsed" + Spaces(20))), 63, lines[6]);
                AssertLine(Row(Box("12512 warmup 1203" + Spaces(10)), Box("00:02:15 / 5m" + Spaces(14))), 63, lines[7]);
                AssertLine(Row(Box(Spaces(27)), Box("▕████████··········▏ 2m 45s")), 63, lines[8]);
                AssertLine(Row(Bottom(31), Bottom(31)), 63, lines[9]);
            })
            .Step("Every standard tile is on screen at every width from 64 up", context =>
            {
                for (var width = 64; width <= 220; width++)
                {
                    var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, width, 0, ColorMode.None);
                    foreach (var label in new[] { "│ rps ", "│ p95 ", "│ errors ", "│ requests ", "│ elapsed " })
                        Assert.Contains(label, lines[2].Text, $"Missing {label.Trim()} at width {width}");
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_tile_height_rules()
    {
        await Scenario()
            .Step("At 30 rows the tiles carry trends; at 29 they do not, while the elapsed gauge still keeps the row three lines tall", context =>
            {
                var withTrends = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 100, 30, ColorMode.None);
                Assert.Contains("⣀⣤⣶⣿⣿", withTrends[4].Text);

                var without = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 100, 29, ColorMode.None);
                AssertLine(Row(Box(Spaces(16)), Box(Spaces(15)), Box(Spaces(15)), Box(Spaces(15)), Box("▕██▊···▏ 2m 45s")), 100, without[4]);
                AssertLine(Row(Bottom(20), Bottom(19), Bottom(19), Bottom(19), Bottom(19)), 100, without[5]);
            })
            .Step("At 20 rows the gauges stay; at 19 they go and every tile is two lines", context =>
            {
                var withGauges = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 20, ColorMode.None);
                AssertLine(Row(Box(Spaces(20)), Box(Spaces(19)), Box(Spaces(19)), Box(Spaces(19)), Box("▕████▌·····▏ 2m 45s")), 120, withGauges[4]);
                Assert.StartsWith("== Fixed Load 20 rps ===", withGauges[6].Text);

                var without = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 19, ColorMode.None);
                AssertLine(Row(Bottom(24), Bottom(23), Bottom(23), Bottom(23), Bottom(23)), 120, without[4]);
                Assert.StartsWith("== Fixed Load 20 rps ===", without[5].Text);
            })
            .Step("At 15 rows the timeline stays; at 14 it goes, the phase moves onto the title line and the separator follows the tiles", context =>
            {
                var withTimeline = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 15, ColorMode.None);
                Assert.StartsWith("== Fixed Load 20 rps ===", withTimeline[5].Text);
                AssertLine(string.Empty, 0, withTimeline[6]);
                Assert.DoesNotContain("sim 1/2", withTimeline[0].Text);

                var without = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 14, ColorMode.None);
                AssertLine("Checkout flow  ● Running · sim 1/2: Gradual Load 10→50 rps" + Spaces(49) + "  ⚡ TestFuzn", 120, without[0]);
                AssertLine(string.Empty, 0, without[5]);
                Assert.StartsWith("╭─ Requests ", without[6].Text);
                foreach (var line in without)
                    Assert.DoesNotContain("Fixed Load 20 rps", line.Text);
            })
            .Step("A threshold gauge goes with the height too, its state colour staying", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { ThresholdSnapshot() }, 120, 19, ColorMode.TrueColor);

                Assert.DoesNotContain("▕", lines[3].Text);
                Assert.StartsWith(Bottom(20) + " " + Bottom(19), lines[4].Text);
                Assert.Contains(Sgr(BoldRed, "112"), lines[3].Text);
            })
            .Step("An unbounded height keeps every optional row", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 0, ColorMode.None);

                Assert.Contains("⣀⣤⣶⣿⣿", lines[4].Text);
                Assert.Contains("▕████▌·····▏ 2m 45s", lines[4].Text);
                Assert.StartsWith("== Fixed Load 20 rps ===", lines[6].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_requests_table_narrows_columns_by_content_width()
    {
        await Scenario()
            .Step("Wide enough for the full spread, every hour-class value renders whole", context =>
            {
                // An unbounded height keeps every panel: the title, five tile rows, a blank,
                // eight chart rows and ten heatmap rows put the requests panel's top border
                // on row 25.
                var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, 120, 0, ColorMode.None);

                AssertLine("│             count      rps         min        mean         p50         p75         p95         p99         max       │", 120, lines[26]);
                AssertLine("│ ok      987654321  9999999  3600000 ms  7200000 ms  5400000 ms  6000000 ms  7200000 ms  8000000 ms  9000000 ms       │", 120, lines[27]);
            })
            .Step("At 78 columns min, p75 and p99 are dropped so the remaining numbers fit exactly", context =>
            {
                // Below 80 columns the tiles carry no trends (four rows) and the charts stack:
                // the title, four tile rows, a blank, two eight-row chart panels and the
                // heatmap put the requests panel's top border on row 32.
                var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, 78, 0, ColorMode.None);

                AssertLine("│             count      rps        mean         p50         p95         max │", 78, lines[33]);
                AssertLine("│ ok      987654321  9999999  7200000 ms  5400000 ms  7200000 ms  9000000 ms │", 78, lines[34]);
                AssertLine("│ failed  123456789      1.0   500000 ms   400000 ms   800000 ms   950000 ms │", 78, lines[35]);
            })
            .Step("One column narrower, p50 and max go too rather than any number truncating", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, 77, 0, ColorMode.None);

                Assert.Contains("p95", lines[33].Text);
                Assert.DoesNotContain("p50", lines[33].Text);
                Assert.DoesNotContain("max", lines[33].Text);
                Assert.Contains("987654321  9999999  7200000 ms  7200000 ms", lines[34].Text);
            })
            .Step("At 56 columns count, rps, mean and p95 remain, all whole", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, 56, 0, ColorMode.None);

                AssertLine("│             count      rps        mean         p95   │", 56, lines[11]);
                AssertLine("│ ok      987654321  9999999  7200000 ms  7200000 ms   │", 56, lines[12]);
                AssertLine("│ failed  123456789      1.0   500000 ms   800000 ms   │", 56, lines[13]);
            })
            .Step("From 56 columns up no numeric cell of the requests rows is ever truncated", context =>
            {
                for (var width = 56; width <= 130; width++)
                {
                    var lines = LiveDashboardLayout.Render(new[] { HugeSnapshot() }, width, 0, ColorMode.None);
                    var requestsRowCount = 0;
                    foreach (var line in lines)
                    {
                        if (!line.Text.StartsWith("│ ok ", StringComparison.Ordinal) && !line.Text.StartsWith("│ failed ", StringComparison.Ordinal))
                            continue;

                        requestsRowCount++;
                        Assert.DoesNotContain("…", line.Text, $"Truncated requests row at width {width}: {line.Text}");
                    }

                    Assert.AreEqual(2, requestsRowCount, $"Expected the ok and failed rows at width {width}");
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_live_rates_share_one_measure_and_the_requests_chart_annotates_the_newest_ok_count()
    {
        await Scenario()
            .Step("The rps tile, the requests rows and the step row share the current interval's rate, while the requests chart annotates the newest ok count — 45 of the interval's 50 rps", context =>
            {
                // No plan, so the phase follows the badge; the title, five tile rows and a
                // blank put the chart panels on rows 7-14: the newest ok count, 45, tops the
                // 0..45 scale, so its annotation sits on the first body row — a count of ok
                // requests, not the tile's 50 rps (45 ok and 5 failed over the second). The
                // requests panel (no warmup line) is rows 25-29 and the step table 30-33.
                var lines = LiveDashboardLayout.Render(new[] { RampSnapshot() }, 100, 0, ColorMode.None);

                AssertLine("Ramp  ● Running · Gradual Load 10→50 rps" + Spaces(47) + "  ⚡ TestFuzn", 100, lines[0]);
                Assert.Contains(Box("50" + Spaces(14)), lines[3].Text);
                AssertLine(PanelTop(RequestsChartTitle, 49) + "  " + PanelTop(LatencyChartTitle, 49), 100, lines[7]);
                Assert.StartsWith(Box("45┤" + Spaces(28) + "⣀⣀⣠⣤⣤⣶⣶⣾⣿ ▶ 45"), lines[8].Text);
                AssertLine("│ ok       1800   45  10 ms  20 ms  20 ms  25 ms  40 ms  60 ms  90 ms" + Spaces(29) + " │", 100, lines[27]);
                AssertLine("│ failed    180  5.0  15 ms  45 ms  45 ms  55 ms  70 ms  80 ms  95 ms" + Spaces(29) + " │", 100, lines[28]);
                AssertLine("│ Add to cart   1980   50  20 ms  40 ms     180  █░░░░ 9.1%" + Spaces(39) + " │", 100, lines[32]);
            })
            .Step("An interval without requests shows a zero rate, and no sample yet shows no data", context =>
            {
                // A single zero sample has no spread: the area fills the lower half of the
                // chart at the middle level, every axis label reads 0, and the annotation
                // sits on the middle level's row, the fourth. No sample draws nothing at all.
                var idle = new LiveMetricsSnapshot { ScenarioName = "Idle", Samples = new[] { Sample(1, 0, 0, 0) }, RequestsPerSecondSeries = new double[] { 0 }, OkDeltaSeries = new double[] { 0 }, FailedDeltaSeries = new double[] { 0 } };
                var fresh = new LiveMetricsSnapshot { ScenarioName = "Fresh" };

                var idleLines = LiveDashboardLayout.Render(new[] { idle }, 100, 0, ColorMode.None);
                var freshLines = LiveDashboardLayout.Render(new[] { fresh }, 100, 0, ColorMode.None);

                Assert.Contains(Box("0.0" + Spaces(13)), idleLines[3].Text);
                AssertLine(Box(" │" + Glyphs('⣿', 39) + " ▶ 0") + "  " + Box("│" + Spaces(44)), 100, idleLines[11]);
                AssertLine(Box("0┤" + Glyphs('⣿', 39) + Spaces(4)) + "  " + Box("┤" + Spaces(44)), 100, idleLines[13]);
                AssertLine("│ ok          0  0.0  N/A   N/A  N/A  N/A  N/A  N/A  N/A" + Spaces(42) + " │", 100, idleLines[27]);
                Assert.Contains(Box("—" + Spaces(15)), freshLines[3].Text);
                AssertLine(Box("│" + Spaces(44)) + "  " + Box("│" + Spaces(44)), 100, freshLines[11]);
                AssertLine("│ ok          0    —  N/A   N/A  N/A  N/A  N/A  N/A  N/A" + Spaces(42) + " │", 100, freshLines[27]);
                for (var row = 8; row <= 13; row++)
                    Assert.DoesNotContain("▶", freshLines[row].Text, $"Annotation on row {row}");
            })
            .Run();
    }

    [Test]
    public async Task Verify_multi_scenario_sections_stack_in_order()
    {
        await Scenario()
            .Step("Sections stack in snapshot order with a blank separator and one logo, each fitted into the rows the ones before it left", context =>
            {
                // At 100×60 the first section takes all its 41 rows (title, five tile rows,
                // the timeline, a blank, eight chart rows, ten heatmap rows, six requests
                // rows, five step rows, four error rows), then the separator. The second
                // gets the 17 rows left: its 32 (title, five tile rows, a two-line timeline,
                // a blank, the charts, the heatmap, five requests rows) shed the heatmap and
                // then the charts, so it is 14 rows — the timeline still there, so its phase
                // stays off the title line — and three blank rows pad down to the footer.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, 100, 60, ColorMode.None);

                Assert.HasCount(60, lines);
                AssertLine(Bottom(100), 100, lines[40]);
                AssertLine(string.Empty, 0, lines[41]);
                AssertLine("Browse catalog  ● Running", 25, lines[42]);
                Assert.EndsWith("▒▒▒▒▒▒▒▒  warmup: Fixed Load 50 rps", lines[48].Text);
                AssertLine(string.Empty, 0, lines[50]);
                AssertLine(PanelTop("Requests", 100), 100, lines[51]);
                AssertLine(Bottom(100), 100, lines[55]);
                AssertLine(string.Empty, 0, lines[56]);
                AssertLine("q quit", 6, lines[59]);
                AssertMaximumWidth(100, lines);

                var logoLineCount = 0;
                var chartTitleCount = 0;
                foreach (var line in lines)
                {
                    if (line.Text.Contains("⚡"))
                        logoLineCount++;

                    if (line.Text.Contains(RequestsChartTitle))
                        chartTitleCount++;
                }

                Assert.AreEqual(1, logoLineCount);
                Assert.AreEqual(1, chartTitleCount);
            })
            .Step("A frame taller than the window keeps the footer on the last row and cuts the content", context =>
            {
                // The first section is fitted to the 29 rows above the footer (its two step
                // rows go), which leaves the second nothing: the separator and the second
                // title are cut.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), IndeterminateSnapshot() }, 120, 30, ColorMode.None);

                Assert.HasCount(30, lines);
                AssertLine(Box(StepsHeaderOnly + Spaces(74)), 120, lines[23]);
                AssertLine("│  2× Add to cart · Timeout after 30s" + Spaces(81) + " │", 120, lines[27]);
                AssertLine(Bottom(120), 120, lines[28]);
                AssertLine("q quit", 6, lines[29]);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain("Browse catalog", line.Text);
                    Assert.DoesNotContain("warmup: Fixed Load 50 rps", line.Text);
                }
            })
            .Step("A one-row window is just the footer", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 1, ColorMode.None);

                Assert.HasCount(1, lines);
                AssertLine("q quit", 6, lines[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_indeterminate_plan_and_status_detail()
    {
        await Scenario()
            .Step("An indeterminate plan renders the hatched segment with the phase label after the bar, and the elapsed tile alone", context =>
            {
                // The label takes 25 columns and the gap 2, leaving a 73-column bar: the
                // hatched segment its fixed 8 and the 10 s warmup the other 65, its label
                // centred with 23 columns each side. The hatched segment's label never fits
                // eight columns: it goes to the legend under column 65, cut at the bar's edge.
                var lines = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, 100, 0, ColorMode.None);

                AssertLine(Row(Box("—" + Spaces(15)), Box("—" + Spaces(14)), Box("—" + Spaces(14)), Box("0" + Spaces(14)), Box("00:00:42" + Spaces(7))), 100, lines[3]);
                AssertLine(Row(Box(Spaces(16)), Box(Spaces(15)), Box(Spaces(15)), Box(Spaces(15)), Box(Spaces(15))), 100, lines[4]);
                AssertLine(new string('-', 23) + " Fixed Load 50 rps " + new string('-', 23) + "▒▒▒▒▒▒▒▒" + "  warmup: Fixed Load 50 rps", 100, lines[6]);
                AssertLine(Spaces(65) + "One Tim…", 73, lines[7]);
                AssertLine(string.Empty, 0, lines[8]);
                AssertLine(PanelTop(RequestsChartTitle, 49) + "  " + PanelTop(LatencyChartTitle, 49), 100, lines[9]);
                Assert.DoesNotContain("▼", lines[6].Text);
                Assert.DoesNotContain("#", lines[6].Text);
                Assert.DoesNotContain("warmup", lines[0].Text);
            })
            .Step("Measurement-segment progress shows the gauge and the time remaining without a planned total", context =>
            {
                // 15 − 9 = 6 cells, 0.3 × 6 = 1.8: one full cell and a six-eighths cell.
                var snapshot = new LiveMetricsSnapshot
                {
                    ScenarioName = "Browse catalog",
                    PhaseLabel = "sim 1/1: Fixed 50 rps",
                    Duration = TimeSpan.FromSeconds(42),
                    ProgressFraction = 0.3,
                    EstimatedTimeRemaining = TimeSpan.FromSeconds(84)
                };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                Assert.Contains(Box("00:00:42" + Spaces(7)), lines[3].Text);
                Assert.Contains(Box("▕█▊····▏ 1m 24s"), lines[4].Text);
                AssertLine(string.Empty, 0, lines[6]);
            })
            .Step("A failed scenario shows its assert reason as a styled line under the header", context =>
            {
                // An unbounded height keeps the empty charts and heatmap: the title, five
                // tile rows, the reason, a blank, eight chart rows, ten heatmap rows and five
                // requests rows, then the footer.
                var lines = LiveDashboardLayout.Render(new[] { FailedSnapshot() }, 100, 0, ColorMode.None);

                Assert.HasCount(32, lines);
                AssertLine("Checkout flow  ● Failed · completed" + Spaces(52) + "  ⚡ TestFuzn", 100, lines[0]);
                Assert.Contains(Box("00:05:00" + Spaces(7)), lines[3].Text);
                AssertLine("✗ Assert.IsLessThan failed. p95 too high: 240 ms", 48, lines[6]);
                AssertLine("q quit", 6, lines[31]);
            })
            .Step("The assert reason line renders red in TrueColor", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { FailedSnapshot() }, 100, 0, ColorMode.TrueColor);

                AssertLine("\u001b[38;5;9m✗ Assert.IsLessThan failed. p95 too high: 240 ms\u001b[0m", 48, lines[6]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_elapsed_tile_and_timeline_drop_pieces_whole()
    {
        await Scenario()
            .Step("At 89 columns (five boxes of 17) the planned total and the four-cell gauge with the time remaining fit; at 88 the elapsed box is a column short of both", context =>
            {
                // 0.45 × 4 = 1.8: one full cell and a six-eighths cell.
                var fitting = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 89, 0, ColorMode.None);
                Assert.EndsWith(Box("00:02:15 / 5m"), fitting[3].Text);
                Assert.EndsWith(Box("▕█▊··▏ 2m 45s"), fitting[4].Text);

                var short_ = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 88, 0, ColorMode.None);
                Assert.EndsWith(Box("00:02:15" + Spaces(4)), short_[3].Text);
                Assert.EndsWith(Box(Spaces(12)), short_[4].Text);
            })
            .Step("The warmup count fits the requests box from 108 columns (a 21-wide fourth box) and goes at 107", context =>
            {
                Assert.Contains("12512 warmup 1203", LiveDashboardLayout.Render(new[] { RichSnapshot() }, 108, 0, ColorMode.None)[3].Text);

                var narrower = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 107, 0, ColorMode.None)[3].Text;
                Assert.Contains("12512", narrower);
                Assert.DoesNotContain("warmup", narrower);
            })
            .Step("The phase label follows the timeline while the bar keeps 24 columns and is dropped one column narrower", context =>
            {
                // At 51 columns the bar is 24: the hatched eight and 16 for the warmup, whose
                // label goes to the legend with the one-time label two columns after it, cut.
                var fitting = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, 51, 0, ColorMode.None);
                AssertLine(new string('-', 16) + "▒▒▒▒▒▒▒▒" + "  warmup: Fixed Load 50 rps", 51, fitting[9]);
                AssertLine("Fixed Load 50 rps  One …", 24, fitting[10]);

                var dropped = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, 50, 0, ColorMode.None);
                AssertLine(new string('-', 11) + " Fixed Load 50 rps " + new string('-', 12) + "▒▒▒▒▒▒▒▒", 50, dropped[9]);
                AssertLine(Spaces(42) + "One Tim…", 50, dropped[10]);
            })
            .Step("A determinate plan shows no phase label: the marker's segment is the phase", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 220, 0, ColorMode.None);

                Assert.AreEqual(220, lines[6].Width);
                Assert.DoesNotContain("sim 1/2", lines[6].Text);
            })
            .Step("Negative durations display as zero instead of negative clock fields", context =>
            {
                var snapshot = new LiveMetricsSnapshot
                {
                    ScenarioName = "Skewed",
                    PhaseLabel = "p",
                    Duration = TimeSpan.FromSeconds(-95),
                    PlannedDuration = TimeSpan.MinValue,
                    EstimatedTimeRemaining = TimeSpan.FromHours(-3)
                };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                Assert.Contains(Box("00:00:00 / 0s" + Spaces(2)), lines[3].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_no_data_and_out_of_range_values_render_safely()
    {
        await Scenario()
            .Step("A p95 series value no TimeSpan can hold shows no data on the tile and is a gap in the latency chart instead of throwing", context =>
            {
                // A lone gap leaves the chart without a finite value: tick-only axis rows,
                // no annotation. The chart panels are rows 7-14 (title, five tile rows, a
                // blank), the latency chart the right one.
                var justOutOfRange = Math.BitIncrement(TimeSpan.MaxValue.TotalMilliseconds);
                foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, justOutOfRange, -justOutOfRange, 1e15 })
                {
                    var snapshot = new LiveMetricsSnapshot { ScenarioName = "p95", ResponseTimePercentile95Series = new[] { value } };

                    var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                    Assert.Contains(Box("—" + Spaces(14)), lines[3].Text, $"Unexpected p95 tile for {value}");
                    Assert.DoesNotContain(" ms", lines[3].Text, $"Unexpected p95 number for {value}");
                    Assert.EndsWith(Box("┤" + Spaces(44)), lines[8].Text, $"Unexpected latency axis for {value}");
                    for (var row = 8; row <= 13; row++)
                        Assert.DoesNotContain("▶", lines[row].Text, $"Unexpected annotation for {value} on row {row}");
                }
            })
            .Step("A representable p95 series value, up to the largest TimeSpan, renders through the shared formatter on the tile and the latency axis", context =>
            {
                // A single latency has no spread: the max and min labels both read it, the
                // area fills the lower half, and without a p99 series there is no annotation.
                var hours = new LiveMetricsSnapshot { ScenarioName = "p95", ResponseTimePercentile95Series = new double[] { 7200000 } };
                var largest = new LiveMetricsSnapshot { ScenarioName = "p95", ResponseTimePercentile95Series = new[] { TimeSpan.MaxValue.TotalMilliseconds } };
                var negative = new LiveMetricsSnapshot { ScenarioName = "p95", ResponseTimePercentile95Series = new double[] { -5 } };

                var hoursLines = LiveDashboardLayout.Render(new[] { hours }, 100, 0, ColorMode.None);
                Assert.Contains(Box("7200000 ms" + Spaces(5)), hoursLines[3].Text);
                Assert.EndsWith(Box("7200000 ms┤" + Spaces(34)), hoursLines[8].Text);
                Assert.EndsWith(Box("          │" + Glyphs('⣿', 34)), hoursLines[11].Text);
                Assert.EndsWith(Box("7200000 ms┤" + Glyphs('⣿', 34)), hoursLines[13].Text);

                var largestLines = LiveDashboardLayout.Render(new[] { largest }, 100, 0, ColorMode.None);
                Assert.Contains("922337203685477 ms", largestLines[3].Text);
                Assert.EndsWith(Box("922337203685477 ms┤" + Spaces(26)), largestLines[8].Text);

                var negativeLines = LiveDashboardLayout.Render(new[] { negative }, 100, 0, ColorMode.None);
                Assert.Contains(Box("—" + Spaces(14)), negativeLines[3].Text);
                Assert.EndsWith(Box("┤" + Spaces(44)), negativeLines[8].Text);
            })
            .Step("A rate that is not finite shows no data on the tile and in the step table alike, and an empty rps chart draws nothing", context =>
            {
                var snapshot = new LiveMetricsSnapshot
                {
                    ScenarioName = "S",
                    RequestsPerSecondSeries = new[] { double.NaN },
                    Steps = new[]
                    {
                        new LiveStepMetrics { Name = "S", RequestCountOk = 5, RequestsPerSecond = double.PositiveInfinity, ResponseTimeMean = TimeSpan.FromMilliseconds(1), ResponseTimePercentile95 = TimeSpan.FromMilliseconds(2) }
                    }
                };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                Assert.Contains(Box("—" + Spaces(15)), lines[3].Text);
                Assert.StartsWith(Box("┤" + Spaces(44)), lines[8].Text);
                AssertLine("│ S         5    —  1 ms  2 ms       0  ░░░░░ 0%" + Spaces(50) + " │", 100, lines[32]);
            })
            .Step("Step counts that do not add up render a clamped bar at every width instead of throwing", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { StepSnapshot(-1, 5) }, 100, 0, ColorMode.None);
                Assert.Contains("█████ 100%", lines[32].Text);

                foreach (var snapshot in new[] { StepSnapshot(-1, 5), StepSnapshot(5, -1), StepSnapshot(-5, 5), StepSnapshot(int.MaxValue, int.MaxValue) })
                {
                    for (var width = 1; width <= 130; width++)
                        AssertMaximumWidth(width, LiveDashboardLayout.Render(new[] { snapshot }, width, 0, ColorMode.None));
                }
            })
            .Run();
    }

    [Test]
    public async Task Verify_step_counts_and_percentages_stay_honest()
    {
        await Scenario()
            .Step("Two counts of two billion sum without wrapping in the step table and the warmup line, and stay off the requests tile's unit line", context =>
            {
                var snapshot = new LiveMetricsSnapshot
                {
                    ScenarioName = "S",
                    WarmupRequestCountOk = 2000000000,
                    WarmupRequestCountFailed = 2000000000,
                    Steps = new[] { new LiveStepMetrics { Name = "S", RequestCountOk = 2000000000, RequestCountFailed = 2000000000 } }
                };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 100, 0, ColorMode.None);

                Assert.Contains(Box("0" + Spaces(14)), lines[3].Text);
                AssertLine("│ warmup 2000000000 ok · 2000000000 failed" + Spaces(56) + " │", 100, lines[26]);
                AssertLine("│ S     4000000000  0.0   N/A  N/A  2000000000  ███░░ 50%" + Spaces(41) + " │", 100, lines[33]);
            })
            .Step("A failure share below 100% never reads as 100%, mirroring the <0.1% floor", context =>
            {
                Assert.Contains("█████ >99.9%", LiveDashboardLayout.Render(new[] { StepSnapshot(3, 9997) }, 100, 0, ColorMode.None)[32].Text);
                Assert.Contains("█████ 99.9%", LiveDashboardLayout.Render(new[] { StepSnapshot(1, 999) }, 100, 0, ColorMode.None)[32].Text);
                Assert.Contains("█████ 99.6%", LiveDashboardLayout.Render(new[] { StepSnapshot(4, 996) }, 100, 0, ColorMode.None)[32].Text);
                Assert.Contains("█████ 99.5%", LiveDashboardLayout.Render(new[] { StepSnapshot(5, 995) }, 100, 0, ColorMode.None)[32].Text);
                Assert.Contains("█████ 99%", LiveDashboardLayout.Render(new[] { StepSnapshot(6, 994) }, 100, 0, ColorMode.None)[32].Text);
                Assert.Contains("█████ 100%", LiveDashboardLayout.Render(new[] { StepSnapshot(0, 5) }, 100, 0, ColorMode.None)[32].Text);
                Assert.Contains("█░░░░ <0.1%", LiveDashboardLayout.Render(new[] { StepSnapshot(9999, 1) }, 100, 0, ColorMode.None)[32].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_status_badges_and_color_modes()
    {
        await Scenario()
            .Step("A completed passed scenario shows the Passed badge", context =>
            {
                var snapshot = new LiveMetricsSnapshot { ScenarioName = "S", PhaseLabel = "completed", IsCompleted = true, Status = TestStatus.Passed };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 40, 0, ColorMode.None);

                AssertLine("S  ● Passed · completed", 23, lines[0]);
            })
            .Step("A skipped scenario shows the Skipped badge", context =>
            {
                var snapshot = new LiveMetricsSnapshot { ScenarioName = "S", PhaseLabel = "completed", IsCompleted = true, Status = TestStatus.Skipped };

                var lines = LiveDashboardLayout.Render(new[] { snapshot }, 40, 0, ColorMode.None);

                AssertLine("S  ● Skipped · completed", 24, lines[0]);
            })
            .Step("Color mode None emits zero escape bytes across the whole frame", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot(), FailedSnapshot() }, 120, 0, ColorMode.None);

                foreach (var line in lines)
                    Assert.DoesNotContain("\u001b", line.Text);
            })
            .Step("TrueColor styles the frame with the warm accent on the panel headers", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.TrueColor);

                Assert.Contains("\u001b[1;38;2;255;157;61mRequests\u001b[0m", lines[26].Text);
                Assert.Contains("\u001b[1;38;2;255;157;61mlatency heatmap\u001b[0m", lines[16].Text);
            })
            .Run();
    }

    [Test]
    public async Task Verify_dashboard_width_invariant_and_determinism()
    {
        await Scenario()
            .Step("No line exceeds the width at any width from 1 to 130 with either glyph set", context =>
            {
                var snapshots = new[] { RichSnapshot(), FailedSnapshot(), IndeterminateSnapshot(), HugeSnapshot(), RampSnapshot(), ThresholdSnapshot() };
                foreach (var glyphSet in new[] { SparklineGlyphSet.Braille, SparklineGlyphSet.Blocks })
                {
                    for (var width = 1; width <= 130; width++)
                    {
                        var lines = LiveDashboardLayout.Render(snapshots, width, 0, ColorMode.TrueColor, glyphSet);
                        AssertMaximumWidth(width, lines);
                    }
                }
            })
            .Step("At every width from 40 to 220 and height from 1 to 60 the frame is exactly the height, the footer owns the last row and no line exceeds the width", context =>
            {
                var snapshots = new[] { RichSnapshot(), ThresholdSnapshot(), IndeterminateSnapshot() };
                for (var width = 40; width <= 220; width++)
                {
                    for (var height = 1; height <= 60; height++)
                    {
                        var lines = LiveDashboardLayout.Render(snapshots, width, height, ColorMode.TrueColor);

                        Assert.HasCount(height, lines, $"Row count at {width}×{height}");
                        Assert.AreEqual(6, lines[height - 1].Width, $"Footer at {width}×{height}");
                        AssertMaximumWidth(width, lines);
                    }
                }
            })
            .Step("The block glyph set passes through to the tile trends and the chart panels", context =>
            {
                // In blocks a body column is one sample and a row eight levels: the ok
                // ramp over 45 columns (sample i at column 4.89 i) at 48 levels sits at 30,
                // 33, 36, 38, 41, 44, 45, 47, 48, 48, so rows 3-5 (levels 1-24) are solid,
                // row 2 (25-32) starts ▆▇▇ (columns 0-2 at 30, 31, 31) and is full from
                // column 3 (v ≥ 91.5), row 1 (33-40) starts at column 4 (v ≥ 94.5) and row 0
                // (41-48) at column 19 (v ≥ 118.5), with level 48 (v ≥ 139.5) from column
                // 38, the last seven. The latency body is solid █ over the median line's
                // bottom row: in blocks a line is the partial block of its level, unjoined,
                // and the flat median's level 1 is the one-eighth block ▁ in every column.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None, SparklineGlyphSet.Blocks);

                AssertLine(Row(Box(Spaces(10) + "▁▂▃▄▅▆▇▇██"), Box(Spaces(9) + "█▇▅▄▃▃▂▂▁▁"), Box(Spaces(19)), Box(Spaces(19)), Box("▕████▌·····▏ 2m 45s")), 120, lines[4]);
                AssertLine(Box("141┤" + Spaces(19) + "▁▁▂▂▃▃▄▄▅▅▅▆▆▆▇▇▇▇▇" + Glyphs('█', 7) + " ▶ 141") + "  " + Box("95 ms┤" + Glyphs('█', 41) + " ▶ 95 ms"), 120, lines[9]);
                AssertLine(Box("   │" + Spaces(4) + "▁▁▂▂▃▃▄▄▅▅▆▆▇▇" + Glyphs('█', 27) + Spaces(6)) + "  " + Box("     │" + Glyphs('█', 41) + Spaces(8)), 120, lines[10]);
                AssertLine(Box(" 71┤▆▇▇" + Glyphs('█', 42) + Spaces(6)) + "  " + Box("65 ms┤" + Glyphs('█', 41) + Spaces(8)), 120, lines[11]);
                AssertLine(Box("  0┤" + Glyphs('█', 45) + Spaces(6)) + "  " + Box("35 ms┤" + Glyphs('▁', 41) + Spaces(8)), 120, lines[14]);
            })
            .Step("Identical inputs render an identical frame", context =>
            {
                var first = LiveDashboardLayout.Render(new[] { RichSnapshot(), ThresholdSnapshot() }, 120, 40, ColorMode.TrueColor);
                var second = LiveDashboardLayout.Render(new[] { RichSnapshot(), ThresholdSnapshot() }, 120, 40, ColorMode.TrueColor);

                Assert.HasCount(first.Count, second);
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.AreEqual(first[index].Text, second[index].Text, $"Text mismatch at row {index}");
                    Assert.AreEqual(first[index].Width, second[index].Width, $"Width mismatch at row {index}");
                }
            })
            .Step("A width below 1 renders nothing and null snapshots are rejected", context =>
            {
                Assert.IsEmpty(LiveDashboardLayout.Render(new[] { RichSnapshot() }, 0, 40, ColorMode.None));
                Assert.ThrowsExactly<ArgumentNullException>(() => LiveDashboardLayout.Render(null!, 120, 40, ColorMode.None));
            })
            .Step("No snapshots renders just the footer padded to the window height", context =>
            {
                var lines = LiveDashboardLayout.Render(Array.Empty<LiveMetricsSnapshot>(), 40, 5, ColorMode.None);

                Assert.HasCount(5, lines);
                AssertLine("q quit", 6, lines[4]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_spinner_glyph_animates_only_the_running_badge()
    {
        await Scenario()
            .Step("A running scenario's badge draws the given spinner glyph in place of the dot", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, 78, 24, ColorMode.None, SparklineGlyphSet.Braille, "⠙");

                AssertLine("Browse catalog  ⠙ Running", 25, lines[0]);
            })
            .Step("The logo still fits beside a spinning title", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 120, 40, ColorMode.None, SparklineGlyphSet.Braille, "⠹");

                AssertLine("Checkout flow  ⠹ Running" + Spaces(83) + "  ⚡ TestFuzn", 120, lines[0]);
            })
            .Step("Failed and passed badges keep their dot regardless of the glyph", context =>
            {
                var failed = LiveDashboardLayout.Render(new[] { FailedSnapshot() }, 78, 24, ColorMode.None, SparklineGlyphSet.Braille, "⠙");
                AssertLine("Checkout flow  ● Failed · completed", 35, failed[0]);

                var passed = new LiveMetricsSnapshot { ScenarioName = "Checkout flow", PhaseLabel = "completed", IsCompleted = true };
                var completed = LiveDashboardLayout.Render(new[] { passed }, 78, 24, ColorMode.None, SparklineGlyphSet.Braille, "⠙");
                AssertLine("Checkout flow  ● Passed · completed", 35, completed[0]);
            })
            .Step("No glyph keeps the dot on the running badge", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, 78, 24, ColorMode.None, SparklineGlyphSet.Braille, null);

                AssertLine("Browse catalog  ● Running", 25, lines[0]);
            })
            .Step("A glyph carrying markup brackets renders literally", context =>
            {
                var lines = LiveDashboardLayout.Render(new[] { IndeterminateSnapshot() }, 78, 24, ColorMode.None, SparklineGlyphSet.Braille, "[");

                AssertLine("Browse catalog  [ Running", 25, lines[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_dashboard_degrades_at_narrow_widths()
    {
        await Scenario()
            .Step("Below 80 columns the logo and the trends are dropped while the rps chart, stacked at the full width, and the full spread remain", context =>
            {
                // The same fit as at 80×24: only the four-row rps chart survives the drop
                // order, its body 64 characters = 128 columns with sample i at column 14.11 i.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 78, 24, ColorMode.None);

                Assert.HasCount(24, lines);
                AssertLine("Checkout flow  ● Running", 24, lines[0]);
                AssertLine(Row(Bottom(15), Bottom(15), Bottom(15), Bottom(15), Bottom(14)), 78, lines[4]);
                AssertLine(PanelTop(RequestsChartTitle, 78), 78, lines[8]);
                AssertLine(Box("141┤" + Spaces(18) + "⢀" + Glyphs('⣀', 7) + "⣠" + Glyphs('⣤', 7) + "⣴" + Glyphs('⣶', 12) + Glyphs('⣿', 17) + " ▶ 141"), 78, lines[9]);
                AssertLine(Box(" 71┤⣤⣴" + Glyphs('⣶', 7) + "⣾" + Glyphs('⣿', 54) + Spaces(6)), 78, lines[10]);
                AssertLine(Box("  0┤" + Glyphs('⣿', 64) + Spaces(6)), 78, lines[12]);
                AssertLine("│         count  rps    min    mean    p50     p75     p95     p99     max" + Spaces(2) + " │", 78, lines[16]);
                AssertLine("q quit", 6, lines[23]);
                AssertMaximumWidth(78, lines);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain("⚡", line.Text);
                    Assert.DoesNotContain(LatencyChartTitle, line.Text);
                }
            })
            .Step("Below 60 columns the chart panels are dropped and the requests table narrows, and the drop order reaches the step table", context =>
            {
                // 13 header rows (the wrapped tiles, a two-line timeline), six requests rows,
                // five step rows and four error rows are five over the 23 above the footer:
                // the two step rows and the two error rows go, then the step table whole,
                // and the error ticker keeps its frame.
                var lines = LiveDashboardLayout.Render(new[] { RichSnapshot() }, 56, 24, ColorMode.None);

                Assert.HasCount(24, lines);
                AssertLine("│         count  rps    mean    p50     p95     max    │", 56, lines[15]);
                AssertLine("│ ok      12480  141   38 ms  35 ms   72 ms  312 ms    │", 56, lines[16]);
                AssertLine(Bottom(56), 56, lines[18]);
                AssertLine(PanelTop("Errors", 56), 56, lines[19]);
                AssertLine(Bottom(56), 56, lines[20]);
                AssertLine(string.Empty, 0, lines[21]);
                AssertLine("q quit", 6, lines[23]);
                AssertMaximumWidth(56, lines);

                foreach (var line in lines)
                {
                    Assert.DoesNotContain(RequestsChartTitle, line.Text);
                    Assert.DoesNotContain("Steps", line.Text);
                }
            })
            .Run();
    }

    private static string Row(params string[] boxes)
    {
        return string.Join(new string(' ', TileRowWidget.Gap), boxes);
    }

    /// <summary>A panel content row — a tile box's or a full-width panel's: the inner text between the borders and their padding.</summary>
    private static string Box(string inner)
    {
        return "│ " + inner + " │";
    }

    /// <summary>A panel's top border with its header, at the given width.</summary>
    private static string PanelTop(string header, int width)
    {
        return "╭─ " + header + " " + new string('─', width - 5 - header.Length) + "╮";
    }

    private static string Glyphs(char glyph, int count)
    {
        return new string(glyph, count);
    }

    /// <summary>A latency bucket vector with the given counts, every other bucket zero.</summary>
    private static int[] Counts(params (int Bucket, int Count)[] entries)
    {
        var counts = new int[LatencyBuckets.Count];
        foreach (var entry in entries)
            counts[entry.Bucket] = entry.Count;

        return counts;
    }

    private static double[] ToDoubles(int[] values)
    {
        var doubles = new double[values.Length];
        for (var index = 0; index < values.Length; index++)
            doubles[index] = values[index];

        return doubles;
    }

    /// <summary>
    /// The median, p99 and bucket series a p95 series stands in for in the synthetic
    /// snapshots: the median half the p95 and the p99 1.2 times it — an idle interval's zero
    /// staying zero in both — and every interval's ok requests in the bucket its p95 falls in,
    /// an idle or requestless interval an all-zero vector.
    /// </summary>
    private static (double[] Median, double[] Percentile99, IReadOnlyList<int>[] Buckets) LatencySeries(double[] responseTimePercentile95, int[] okDeltas)
    {
        var median = new double[responseTimePercentile95.Length];
        var percentile99 = new double[responseTimePercentile95.Length];
        var buckets = new IReadOnlyList<int>[responseTimePercentile95.Length];
        for (var index = 0; index < responseTimePercentile95.Length; index++)
        {
            var percentile95 = responseTimePercentile95[index];
            median[index] = percentile95 / 2;
            percentile99[index] = percentile95 * 1.2;

            if (double.IsFinite(percentile95) && percentile95 > 0 && okDeltas[index] > 0)
                buckets[index] = Counts((LatencyBuckets.IndexOf(TimeSpan.FromMilliseconds(percentile95)), okDeltas[index]));
            else
                buckets[index] = new int[LatencyBuckets.Count];
        }

        return (median, percentile99, buckets);
    }

    private static string Top(int width)
    {
        return "╭" + new string('─', width - 2) + "╮";
    }

    private static string Bottom(int width)
    {
        return "╰" + new string('─', width - 2) + "╯";
    }

    private static string Spaces(int count)
    {
        return new string(' ', count);
    }

    private static string Sgr(string parameters, string text)
    {
        return "\u001b[" + parameters + "m" + text + "\u001b[0m";
    }

    private static void AssertLine(string expectedText, int expectedWidth, RenderedLine actualLine)
    {
        Assert.AreEqual(expectedText, actualLine.Text);
        Assert.AreEqual(expectedWidth, actualLine.Width);
    }

    private static void AssertMaximumWidth(int width, IReadOnlyList<RenderedLine> lines)
    {
        for (var index = 0; index < lines.Count; index++)
            Assert.IsLessThanOrEqualTo(width, lines[index].Width, $"Line {index} exceeds width {width}");
    }
}
