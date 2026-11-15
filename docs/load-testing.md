# Load Testing

Load tests are defined by adding `Load().Simulations()` to a scenario. This changes the test from a standard test to a load test.

Standard tests run once by default or once per input data item, while load tests instead run the number of iterations defined by their simulations.

---

## Single Scenario

A single scenario load test executes one scenario with a defined sequence of steps. 
Use this approach when you want to test a specific user journey or workflow under load. 
The scenario runs according to the simulations you configure, allowing you to measure performance metrics for that isolated flow.

```csharp
[Test]
public async Task Single_scenario_load_test()
{
    await Scenario()
        .Id("PROD-CRUD-001")
        .Step("Authenticate and retrieve JWT token", async (context) =>
        {
            // Get auth token from /api/Auth/token
        })
        .Step("Call GET /Products to list all products", async (context) =>
        {
            // Fetch product list
        })
        .Load().Simulations((context, simulations) =>
        {
            // Define load simulations here.
            simulations.GradualLoadIncrease(1, 10, TimeSpan.FromSeconds(5));
            simulations.FixedLoad(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100));
            simulations.OneTimeLoad(100);
            simulations.Pause(TimeSpan.FromSeconds(5));
            simulations.FixedConcurrentLoad(1000, TimeSpan.FromSeconds(100));
            simulations.RandomLoadPerSecond(10, 50, TimeSpan.FromSeconds(100));
        });
}
```

---

## Multiple Scenarios

Multiple-scenario load tests let you run several scenarios concurrently to simulate different user behaviors happening at the same time (for example, some users browsing products while others creating new products).

Each scenario runs **independently** with its own load simulations and statistics.
TestFuzn does **not** merge or aggregate load, timing, or metrics across scenarios—results are reported **per scenario only**.

Give each scenario a name with `Scenario("...")`. The main scenario defaults to the test name. A scenario passed to `IncludeScenario` must have a name.

```csharp
[Test]
public async Task Multiple_scenarios_load_test()
{
    var browseScenario = Scenario("Browse products")
        .Step("Call GET /Products", (context) =>
        {
            // Fetch product list
        })
        .Load().Simulations((context, simulations) =>
        {
            // Define load simulations here. Browse and create scenarios can have different simulations.
            simulations.FixedLoad(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100));
            simulations.GradualLoadIncrease(1, 10, TimeSpan.FromSeconds(5));
            simulations.OneTimeLoad(100);
            simulations.Pause(TimeSpan.FromSeconds(5));
            simulations.FixedConcurrentLoad(1000, TimeSpan.FromSeconds(100));
            simulations.RandomLoadPerSecond(10, 50, TimeSpan.FromSeconds(100));
        });

    await Scenario("Create products")
        .Id("PROD-CREATE-001")
        .Step("Authenticate and retrieve JWT token", (context) =>
        {
            // Get auth token
        })
        .Step("Call POST /Products to create a new product", async (context) =>
        {
            // Create product
        })
        .Load().Simulations((context, simulations) =>
        {
            // Define load simulations here. Browse and create scenarios can have different simulations.
            simulations.GradualLoadIncrease(1, 10, TimeSpan.FromSeconds(5));
            simulations.FixedLoad(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100));
            simulations.OneTimeLoad(100);
            simulations.Pause(TimeSpan.FromSeconds(5));
            simulations.FixedConcurrentLoad(1000, TimeSpan.FromSeconds(100));
            simulations.RandomLoadPerSecond(10, 50, TimeSpan.FromSeconds(100));
        })
        .Load().IncludeScenario(browseScenario);
}
```

---

## Warmup

Before a load test starts, warmup simulations can be executed to stabilize performance.
This is important because .NET performs runtime optimizations (JIT compilation, caching, thread pool ramp-up) during initial execution.
No metrics or statistics are collected during the warmup phase, only the actual load test is measured.

```csharp
.Load().Warmup((context, simulations) =>
{
    simulations.FixedConcurrentLoad(10, TimeSpan.FromSeconds(5));
})
```

### Assert While Warming Up

Validate warmup iterations and stop the test early if things are fundamentally broken (e.g., the target service is down). 
Some failures during warmup are expected, so use conditions like minimum iteration count or elapsed time to avoid stopping prematurely:

```csharp
.Load().AssertWhileWarmingUp((context, warmup) =>
{
    // Stop if more than 80% of iterations fail after at least 10 have completed
    if (warmup.Count >= 10 && warmup.FailedRate > 0.8)
        throw new Exception("Too many failures during warmup");
})
```

Available properties on `WarmupStats`:

| Property | Description |
|----------|-------------|
| `Count` | Number of warmup iterations |
| `Ok.Count` | Number of ok warmup iterations |
| `Failed.Count` | Number of failed warmup iterations |
| `OkRate` | Share of ok iterations (0.0 to 1.0) |
| `FailedRate` | Share of failed iterations (0.0 to 1.0) |
| `Elapsed` | Time since warmup started |

---

## Watching a Load Test Run

Under the default MSTest runner there is no console output while a load test runs. Pass `--runner=testfuzn` to run it under TestFuzn's own runner instead, which prints live statistics as the test executes. See [Test Runners](test-runners.md).

To run the load on other machines, see [Cluster Runs](cluster-runs.md).

Under the MSTest runner, tests can run in parallel. That is fine for short load tests. Mark a long load test with `[DoNotParallelize]`, so it runs alone and other tests do not take its CPU and change its numbers. The TestFuzn runner runs one test at a time.

In the load report, the charts over time show each interval on its own: the iterations per second (ok, failed and the planned target), the P50, P95 and P99 duration, and the virtual users, which is the average number of iterations running at the same time. Each point covers the iterations that ended since the point before. A spike late in a long run therefore shows. A run keeps at most 100 points, evenly spread: 3 s apart at first, and further apart as the run gets longer. The snapshot table shows the totals from the start of the measurement.

## Run-Once Hooks

`.Load().BeforeScenarioOnce` runs once before the load starts and may return data; read it anywhere in the scenario with `context.OnceData<T>()`. `.Load().AfterScenarioOnce` runs once after the load, also after a stop or failure:

```csharp
await Scenario()
    .Load().BeforeScenarioOnce(async context => await api.CreateItems(100))
    .InputDataFromList(context => context.OnceData<List<Item>>().Cast<object>().ToList())
    .Step("Use item", async context => { /* ... */ })
    .Load().Simulations((context, simulations) => simulations.OneTimeLoad(100))
    .Load().AfterScenarioOnce(async context => await api.DeleteItems(context.OnceData<List<Item>>()));
```

The data must be JSON-serializable: in a cluster run the hooks run once on the controller and the data goes to every load worker as JSON.

## Simulations

Define load patterns to simulate real-world traffic. A **rate** is iterations started per second, unless you give an interval. A **count** is a total, or a number running at the same time. A rate is spread evenly over each second or interval instead of started in one burst, and a simulation with a duration stops starting iterations when its duration ends.

| Simulation | What it does |
|---|---|
| `OneTimeLoad(count)` | Starts `count` iterations all at once |
| `FixedLoad(rate, duration)` | Starts `rate` iterations per second |
| `FixedLoad(rate, interval, duration)` | Starts `rate` iterations per interval |
| `GradualLoadIncrease(startRate, endRate, duration)` | Changes the rate per second evenly from `startRate` to `endRate`; a lower `endRate` ramps down |
| `FixedConcurrentLoad(count, duration)` | Keeps `count` iterations running at the same time (virtual users) |
| `RandomLoadPerSecond(minRate, maxRate, duration)` | Starts a random number per second, between `minRate` and `maxRate` |
| `Pause(duration)` | Starts nothing |

Invalid values throw an `ArgumentOutOfRangeException` when the simulation is built: a count or rate below 0, a duration or interval of 0 or less (a pause may be 0), or `minRate` above `maxRate`.

### One-Time Load

Start a number of iterations all at once:

```csharp
simulations.OneTimeLoad(1000); // 1000 iterations at once
```

### Gradual Load Increase

Change the rate evenly over a duration:

```csharp
simulations.GradualLoadIncrease(
    startRate: 1,                          // 1 per second at the start
    endRate: 100,                          // 100 per second at the end
    duration: TimeSpan.FromSeconds(30)     // about 1,515 iterations in total
);

simulations.GradualLoadIncrease(100, 10, TimeSpan.FromSeconds(30)); // ramps down
```

### Fixed Load

Start a fixed number of iterations per second:

```csharp
simulations.FixedLoad(
    rate: 50,                              // 50 per second
    duration: TimeSpan.FromMinutes(5)      // 15,000 iterations in total
);

// Or per interval of your choice
simulations.FixedLoad(
    rate: 100,                             // 100 every 2 seconds
    interval: TimeSpan.FromSeconds(2),
    duration: TimeSpan.FromMinutes(5)
);

// 50 in total, spread evenly over 30 seconds
simulations.FixedLoad(50, interval: TimeSpan.FromSeconds(30), duration: TimeSpan.FromSeconds(30));
```

The load stops when the duration ends, also partway through an interval.

### Fixed Concurrent Load

Keep a number of iterations running at the same time. A new iteration starts as soon as one finishes:

```csharp
simulations.FixedConcurrentLoad(
    count: 500,                            // 500 virtual users
    duration: TimeSpan.FromMinutes(10)
);
```

### Random Load Per Second

Start a random number of iterations each second, spread over the second:

```csharp
simulations.RandomLoadPerSecond(
    minRate: 10,
    maxRate: 100,
    duration: TimeSpan.FromMinutes(5)
);
```

### Pause

Add pauses between simulations:

```csharp
simulations.Pause(TimeSpan.FromSeconds(30));
```

### Combined Simulations

Chain multiple simulations together:

```csharp
.Load().Simulations((context, simulations) =>
{
    simulations.GradualLoadIncrease(1, 50, TimeSpan.FromSeconds(20));
    simulations.FixedLoad(50, TimeSpan.FromMinutes(2));
    simulations.Pause(TimeSpan.FromSeconds(10));
    simulations.FixedConcurrentLoad(100, TimeSpan.FromMinutes(3));
})
```

---

## Assertions

Validate performance during and after test execution.

All assertion methods follow the same pattern: if the action throws an exception, the test is stopped and marked as failed.

### Assert While Warming Up

See [Warmup > Assert While Warming Up](#assert-while-warming-up) above.

### Assert While Running

Continuous validation during the test. If assertion fails, the test stops:

```csharp
.Load().AssertWhileRunning((context, stats) =>
{
    Assert.IsTrue(stats.Count < 10000, "Too many iterations");
    Assert.IsTrue(stats.Ok.DurationMean < TimeSpan.FromSeconds(1), "Response too slow");
    Assert.AreEqual(0, stats.Failed.Count, "Should have no failures");
})
```

### Assert When Done

Final validation after test completion:

```csharp
.Load().AssertWhenDone((context, stats) =>
{
    Assert.AreEqual(0, stats.Failed.Count, "Expected no failures");
    Assert.IsTrue(stats.Ok.DurationMean < TimeSpan.FromSeconds(0.5));
    Assert.IsTrue(stats.Ok.DurationPercentile99 < TimeSpan.FromSeconds(1));
    
    // Per-step assertions
    var createProductStats = stats.GetStep("Call POST /Products to create a new product");
    Assert.IsTrue(createProductStats.Ok.DurationMean < TimeSpan.FromMilliseconds(300));
})
```

---

## Statistics

One pass through the scenario is an iteration. The scenario stats count iterations; the stats of a step (`stats.GetStep("name")`) count the times that step ran. Both have the same shape:

| Metric | Description |
|--------|-------------|
| `Count` | Number of iterations (or step runs) |
| `OkRate` | Share of ok iterations (0.0 to 1.0) |
| `FailedRate` | Share of failed iterations (0.0 to 1.0) |
| `Ok`, `Failed` | The statistics below, for the ok and the failed ones |

On `Ok` and `Failed`:

| Metric | Description |
|--------|-------------|
| `Count` | Number of iterations (or step runs) |
| `PerSecond` | Iterations (or step runs) per second |
| `DurationMin` | Shortest duration |
| `DurationMax` | Longest duration |
| `DurationMean` | Average duration |
| `DurationMedian` | 50th percentile duration |
| `DurationPercentile75` | 75th percentile duration |
| `DurationPercentile90` | 90th percentile duration |
| `DurationPercentile95` | 95th percentile duration |
| `DurationPercentile99` | 99th percentile duration |
| `DurationStandardDeviation` | Standard deviation of the durations |

```csharp
stats.Count                  // All iterations
stats.Ok.Count               // Ok iterations
stats.Failed.Count           // Failed iterations
stats.Ok.DurationMean        // Average duration of the ok iterations
stats.GetStep("Login").Ok.PerSecond   // Ok runs of the Login step per second
```

---

[← Back to Table of Contents](README.md)
