# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TestFuzn ("testfusion") is a C# unified testing framework that combines unit tests, end-to-end tests, and load tests under a single fluent API. Backwards compatibility is not a concern.

## Build & Test Commands

```bash
# Build the solution
dotnet build src/TestFuzn.slnx

# Run tests (TestWebApp and SampleApp.WebApp run in Kubernetes — do NOT start them locally)
dotnet test src/TestFuzn.Tests/TestFuzn.Tests.csproj
dotnet test src/TestFuzn.Tests.Attributes/TestFuzn.Tests.Attributes.csproj
dotnet test src/TestFuzn.Tests.DefaultHttpClient/TestFuzn.Tests.DefaultHttpClient.csproj

# Run a single test by filter
dotnet test src/TestFuzn.Tests/TestFuzn.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

**Always run `dotnet test src/TestFuzn.Tests/TestFuzn.Tests.csproj` before reporting a task as complete.** A clean build is not enough — the suite must pass.

`TestWebApp` and `SampleApp.WebApp` run as services in Kubernetes — the user manages those; don't try to `dotnet run` them. TestWebApp listens on `https://localhost:7058` and `http://localhost:7059`.

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
- **Standalone runner** (`BaseStandaloneRunnerAdapter`) -- Provides real-time console output: a full-screen live dashboard for load tests on an interactive ANSI terminal (plain per-second stats lines when output or input is redirected), an interactive test picker, and the final summaries, all rendered by the terminal engine below. Hosted by the test project itself (no separate runner project): the test project sets `GenerateTestingPlatformEntryPoint=false` and provides a custom `Main` that routes the `run` verb to `TestFuznHost.RunStandalone` and everything else to the MSTest runner. See `src/TestFuzn.Tests/Program.cs` and `docs/standalone-runner.md`. Run with `dotnet run --project src/TestFuzn.Tests -- run --test-name=<FullyQualifiedName>`; `dotnet run --project src/TestFuzn.Tests -- run --demo` plays a scripted synthetic load through the real dashboard with no Startup, config or target system.

This is why tests should avoid depending on MSTest-specific APIs (e.g. `TestContext`, MSTest assertions). Use TestFuzn's own abstractions (`Context`, `[Test]` attribute, etc.) so tests remain portable across MSTest, the standalone runner, and any future framework adapters.

### Terminal Rendering Engine

`src/TestFuzn/Internals/Terminal/` (namespace `Fuzn.TestFuzn.Internals.Terminal`, all internal) is an in-repo ANSI/VT rendering engine with no third-party console dependency -- `Fuzn.TestFuzn` depends only on HdrHistogram and `Microsoft.Extensions.*`.

- **Capabilities** -- `TerminalCapabilities` (`Detect`/`Resolve`: interactive = neither stdin nor stdout redirected; ANSI unless output is redirected, `TERM=dumb`, or Windows VT enablement fails; `ColorMode` None / Monochrome (`NO_COLOR`) / Colors16 / TrueColor (`COLORTERM`)). `SupportsLiveView` = interactive && ANSI gates every alternate-screen path. `AnsiCodes`, `WindowsVirtualTerminal`.
- **Frame pipeline** -- `FrameBuffer` + `FrameRenderer`: whole-line diff against the previous frame, DEC synchronized output, full redraw on resize. Callers own the alternate screen and pass the size they laid out for.
- **Markup** -- `MarkupParser`/`MarkupRenderer`/`MarkupText`: the `[bold green]...[/]` dialect used by `ConsoleWriter` and the adapters; unknown tags render literally, never throws.
- **Widgets and layouts** -- `PanelWidget`, `TableWidget`, `ProgressBarWidget`, `SparklineWidget`, `ChartWidget`, `HeatmapWidget`, `StatTileWidget` (v2, boxed into a row by `TileRowWidget`), `TimelineWidget`, `KeyHintBarWidget`, `LogoWidget` return `RenderedLine`s of known display width; `TerminalPalette` holds the shared styles (the status vocabulary, the logo gradient endpoints and their interpolation). `LiveDashboardLayout`, `TestSelectionMenuLayout`, `LoadSummaryLayout`, `AdvancedTableLayout`, `StartupBanner` and `ExceptionRenderer` are pure functions of their inputs (no console, no clock), so identical inputs render identical frames.
- **Live view** -- `ConsoleManager` (`Internals/ConsoleOutput/`) runs one loop (250 ms tick, 1 Hz force-refreshed collector samples into `ScenarioLiveMetrics`) driving either `LiveDashboard` (alternate screen; `q` calls `TestExecutionState.RequestStop()`, the same path as Ctrl+C) or `LiveStatsWriter` (plain lines, zero escapes). `TestSelectionMenu`, `LiveViewDemo` and `LiveViewDemoScript` live in `StandaloneRunner/`.
- **Seams** -- `ITerminalWriter`, `ITerminalReader`, `ILiveViewHost` (production: `ConsoleTerminalWriter`, `ConsoleTerminalReader`, `ConsoleLiveViewHost`). These and `TerminalCapabilities.Detect` are the engine's only `System.Console` touchpoints; everything else takes the seam, so it runs without a TTY. The terminal size is read only on an ANSI output and keys only behind `SupportsLiveView` (a redirected output fabricates a size, a console-less Windows process throws, a redirected input throws on a key read).
- **Tests** -- `src/TestFuzn.Tests/Terminal/` are hermetic golden tests over `FakeTerminalWriter`/`FakeTerminalReader`/`FakeLiveViewHost` (no TestWebApp, InfluxDB or Playwright): `dotnet test src/TestFuzn.Tests/TestFuzn.Tests.csproj -- --filter "FullyQualifiedName~Terminal"`. `run --demo` is the dev loop for the dashboard's looks.

### State & Cancellation

`TestExecutionState` owns the `CancellationTokenSource`. Setting `ExecutionStatus.Stopped` cascades cancellation through the entire producer-consumer pipeline. The token is sourced from the test framework adapter (MSTest's `TestContext.CancellationToken` or standalone runner's Ctrl+C handler).

## Solution Projects

| Project | NuGet Package | Purpose |
|---------|--------------|---------|
| TestFuzn | Fuzn.TestFuzn | Core framework |
| TestFuzn.Adapters.MSTest | Yes | MSTest integration, `Test` base class |
| TestFuzn.Plugins.Http | Yes | HTTP testing via Fuzn.FluentHttp |
| TestFuzn.Plugins.Playwright | Yes | Browser automation |
| TestFuzn.Plugins.WebSocket | Yes | WebSocket testing |
| TestFuzn.Sinks.InfluxDB | Yes | Real-time metrics to InfluxDB |

Test/sample projects: `TestFuzn.Tests`, `TestFuzn.Tests.Attributes`, `TestFuzn.Tests.DefaultHttpClient`, `TestFuzn.Tests.CustomHttpClient`, `TestFuzn.Tests.Failing`, `SampleApp.Tests`, `SampleApp.WebApp`, `TestWebApp`.

## Key Conventions

- Thread safety is critical in load test paths -- no shared mutable state between iterations
- Use `[Test]` attribute (TestFuzn's own), not raw MSTest attributes for test methods
- Use descriptive step/scenario names, never "Test1" or vague titles
- Avoid `Console.WriteLine` -- use the framework's logging via `Context.Logger`. Console output in the framework goes through the `ITestFrameworkAdapter` or the terminal engine's `ITerminalWriter`/`ILiveViewHost` seams, never `System.Console` directly (see Terminal Rendering Engine)
- PR reviews should focus on core framework and plugins, not test projects or TestWebApp
- Use MSTest v4 assertion syntax. Prefer specific assertions over `Assert.IsTrue` with expressions:
  - **Comparison:** `Assert.IsLessThan`, `Assert.IsGreaterThan`, `Assert.IsLessThanOrEqualTo`, `Assert.IsGreaterThanOrEqualTo`, `Assert.IsInRange`
  - **Collections:** `Assert.Contains`, `Assert.DoesNotContain`, `Assert.IsEmpty`, `Assert.IsNotEmpty`, `Assert.HasCount`, `Assert.ContainsSingle`
  - **Strings:** `Assert.Contains`, `Assert.StartsWith`, `Assert.EndsWith`, `Assert.MatchesRegex` (prefer over `StringAssert`)
  - **Exceptions:** `Assert.ThrowsExactly<T>`, `Assert.ThrowsExactlyAsync<T>` (prefer over `[ExpectedException]`)
- When making changes, update relevant README/documentation files (e.g. `README.md`, `docs/*.md`) if the change affects documented behavior, APIs, or usage examples
- Test methods that are expected to throw an exception or fail should be prefixed with `ShouldFail_` (e.g. `ShouldFail_Verify_assert_while_running`)
- For null checks, use explicit `if` checks -- not null-conditional (`?.`), null-coalescing (`??`), or null-coalescing assignment (`??=`) operators
- Consistent terminology matters. Before naming new properties, classes, methods, or parameters, search the existing codebase to see what terms are already used for similar concepts. Use the same words -- e.g. if the codebase uses `Duration` for time spans, don't introduce `Elapsed` or `TimeSpan` for the same idea. This applies to naming at all levels: public API, internals, tests, and documentation.
