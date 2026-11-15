# API Reference

## ScenarioBuilder Methods

| Method | Description |
|--------|-------------|
| `Id(string)` | Set scenario ID |
| `Description(string)` | Set scenario description, shown in reports. In a standard test it is the test description, unless `[Test(Description = ...)]` is set |
| `BeforeScenario(Action/Func)` | Add scenario initialization hook |
| `AfterScenario(Action/Func)` | Add scenario cleanup hook |
| `BeforeIteration(Action/Func)` | Add iteration initialization hook |
| `AfterIteration(Action/Func)` | Add iteration cleanup hook |
| `InputData(params object[])` | Provide static input data |
| `InputDataFromList(Func)` | Provide input data from function |
| `InputDataBehavior(InputDataBehavior)` | Set input data consumption behavior |
| `Step(Action/Func)` | Add a step with auto-generated name |
| `Step(string, Action/Func)` | Add a step |
| `Step(string, string, Action/Func)` | Add a step with ID |
| `Load()` | Access load testing configuration |
| `await` | Run the scenario: `await Scenario().Step(...)` |

---

## IterationContext Members

| Member | Description |
|--------|-------------|
| `Model` | Access custom context model |
| `Logger` | ILogger instance for logging |
| `InputData<T>()` | Get input data for current iteration |
| `SetSharedData(string, object)` | Store shared data |
| `GetSharedData<T>(string)` | Retrieve shared data |
| `Step(string, Action/Func)` | Create sub-step |
| `Cleanup(Action/Func)` | Register cleanup action for end of iteration |
| `Comment(string)` | Add comment to execution |
| `Attach(string, content)` | Attach file to step |
| `CreateHttpRequest(string)` | Create HTTP request (requires HTTP plugin) |
| `CreateWebSocketConnection(string, Action<FluentWebSocketRequest>?)` | Create and connect a WebSocket connection with optional configuration (requires WebSocket plugin) |
| `CreateBrowserPage(browserType?, device?, configureBrowserContext?)` | Create Playwright browser page with optional browser type, device emulation, and context options (requires Playwright plugin) |
| `GetPlaywright()` | Get the Playwright instance for direct API access such as device descriptors (requires Playwright plugin) |
| `Files` | Access the `FileManager` for loading test data from CSV and JSON files |
| `AppConfiguration` | Access the `AppConfigurationManager` for reading values from `appsettings.json` |
| `ServicesProvider` | Access the `IServiceProvider` for resolving dependencies from the IoC container |

---

## GlobalState Members

| Member | Description |
|--------|-------------|
| `TargetEnvironment` | The target environment the tests are executing against |
| `ExecutionEnvironment` | The execution environment where tests are running |
| `TagsFilterInclude` | Tags to include when filtering tests |
| `TagsFilterExclude` | Tags to exclude when filtering tests |
| `AppConfiguration` | Access the `AppConfigurationManager` for reading values from `appsettings.json` |
| `NodeName` | The name of the machine where the tests are running |
| `ServiceProvider` | The `IServiceProvider` for resolving dependencies registered during test initialization |
| `Logger` | The `ILogger` instance used for writing test log output |
| `TestsResultsDirectory` | The directory where test results and artifacts are written |
| `TestRunId` | The unique identifier for the current test run |

---

## FileManager Methods

| Method | Description |
|--------|-------------|
| `LoadFromCsv<T>(string path)` | Load records from a CSV file, mapping columns to properties of `T` by header name |
| `LoadFromJson<T>(string path)` | Load records from a JSON file, deserializing contents as a list of `T` |

---

## Load Testing Methods

| Method | Description |
|--------|-------------|
| `Warmup(Action)` | Configure warmup simulations |
| `Simulations(Action/Func)` | Configure load simulations |
| `AssertWhileRunning(Action)` | Add runtime assertions |
| `AssertWhenDone(Action)` | Add post-execution assertions |
| `BeforeScenarioOnce(Func)` | Run once before the load starts; the returned data is read with `context.OnceData<T>()` |
| `AfterScenarioOnce(Func/Action)` | Run once after the load, also after a stop or failure |
| `IncludeScenario(ScenarioBuilder)` | Include an additional scenario to execute in parallel with the main scenario. Multiple calls add multiple scenarios. |

---

## Cluster

| Member | Description |
|--------|-------------|
| `context.OnceData<T>()` | The data `BeforeScenarioOnce` returned |
| `context.Info.WorkerIndex` / `WorkerCount` | This load worker's index and the number of workers (0 and 1 on one machine) |

| Argument | Description |
|----------|-------------|
| `--cluster` | Run the load test on the pool's workers (or `TESTFUZN_CLUSTER=true`) |
| `--run-name="..."` | A readable name on Cockpit's cluster page |

---

## Simulation Types

| Method | Description |
|--------|-------------|
| `OneTimeLoad(count)` | Starts `count` iterations all at once |
| `GradualLoadIncrease(start, end, duration)` | Changes the rate per second evenly from `start` to `end`; a lower `end` ramps down |
| `FixedLoad(rate, duration)` | Starts `rate` iterations per second |
| `FixedLoad(rate, interval, duration)` | Starts `rate` iterations per interval |
| `FixedConcurrentLoad(count, duration)` | Keeps `count` iterations running at the same time (virtual users) |
| `RandomLoadPerSecond(min, max, duration)` | Starts a random number per second, between `min` and `max` |
| `Pause(duration)` | Starts nothing |

---

[← Back to Table of Contents](README.md)
