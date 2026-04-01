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
        })
        .Run();
}
```

---

## Multiple Scenarios

Multiple-scenario load tests let you run several scenarios concurrently to simulate different user behaviors happening at the same time (for example, some users browsing products while others creating new products).

Each scenario runs **independently** with its own load simulations and statistics.
TestFuzn does **not** merge or aggregate load, timing, or metrics across scenarios—results are reported **per scenario only**.

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
        .Load().IncludeScenario(browseScenario)
        .Run();
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
    if (warmup.TotalCount >= 10 && warmup.FailedRate > 0.8)
        throw new Exception("Too many failures during warmup");
})
```

Available properties on `WarmupStats`:

| Property | Description |
|----------|-------------|
| `OkCount` | Number of successful warmup iterations |
| `FailedCount` | Number of failed warmup iterations |
| `TotalCount` | Total warmup iterations (OkCount + FailedCount) |
| `OkRate` | Rate of successful iterations (0.0 to 1.0) |
| `FailedRate` | Rate of failed iterations (0.0 to 1.0) |
| `Duration` | Time since warmup started |

---

## Simulations

Define load patterns to simulate real-world traffic:

### One-Time Load

Execute a specific number of iterations:

```csharp
simulations.OneTimeLoad(1000); // Execute 1000 times
```

### Gradual Load Increase

Ramp up load gradually:

```csharp
simulations.GradualLoadIncrease(
    startRate: 1, 
    endRate: 100, 
    duration: TimeSpan.FromSeconds(30)
);
```

### Fixed Load

Maintain constant requests per second:

```csharp
simulations.FixedLoad(
    rate: 50,                              // Requests per second
    duration: TimeSpan.FromMinutes(5)
);

// Or with custom interval
simulations.FixedLoad(
    rate: 100,
    interval: TimeSpan.FromSeconds(1),
    duration: TimeSpan.FromMinutes(5)
);
```

### Fixed Concurrent Load

Maintain a constant number of concurrent users:

```csharp
simulations.FixedConcurrentLoad(
    count: 500,                            // Concurrent users
    duration: TimeSpan.FromMinutes(10)
);
```

### Random Load Per Second

Vary load randomly within a range:

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
    Assert.IsTrue(stats.RequestCount < 10000, "Too many requests");
    Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(1), "Response too slow");
    Assert.AreEqual(0, stats.Failed.RequestCount, "Should have no failures");
})
```

### Assert When Done

Final validation after test completion:

```csharp
.Load().AssertWhenDone((context, stats) =>
{
    Assert.AreEqual(0, stats.Failed.RequestCount, "Expected no failures");
    Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(0.5));
    Assert.IsTrue(stats.Ok.ResponseTimePercentile99 < TimeSpan.FromSeconds(1));
    
    // Per-step assertions
    var createProductStats = stats.GetStep("Call POST /Products to create a new product");
    Assert.IsTrue(createProductStats.Ok.ResponseTimeMean < TimeSpan.FromMilliseconds(300));
})
```

---

## Statistics

Available metrics for assertions:

| Metric | Description |
|--------|-------------|
| `RequestCount` | Total number of requests |
| `OkRate` | Rate of successful requests (0.0 to 1.0) |
| `FailedRate` | Rate of failed requests (0.0 to 1.0) |
| `RequestsPerSecond` | Requests per second |
| `ResponseTimeMin` | Minimum response time |
| `ResponseTimeMax` | Maximum response time |
| `ResponseTimeMean` | Average response time |
| `ResponseTimeMedian` | 50th percentile response time |
| `ResponseTimePercentile75` | 75th percentile response time |
| `ResponseTimePercentile95` | 95th percentile response time |
| `ResponseTimePercentile99` | 99th percentile response time |
| `ResponseTimeStandardDeviation` | Response time standard deviation |

Access statistics separately for successful (`Ok`) and failed (`Failed`) requests:

```csharp
stats.Ok.RequestCount        // Successful requests
stats.Failed.RequestCount    // Failed requests
stats.Ok.ResponseTimeMean    // Average response time for successful requests
```

---

[← Back to Table of Contents](README.md)
