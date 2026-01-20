# API Reference

## ScenarioBuilder Methods

| Method | Description |
|--------|-------------|
| `Id(string)` | Set scenario ID |
| `BeforeScenario(Action/Func)` | Add scenario initialization hook |
| `AfterScenario(Action/Func)` | Add scenario cleanup hook |
| `BeforeIteration(Action/Func)` | Add iteration initialization hook |
| `AfterIteration(Action/Func)` | Add iteration cleanup hook |
| `InputData(params object[])` | Provide static input data |
| `InputDataFromList(Func)` | Provide input data from function |
| `InputDataBehavior(InputDataBehavior)` | Set input data consumption behavior |
| `Step(string, Action/Func)` | Add a step |
| `Step(string, string, Action/Func)` | Add a step with ID |
| `Load()` | Access load testing configuration |
| `Run()` | Execute the scenario |

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
| `Comment(string)` | Add comment to execution |
| `Attach(string, content)` | Attach file to step |
| `CreateHttpRequest(string)` | Create HTTP request (requires HTTP plugin) |
| `CreateBrowserPage()` | Create Playwright browser page (requires Playwright plugin) |

---

## Load Testing Methods

| Method | Description |
|--------|-------------|
| `Warmup(Action)` | Configure warmup simulations |
| `Simulations(Action/Func)` | Configure load simulations |
| `AssertWhileRunning(Action)` | Add runtime assertions |
| `AssertWhenDone(Action)` | Add post-execution assertions |
| `IncludeScenario(ScenarioBuilder)` | Include an additional scenario to execute in parallel with the main scenario. Multiple calls add multiple scenarios. |

---

## Simulation Types

| Method | Description |
|--------|-------------|
| `OneTimeLoad(count)` | Execute N iterations |
| `GradualLoadIncrease(start, end, duration)` | Ramp load gradually |
| `FixedLoad(rate, duration)` | Constant requests/second |
| `FixedConcurrentLoad(count, duration)` | Constant concurrent users |
| `RandomLoadPerSecond(min, max, duration)` | Random load in range |
| `Pause(duration)` | Pause between simulations |

---

[← Back to Table of Contents](README.md)
