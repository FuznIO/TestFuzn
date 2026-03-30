# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TestFuzn ("testfusion") is a C# unified testing framework that combines unit tests, end-to-end tests, and load tests under a single fluent API. Currently in beta (v0.6.18-beta). Not yet stable -- backwards compatibility is not a concern.

## Build & Test Commands

```bash
# Build the solution
dotnet build src/TestFuzn.slnx

# Run tests (requires TestWebApp running first)
dotnet run --project src/TestWebApp/TestWebApp.csproj --launch-profile TestWebApp &
# Wait for it to respond, then run test suites sequentially:
dotnet test src/TestFuzn.Tests/TestFuzn.Tests.csproj
dotnet test src/TestFuzn.Tests.Attributes/TestFuzn.Tests.Attributes.csproj
dotnet test src/TestFuzn.Tests.DefaultHttpClient/TestFuzn.Tests.DefaultHttpClient.csproj

# Run a single test by filter
dotnet test src/TestFuzn.Tests/TestFuzn.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

TestWebApp listens on `https://localhost:7058` and `http://localhost:7059`.

## Target Framework

- **.NET 10.0** (`net10.0`), latest C# language version
- Nullable reference types enabled, implicit usings enabled
- Global build properties in `src/Directory.Build.props`

## Test Types

- **Standard Test** -- Validates correctness of a feature, optionally with input data driving iterations. Scenarios consist of steps (with support for nested sub-steps).
- **Load Test** -- Stresses the system with concurrent iterations using load simulations (FixedLoad, GradualLoadIncrease, etc.). Both types use the same `[Test]` attribute; a scenario becomes a load test when `.Load().Simulations(...)` is configured.

Results are collected in structured formats: XML, HTML reports, and optionally streamed to InfluxDB + Grafana for real-time dashboards.

## Architecture

### Producer-Consumer Execution Pipeline

The core execution model is a producer-consumer pattern via `BlockingCollection<T>`:

- **TestRunner** (`Internals/TestRunner.cs`) -- Orchestrates the lifecycle: Init -> Execute -> Cleanup -> Reports
- **ProducerManager** -- Generates `ExecuteScenarioMessage` items based on load simulation type (FixedLoad, GradualLoadIncrease, RandomLoadPerSecond, etc.) and enqueues them
- **ConsumerManager** -- Processes messages via `Parallel.ForEachAsync`, executing scenario iterations concurrently
- **ExecuteScenarioMessageHandler** -- Executes a single scenario iteration (runs steps, collects results)

### Fluent Builder API

Tests are built with `ScenarioBuilder<TModel>` which chains `.Step()`, `.InputData()`, `.Load().Simulations()`, and `.Run()`. Load simulations are configured via `SimulationsBuilder`.

### Plugin System

- **IContextPlugin** -- Per-iteration state management (HTTP client, Playwright browser, WebSocket). Methods: `InitSuite()`, `InitIteration()`, `HandleStepException()`, `CleanupIteration()`, `CleanupSuite()`
- **ISinkPlugin** -- Metrics/reporting sinks (InfluxDB). Methods: `InitSuite()`, `WriteStats()`, `CleanupSuite()`
- **ITestFrameworkAdapter** -- Abstracts test framework integration (MSTest today, extensible to others)

### Context Hierarchy

`Context` (base: logging, DI, config) -> `IterationContext` (shared data, input data, comments) -> `IterationContext<TModel>` (typed model per iteration)

### Test Framework Adapter Model

TestFuzn abstracts the test runner via `ITestFrameworkAdapter` so tests can run under different hosts:

- **MSTest runner** (`MsTestRunnerAdapter`) -- Good for standard tests and simple load tests. Also works for long-running tests, but MSTest does not support real-time console output during execution.
- **Standalone runner** (`BaseStandaloneRunnerAdapter`) -- Provides real-time console output, useful for complex/long-running load tests where live feedback matters.

This is why tests should avoid depending on MSTest-specific APIs (e.g. `TestContext`, MSTest assertions). Use TestFuzn's own abstractions (`Context`, `[Test]` attribute, etc.) so tests remain portable across MSTest, the standalone runner, and any future framework adapters.

### State & Cancellation

`TestExecutionState` owns the `CancellationTokenSource`. Setting `ExecutionStatus.Stopped` cascades cancellation through the entire producer-consumer pipeline. The token is sourced from the test framework adapter (MSTest's `TestContext.CancellationToken` or standalone runner's Ctrl+C handler).

## Solution Projects

| Project | NuGet Package | Purpose |
|---------|--------------|---------|
| TestFuzn | Fuzn.TestFuzn | Core framework |
| TestFuzn.Adapters.MSTest | Yes | MSTest integration, `Test` base class |
| TestFuzn.Plugins.Http | Yes | HTTP testing via Fuzn.FluentHttp |
| TestFuzn.Plugins.Playwright | Yes | Browser automation |
| TestFuzn.Plugins.WebSocket | - | WebSocket testing |
| TestFuzn.Sinks.InfluxDB | Yes | Real-time metrics to InfluxDB |

Test/sample projects: `TestFuzn.Tests`, `TestFuzn.Tests.Attributes`, `TestFuzn.Tests.DefaultHttpClient`, `TestFuzn.Tests.Runner`, `SampleApp.Tests`, `SampleApp.WebApp`, `TestWebApp`.

## Key Conventions

- Thread safety is critical in load test paths -- no shared mutable state between iterations
- Use `[Test]` attribute (TestFuzn's own), not raw MSTest attributes for test methods
- Use descriptive step/scenario names, never "Test1" or vague titles
- Avoid `Console.WriteLine` -- use the framework's logging via `Context.Logger`
- PR reviews should focus on core framework and plugins, not test projects or TestWebApp
- Use MSTest v4 assertion syntax. Prefer specific assertions over `Assert.IsTrue` with expressions:
  - **Comparison:** `Assert.IsLessThan`, `Assert.IsGreaterThan`, `Assert.IsLessThanOrEqualTo`, `Assert.IsGreaterThanOrEqualTo`, `Assert.IsInRange`
  - **Collections:** `Assert.Contains`, `Assert.DoesNotContain`, `Assert.IsEmpty`, `Assert.IsNotEmpty`, `Assert.HasCount`, `Assert.ContainsSingle`
  - **Strings:** `Assert.Contains`, `Assert.StartsWith`, `Assert.EndsWith`, `Assert.MatchesRegex` (prefer over `StringAssert`)
  - **Exceptions:** `Assert.ThrowsExactly<T>`, `Assert.ThrowsExactlyAsync<T>` (prefer over `[ExpectedException]`)
- When making changes, update relevant README/documentation files (e.g. `README.md`, `docs/*.md`) if the change affects documented behavior, APIs, or usage examples
