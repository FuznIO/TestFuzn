# Upgrading

Breaking changes and the steps to move across them. Newest first.

---

## 0.9.0 — Cockpit gets reports from `cockpit upload`; one load test per cluster run

The Cockpit sink now sends live load data only. Cluster runs use protocol 3: update Cockpit, the worker
app and your test projects together.

**1. Send the report with `cockpit upload`** after the tests, for example in CI:

```bash
cockpit upload --framework testfuzn --source nightly
```

Cockpit's Send results page shows the full step for your CI. Cluster runs need no upload: Cockpit imports
them itself when they end.

**Cluster runs need a source.** Set `TESTFUZN_COCKPIT_SOURCE`, for example `nightly-load`. Without it the
run stops before it starts. On your own machine, the branch and commit now come from git when no CI
variable sets them.

**2. Run one load test per cluster run.** With `--cluster` or `TESTFUZN_CLUSTER=true`, a second test in
the same session fails without running, a standard test too. Run each load test with its own command,
and run standard tests without the cluster.

**3. Watch cluster runs in Cockpit.** The terminal prints the live link, a `Running…` line every 30 s,
the result and the link to the run.

**4. `--detach`, `--runs`, `--results` and `--stop` are removed.** Use Cockpit's TestFuzn cluster page
to see runs, their results and to stop them. A CI job runs with `--cluster` and waits for the result.

**5. Runs queue when the workers are busy.** A run no longer fails because the pool's workers are busy:
it waits its turn and starts by itself. Update Cockpit together with TestFuzn.

**6. Every test, scenario and step has an id.** Without `Id = "..."`, `.Id("...")` or a step id, TestFuzn
makes one from the names (12 characters of a SHA-256 hash), and the XML reports always carry it; step ids
set by hand were left out of the report before. Two steps with the same id in a scenario, or two tests with
the same id, now stop the test with an error that names both: give one of them another id.

**7. Load errors are grouped by shape.** Error messages that differ only by an id, a decimal or a number of
4 or more digits count as one error, shown with a placeholder: `Order {n} not found`. A step still keeps 10
kinds of error; the rest now show as one `Other errors` row instead of being dropped, so the counts add up
to the failed total.

---

## 0.9.0 — load simulations do what their docs say

A **rate** is now iterations started per second, unless you give an interval, and starts are spread
evenly instead of sent in bursts. Every simulation ends when its duration ends. The amount of load
changes for these calls:

| Call | Before | Now |
|---|---|---|
| `FixedLoad(50, 30s)` | 50 in total, spread over 30 s | 50 per second: 1,500 in total |
| `FixedLoad(100, 2s, 3s)` | 2 whole intervals: 200 in 4 s | stops at 3 s: 150 |
| `GradualLoadIncrease(5, 50, 5s)` | bursts of 5, 6, … 50 every 0.11 s: about 45/s to 450/s, 1,265 in total | 5/s rising evenly to 50/s: about 138 in total |
| `GradualLoadIncrease(50, 5, 10s)` | nothing ran | ramps down from 50/s to 5/s |
| `RandomLoadPerSecond(10, 50, 1min)` | each second's iterations started at once | spread over the second |
| `FixedConcurrentLoad(10, 1min)` | a free slot was refilled every 10–15 ms, so fast iterations ran well below 10 at once | the next iteration starts as soon as one finishes |

**1. Check the load your tests ask for.** To keep the old `FixedLoad(rate, duration)` behaviour of
`rate` iterations in total, give the duration as the interval:

```csharp
// Before
simulations.FixedLoad(50, TimeSpan.FromSeconds(30));

// After, same load as before
simulations.FixedLoad(50, interval: TimeSpan.FromSeconds(30), duration: TimeSpan.FromSeconds(30));
```

**2. Invalid values now throw.** An `ArgumentOutOfRangeException` is thrown when the simulation is built
for a count or rate below 0, a duration or interval of 0 or less (a `Pause` may be 0), or a `minRate`
above `maxRate`. Before, an interval of 0 looped forever and `minRate` above `maxRate` failed during
the run.

---

## 0.9.0 — load stats count iterations

One pass through a scenario is an iteration. The load stats called it a request, which reads like one HTTP
call. They now use one shape everywhere: the object tells you what is counted. Durations are named
`Duration…`, and the time a phase has run is `Elapsed`.

**1. Rename the members** in `AssertWhileRunning`, `AssertWhenDone` and `AssertWhileWarmingUp`:

| Before | After |
|--------|-------|
| `stats.RequestCount` | `stats.Count` |
| `stats.Ok.RequestCount`, `stats.Failed.RequestCount` | `stats.Ok.Count`, `stats.Failed.Count` |
| `stats.Ok.RequestsPerSecond` | `stats.Ok.PerSecond` |
| `stats.Ok.ResponseTimeMin`, `…Max`, `…Mean`, `…Median`, `…StandardDeviation` | `stats.Ok.DurationMin`, `…Max`, `…Mean`, `…Median`, `…StandardDeviation` |
| `stats.Ok.ResponseTimePercentile75`, `…90`, `…95`, `…99` | `stats.Ok.DurationPercentile75`, `…90`, `…95`, `…99` |
| `step.Ok.RequestCount`, `step.Ok.RequestsPerSecond` | `step.Ok.Count`, `step.Ok.PerSecond` |
| `warmup.TotalCount` | `warmup.Count` |
| `warmup.OkCount`, `warmup.FailedCount` | `warmup.Ok.Count`, `warmup.Failed.Count` |
| `warmup.Duration` | `warmup.Elapsed` |

`step` is `stats.GetStep("name")`. It now also has `Count`, `OkRate` and `FailedRate`, like the scenario.

```csharp
// Before
Assert.AreEqual(0, stats.Failed.RequestCount);
Assert.IsTrue(stats.Ok.ResponseTimePercentile95 < TimeSpan.FromSeconds(1));

// After
Assert.AreEqual(0, stats.Failed.Count);
Assert.IsTrue(stats.Ok.DurationPercentile95 < TimeSpan.FromSeconds(1));
```

**2. Reports and console output** say "Iterations" and "Iterations/s" for a scenario, and "Count" and
"Per second" for a step. In the XML report, `RequestCount` and `RequestsPerSecond` are now `Count` and
`PerSecond`, the timeline's `RequestsPerSecond` is `OkPerSecond`, and a worker's `Requests` is `Iterations`.

---

## 0.9.0 — no setup code in the test project

The MSTest adapter now starts TestFuzn before the first test and writes the reports after the last test.
It also adds the test project's entry point. `TestFuznIntegration` is gone.

**1. Remove the MSTest setup code** from your `Startup` class: the `[TestClass]` attribute and the
`[AssemblyInitialize]` and `[AssemblyCleanup]` methods that call `TestFuznIntegration`:

```csharp
// Before
[TestClass]
public class Startup : IStartup
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
        => await TestFuznIntegration.Init<Startup>(testContext);

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
        => await TestFuznIntegration.Cleanup(testContext);

    public void Configure(TestFuznConfiguration configuration) { }
}

// After
public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration) { }
}
```

**2. Move other code** from those methods to `IBeforeSuite` and `IAfterSuite` on the `Startup` class
(see [Lifecycle Hooks](docs/lifecycle.md)). These run under both runners.

**3. Delete `Program.cs`**, and remove these properties from the test project's `.csproj`:

```xml
<OutputType>Exe</OutputType>
<EnableMSTestRunner>true</EnableMSTestRunner>
<GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
```

`MSTest.Sdk` sets the first two, and the adapter package sets the third. To keep your own `Main`, add
`<TestFuznGenerateEntryPoint>false</TestFuznGenerateEntryPoint>` instead (see
[Test Runners](docs/test-runners.md#your-own-main)).

**4. Find the reports** in `TestFuznResults` in the test exe's folder, for example `bin/Debug/net10.0`.
The MSTest runner used to write them to `bin/TestFuznResults`.

---

## 0.9.0 — `.Run()` is removed

A scenario now runs when you `await` it, so `.Run()` is gone.

**1. Remove `.Run()`** from the end of each scenario chain:

```csharp
// Before
await Scenario()
    .Step("Step 1", context => { })
    .Run();

// After
await Scenario()
    .Step("Step 1", context => { });
```

**2. If you need a `Task`**, for example in a helper method or `Task.WhenAll`, make the method `async` and await the scenario:

```csharp
// Before
Task LoginFlow() => Scenario().Step("Log in", context => { }).Run();

// After
async Task LoginFlow() => await Scenario().Step("Log in", context => { });
```

A test that builds a scenario but never awaits it now fails, unless the scenario is passed to `Load().IncludeScenario(...)`.

---

## 0.9.0 — the scenario name can name a standard test

The name in `Scenario("...")` and `.Description()` were not used in a standard test. Now the test name is
the first one that is set: `[Test(Name = ...)]`, then the scenario name, then the method name. The
description works the same way (see [Test name](docs/scenarios.md#test-name)).

**1. Check the names in your reports.** A standard test that sets a scenario name but no `[Test(Name = ...)]`
now shows the scenario name, not the method name. Set `Id` to keep its history across the rename.

**2. Load tests:** a scenario with no name now gets the test name, not the raw method name. A scenario passed
to `Load().IncludeScenario(...)` must have a name.

---

## 0.9.0 — the InfluxDB sink is removed

Load tests can now run on a cluster of workers (see [Cluster Runs](docs/cluster-runs.md)).
Live graphs of a run come from the TestFuzn runner's live view, or from Cockpit through the Cockpit sink
built into TestFuzn. `Fuzn.TestFuzn.Sinks.InfluxDB` and the Grafana example are gone.

**1. Remove the package** from your test project:

```bash
dotnet remove package Fuzn.TestFuzn.Sinks.InfluxDB
```

**2. Remove** `configuration.UseInfluxDB()` and `using Fuzn.TestFuzn.Sinks.InfluxDB;` from your `Startup`.

**3. Remove the `TestFuzn:InfluxDB` section** from your `appsettings*.json` files.

**4. For live graphs**, run with `--runner=testfuzn` (see [Test Runners](docs/test-runners.md)), or send
the data to Cockpit: set `TESTFUZN_COCKPIT_URL`, `TESTFUZN_COCKPIT_API_KEY` and `TESTFUZN_COCKPIT_SOURCE`.
There is no package to add and nothing to call in `Startup` (see [Cockpit](docs/cockpit.md)).

---

## 0.7.6 — one test project, no separate runner project

Live console output used to need a second project. Your test project now does both jobs, so the
runner project goes away.

**1. Add this to your test project's `.csproj`:**

```xml
<GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
```

**2. Add a `Program.cs` to your test project:**

```csharp
using Fuzn.TestFuzn;

namespace MyProject.Tests;

internal static class Program
{
    public static Task<int> Main(string[] args) => TestFuznHost.Run<Startup>(args);
}
```

**3. Delete the runner project**, and remove it from your solution file.

**4. Run with live console output like this:**

```bash
dotnet run --project MyProject.Tests -- --runner=testfuzn
```

`dotnet test`, Test Explorer and CI do not change.

See [Test Runners](docs/test-runners.md).

---

[← Back to README](README.md)
