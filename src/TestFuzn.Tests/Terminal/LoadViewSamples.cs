using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

internal static class LoadViewSamples
{
    private static readonly DateTime Start = new DateTime(2026, 9, 20, 12, 4, 21, DateTimeKind.Utc);

    public static LiveMetricsSnapshot RunningSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Checkout flow",
            Phase = LoadTestPhase.Measurement,
            Duration = TimeSpan.FromSeconds(135),
            Timestamp = Start.AddSeconds(135),
            PhaseTimes = new TestPhasesLayout.PhaseTimes
            {
                InitStart = Start,
                InitEnd = Start.AddSeconds(2),
                WarmupStart = Start.AddSeconds(2),
                WarmupEnd = Start.AddSeconds(32),
                MeasurementStart = Start.AddSeconds(32)
            },
            HasWarmup = true,
            Simulations = new[]
            {
                new SimulationInfo { Description = "Fixed Load - Rate: 10, Interval: 0:00:01, Duration: 0:00:30 (Warmup)", IsWarmup = true },
                new SimulationInfo { Description = "Fixed Load - Rate: 400, Interval: 0:00:01, Duration: 0:05:00", IsWarmup = false }
            },
            RequestCountOk = 12480,
            RequestCountFailed = 32,
            WarmupRequestCountOk = 1200,
            WarmupRequestCountFailed = 3,
            RequestsPerSecond = 142,
            Ok = Stats(12480, 142, 12, 38, 15, 35, 48, 72, 94, 312),
            Failed = Stats(32, 1, 88, 102, 20, 99, 110, 140, 160, 201),
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
                    Ok = Stats(6250, 71, 8, 18, 6, 16, 22, 40, 55, 120),
                    Failed = Stats(2, 1, 95, 102, 21, 99, 110, 140, 160, 980)
                },
                new LiveStepMetrics
                {
                    Name = "Checkout",
                    RequestCountOk = 6230,
                    RequestCountFailed = 30,
                    Ok = Stats(6230, 69, 20, 58, 19, 52, 70, 90, 130, 410),
                    Failed = Stats(30, 1, 95, 102, 21, 99, 110, 140, 160, 980),
                    Steps = new[]
                    {
                        new LiveStepMetrics
                        {
                            Name = "Pay",
                            RequestCountOk = 6230,
                            Ok = Stats(6230, 69, 10, 22, 7, 20, 26, 44, 61, 150)
                        }
                    }
                }
            }
        };
    }

    public static LiveMetricsSnapshot SecondSnapshot()
    {
        return new LiveMetricsSnapshot
        {
            ScenarioName = "Browse catalog",
            Phase = LoadTestPhase.Warmup,
            Duration = TimeSpan.FromSeconds(42),
            Timestamp = Start.AddSeconds(42),
            PhaseTimes = new TestPhasesLayout.PhaseTimes { InitStart = Start, InitEnd = Start.AddSeconds(2), WarmupStart = Start.AddSeconds(2) },
            RequestsPerSecond = 48,
            Simulations = new[] { new SimulationInfo { Description = "Gradual Load Increase - From: 10, To: 50, Duration: 0:02:00", IsWarmup = false } },
            Ok = Stats(2000, 48, 5, 12, 4, 11, 15, 22, 30, 90),
            Failed = Stats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        };
    }

    public static Dictionary<Scenario, ScenarioLoadResult> SummaryResults()
    {
        var scenario = new Scenario("Checkout flow");
        scenario.SimulationsInternal.Add(new FixedLoadConfiguration(10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)) { IsWarmup = true });
        scenario.SimulationsInternal.Add(new FixedLoadConfiguration(400, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5)));
        var result = new ScenarioLoadResult
        {
            ScenarioName = "Checkout flow",
            Description = "Buys a product and checks out",
            Simulations = new List<string> { "Fixed Load - Rate: 400, Interval: 0:00:01, Duration: 0:05:00" },
            InitStartTime = Start,
            InitEndTime = Start.AddSeconds(2),
            WarmupStartTime = Start.AddSeconds(2),
            WarmupEndTime = Start.AddSeconds(32),
            MeasurementStartTime = Start.AddSeconds(32),
            MeasurementEndTime = Start.AddSeconds(305),
            CleanupStartTime = Start.AddSeconds(305),
            CleanupEndTime = Start.AddSeconds(312),
            Created = Start.AddSeconds(312),
            IsCompleted = true,
            Status = TestStatus.Failed,
            RequestCount = 24000,
            RequestsPerSecond = 397,
            WarmupRequestCountOk = 1200,
            WarmupRequestCountFailed = 3,
            Ok = CoreStats(16001, 265, 12, 38, 15, 35, 48, 72, 94, 312),
            Failed = CoreStats(7999, 132, 88, 102, 20, 99, 110, 140, 160, 201),
            AssertWhileRunningException = new InvalidOperationException("Assert.IsLessThan failed. p95 too high: 240 ms"),
            Steps = new Dictionary<string, StepLoadResult>
            {
                ["Add to cart"] = new StepLoadResult
                {
                    Name = "Add to cart",
                    Ok = CoreStats(24000, 397, 8, 18, 6, 16, 22, 40, 55, 120),
                    Failed = CoreStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
                },
                ["Checkout"] = new StepLoadResult
                {
                    Name = "Checkout",
                    Ok = CoreStats(16001, 265, 20, 58, 19, 52, 70, 90, 130, 410),
                    Failed = CoreStats(7999, 132, 95, 102, 21, 99, 110, 140, 160, 980),
                    Errors = new Dictionary<string, ErrorEntry> { ["Assertion failed."] = new ErrorEntry { Count = 7999 } }
                }
            }
        };

        return new Dictionary<Scenario, ScenarioLoadResult> { [scenario] = result };
    }

    public static LiveStats Stats(int requestCount, int requestsPerSecond, double min, double mean, double standardDeviation,
        double median, double percentile75, double percentile95, double percentile99, double max)
    {
        return new LiveStats
        {
            RequestCount = requestCount,
            RequestsPerSecond = requestsPerSecond,
            ResponseTimeMin = TimeSpan.FromMilliseconds(min),
            ResponseTimeMean = TimeSpan.FromMilliseconds(mean),
            ResponseTimeStandardDeviation = TimeSpan.FromMilliseconds(standardDeviation),
            ResponseTimeMedian = TimeSpan.FromMilliseconds(median),
            ResponseTimePercentile75 = TimeSpan.FromMilliseconds(percentile75),
            ResponseTimePercentile95 = TimeSpan.FromMilliseconds(percentile95),
            ResponseTimePercentile99 = TimeSpan.FromMilliseconds(percentile99),
            ResponseTimeMax = TimeSpan.FromMilliseconds(max)
        };
    }

    public static Stats CoreStats(int requestCount, int requestsPerSecond, double min, double mean, double standardDeviation,
        double median, double percentile75, double percentile95, double percentile99, double max)
    {
        return new Stats
        {
            RequestCount = requestCount,
            RequestsPerSecond = requestsPerSecond,
            ResponseTimeMin = TimeSpan.FromMilliseconds(min),
            ResponseTimeMean = TimeSpan.FromMilliseconds(mean),
            ResponseTimeStandardDeviation = TimeSpan.FromMilliseconds(standardDeviation),
            ResponseTimeMedian = TimeSpan.FromMilliseconds(median),
            ResponseTimePercentile75 = TimeSpan.FromMilliseconds(percentile75),
            ResponseTimePercentile95 = TimeSpan.FromMilliseconds(percentile95),
            ResponseTimePercentile99 = TimeSpan.FromMilliseconds(percentile99),
            ResponseTimeMax = TimeSpan.FromMilliseconds(max)
        };
    }
}
