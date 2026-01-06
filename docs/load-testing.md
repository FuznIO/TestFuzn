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
        .Id("Scenario-1")
        .Step("Step 1", async (context) =>
        {
        })
        .Step("Step 2", async (context) =>
        {
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

Multiple scenario load tests allow you to run several scenarios concurrently, simulating diverse user behaviors happening at the same time. 
This is useful for testing realistic traffic patterns where different users perform different actions simultaneously (e.g., some users browsing while others checkout).

```csharp
[Test]
public async Task Multiple_scenarios_load_test()
{
    var scenario2 = Scenario("Second scenario")
        .Step("Step 1", (context) =>
        {
        })
        .Load().Simulations((context, simulations) =>
        {
            // Define load simulations here. First and second scenario can have different simulations.
            simulations.FixedLoad(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100));
            simulations.GradualLoadIncrease(1, 10, TimeSpan.FromSeconds(5));
            simulations.OneTimeLoad(100);
            simulations.Pause(TimeSpan.FromSeconds(5));
            simulations.FixedConcurrentLoad(1000, TimeSpan.FromSeconds(100));
            simulations.RandomLoadPerSecond(10, 50, TimeSpan.FromSeconds(100));
        });

    await Scenario("First scenario")
        .Id("Scenario-1")
        .Step("Step 1", (context) =>
        {
        })
        .Step("Step 2", async (context) =>
        {
        })
        .Load().Simulations((context, simulations) =>
        {
            // Define load simulations here. First and second scenario can have different simulations.
            simulations.GradualLoadIncrease(1, 10, TimeSpan.FromSeconds(5));
            simulations.FixedLoad(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100));
            simulations.OneTimeLoad(100);
            simulations.Pause(TimeSpan.FromSeconds(5));
            simulations.FixedConcurrentLoad(1000, TimeSpan.FromSeconds(100));
            simulations.RandomLoadPerSecond(10, 50, TimeSpan.FromSeconds(100));
        })
        .Load().IncludeScenario(scenario2)
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

Validate performance during and after test execution:

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
    var loginStats = stats.GetStep("Login");
    Assert.IsTrue(loginStats.Ok.ResponseTimeMean < TimeSpan.FromMilliseconds(300));
})
```

---

## Statistics

Available metrics for assertions:

| Metric | Description |
|--------|-------------|
| `RequestCount` | Total number of requests |
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
