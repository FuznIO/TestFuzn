# Standalone Runner

The MSTest runner works well for standard tests, CI, and Test Explorer, but MSTest does not support real-time console output during execution. The standalone runner hosts tests in the test project's own executable and provides live feedback — a full-screen dashboard with live metrics while a load test runs, and a summary when it ends — which matters for complex or long-running load tests. It renders with TestFuzn's own terminal engine, so the `Fuzn.TestFuzn` package has no third-party console dependency.

No separate runner project is needed: the test project doubles as the runner through a custom entry point.

---

## Setup

### 1. Disable the generated entry point

The MSTest runner normally generates the `Main` method for the test project. Disable that so the project can provide its own:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <EnableMSTestRunner>true</EnableMSTestRunner>
  <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
</PropertyGroup>
```

### 2. Add a Program.cs

The entry point routes the `run` verb to the standalone runner; everything else goes to the MSTest runner as before:

```csharp
using Microsoft.Testing.Platform.Builder;

namespace MyProject.Tests;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (TestFuznHost.IsStandaloneRun(args))
            return await TestFuznHost.RunStandalone<Startup>(typeof(Program).Assembly, args);

        var builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddSelfRegisteredExtensions(args);
        using var app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}
```

`AddSelfRegisteredExtensions` is source-generated into the test project by the MSTest runner and registers extensions such as TRX reports and code coverage, exactly like the generated entry point would.

`dotnet test`, Visual Studio/Rider/VS Code Test Explorer, and CI runs are unaffected — they never pass the `run` verb, so they continue through the MSTest runner.

---

## Running Tests

Run the test project with the `run` verb to enter standalone mode:

```bash
# Interactive test selection menu
dotnet run --project MyProject.Tests -- run

# Run a specific test (fully qualified class name + method name)
dotnet run --project MyProject.Tests -- run --test-name=MyProject.Tests.ProductLoadTests.Load_products_endpoint

# Play the live dashboard with a scripted synthetic load (no Startup, config or target system needed)
dotnet run --project MyProject.Tests -- run --demo
```

The test name can also be provided via the `TESTFUZN_TEST_NAME` environment variable, which is useful in containers:

```bash
TESTFUZN_TEST_NAME=MyProject.Tests.ProductLoadTests.Load_products_endpoint \
  dotnet run --project MyProject.Tests -- run
```

A `--test-name` that names no test prints `Test '<name>' not found.` and exits with code 1. A bare `--test-name` without a value prints `--test-name requires a value: --test-name=<FullyQualifiedName>` and exits with code 1.

### Test Selection Menu

With no test named, the runner discovers the assembly's tests and shows a full-screen selection menu: the TestFuzn logo on top, the numbered test list with the highlighted test marked `▸`, a `Filter:` line with the match count, and a key-hint footer.

| Key | Action |
|-----|--------|
| `↑` / `↓` | Move the highlight (stops at the ends) |
| `PgUp` / `PgDn` | Move by a page |
| `Home` / `End` | Jump to the first / last test |
| Any text | Filter the list — a case-insensitive substring match on the full test name, underlined in each match |
| A test number | Jumps the highlight to that test (the list stays complete), so typing a number and pressing `Enter` runs it |
| `Backspace` | Remove the last filter character |
| `Enter` | Run the highlighted test |
| `Esc` or `Ctrl+C` | Quit without running anything (exit code 0) |

`Alt` and `Ctrl` chords are ignored. The full six-row logo needs a terminal at least 80 columns wide and 27 rows tall; a shorter terminal gets the compact `⚡ TestFuzn` wordmark, and a narrower one no logo.

Without an interactive ANSI terminal (see [Terminal Requirements](#terminal-requirements)) the menu falls back to a plain numbered list (`ID  Test Name`) and a prompt: enter a test's number to run it, or an empty line (or end of input) to quit with exit code 0. Anything else prints `Invalid test index.` and prompts again.

### Startup Banner

Every run opens with three lines on the normal screen — before the dashboard starts, so they stay in the scrollback above it:

```
⚡ TestFuzn
Running test: MyProject.Tests.ProductLoadTests.Load_products_endpoint
Assembly: MyProject.Tests · Target environment: -
```

The target environment is read from `TESTFUZN_TARGET_ENVIRONMENT` (`-` when unset). On a redirected or non-ANSI output the banner is plain text.

### Live Dashboard

While a load test runs on an interactive ANSI terminal, the runner switches to the terminal's alternate screen, hides the cursor and renders a full-screen dashboard at about four frames per second over metrics sampled once a second. Each scenario gets a section:

- **Title line** — the scenario name and a status badge (`Running` with a spinner, then `Passed`, `Failed` or `Skipped`), with the compact logo top-right.
- **Timing line** — elapsed and planned duration, a progress bar with percentage, the ETA, and the current phase (`warmup: Fixed Load 10 rps`, `sim 2/2: Fixed Load 80 rps`, `cleanup`). A plan with a count-based simulation (`OneTimeLoad`, or `FixedConcurrentLoad` with a total count) has no planned duration, so only the elapsed time is shown. When an assertion fails, its reason is shown below this line.
- **`rps` and `p95` sparklines** — the current-interval request rate and the per-second p95 of successful requests, newest at the right, each headed by its latest value.
- **Requests** — `ok` and `failed` rows with count, current rate and the response-time spread (min, mean, p50, p75, p95, p99, max). Warmup counts are shown on their own line and excluded from the totals.
- **Steps** — one row per step: count, current rate, mean, p95, failed count and a fail% mini-bar.
- **Errors** — the most recent distinct errors with their counts and the step they occurred in.
- **Footer** — `q quit`.

Multi-scenario tests stack one section per scenario. The layout adapts to the terminal width: the logo is dropped below 80 columns and the sparklines below 60, and the Requests table drops percentile columns rather than cutting numbers short. When the test completes, is stopped or fails, the terminal is always restored and the summary is printed to the normal screen, so it lands in the scrollback. If the live view itself fails, the terminal is restored at once, the run continues without it, and the failure is printed after the summary.

Standard (non-load) tests have no live view — they print their result table when they finish.

### Stopping a Run

Press `q` on the dashboard, or `Ctrl+C` at any time, to stop a running test gracefully: the load producers stop, in-flight iterations and the cleanup hooks complete, the summary is written for the completed iterations, and the runner prints `Run stopped (OperationCanceledException): the run was cancelled by Ctrl+C or the quit key before it completed.` and exits with code 1.

### Output Without a Terminal

When standard output is redirected (`docker logs`, a CI log, a pipe), the terminal has no ANSI support, or standard input is redirected (`< /dev/null`, as several CI runners do), the runner writes no escape sequences at all. Instead of the dashboard it prints one stats line per scenario every second, a line for each phase change, and a final line with the outcome:

```
[Checkout flow] phase: warmup: Fixed Load 10 rps
[Checkout flow] elapsed 00:00:03 / 00:00:17  total 0  ok 0  failed 0  rps 10.0  p95 N/A
[Checkout flow] phase: sim 1/2: Gradual Load 10→80 rps
[Checkout flow] elapsed 00:00:08 / 00:00:17  total 105  ok 104  failed 1  rps 40  p95 513 ms
[Checkout flow] phase: cleanup
[Checkout flow] completed  elapsed 00:00:19  total 840  ok 807  failed 33  p95 519 ms
```

- `elapsed hh:mm:ss / planned` — the planned duration is omitted when the plan has no fixed duration.
- `total`, `ok`, `failed` — measurement requests only; warmup is excluded, as on the dashboard.
- `rps` — the current interval's rate (warmup traffic included), `N/A` before the first interval has closed.
- `p95` — the cumulative p95 of successful requests, the same number the summary reports; `N/A` before the first successful request.
- `[name] failed: <reason>` is written once when an assertion fails.
- The final line reads `completed`, `stopped` (`Ctrl+C`, `q`, or an assertion that stopped the run), `failed` or `skipped`, followed by the totals and, when there is one, `reason: <detail>`.

Phase changes are observed at the one-second sampling cadence, so a phase shorter than a second gets no line. The summary follows the final line.

### Final Summary

After a load test, the summary is printed as a column of panels per scenario:

- **Load Test Summary** — scenario, execution time, test run time and status.
- **Load Simulations** — every configured simulation, warmup ones marked.
- **Global Metrics** — the **Scenario Requests** table (total, ok and failed counts with their rates) beside the **Response Times** table (min, mean, max, standard deviation, median, p75, p95, p99, for ok and failed requests).
- **Step … Details** — the same two tables per step; the **Step Requests** table adds a **Skipped** row, since iterations that fail at an earlier step never reach the later ones.
- **Errors by Step** — every distinct error message with its count, when any step recorded errors.

Numbers are never cut short: on a narrow terminal the two tables stack, and the response-time spread splits into smaller tables. The summary is laid out at the terminal's width on an ANSI terminal at least 40 columns wide (and wide enough to show every number), and at 120 columns otherwise, in which case the terminal wraps it. Standard tests print their result table the same way.

A run that fails prints the exception with its stack trace, `Caused by:` chains and numbered aggregate inner exceptions — styled on an ANSI terminal, plain otherwise.

### Demo Mode

`run --demo` plays a scripted synthetic load run through the real dashboard: about a second of init, a 3 s warmup at 10 rps, an 8 s ramp from 10 to 80 rps, 6 s of steady load at 80 rps with three kinds of errors, and a second of cleanup — about 20 seconds in all, followed by the summary. It needs no `Startup`, `appsettings`, InfluxDB or target system, writes no reports, and exits with code 0; `q` and `Ctrl+C` stop it like a real run (exit code 1). With redirected output it prints the plain stats lines instead. Use it to check how the dashboard looks in your terminal.

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Test passed (or was skipped), the demo completed, or the selection menu was quit |
| 1 | Test failed, the run was stopped (`q` or `Ctrl+C`), the test name was not found, or the arguments were invalid |

### Results

Reports are written to the `TestFuznResults` folder next to the test assembly (the project's build output directory), the same location the MSTest adapter uses.

---

## Terminal Requirements

The dashboard and the selection menu need an **interactive ANSI terminal**: both standard input and standard output attached to a terminal, and `TERM` not set to `dumb`. Anything else — redirected output, redirected input, a dumb terminal — gets the plain-text behavior described above, with zero escape bytes.

- **Colors** — 24-bit color when `COLORTERM` is `truecolor` or `24bit`, the 16-color palette otherwise. Setting `NO_COLOR` keeps the layout and text decorations (bold, dim, underline) but drops all colors.
- **Glyphs** — the dashboard, menu and summary use UTF-8 box-drawing characters and braille sparklines (the runner switches the console output encoding to UTF-8 itself); the terminal font must be able to show them.
- **Windows** — virtual terminal processing is enabled automatically (Windows 10 version 1511 or later, Windows Terminal included). A process without a console window, or with redirected output, gets the plain output.
- **`timeout`** — when wrapping the runner in coreutils `timeout`, use `timeout --foreground`; without it the process is not allowed to read the terminal and is stopped as soon as the dashboard polls for keys.

### MSTest-Hosted Runs

Under `dotnet test` and Test Explorer nothing changes: there is no live view, the summary goes to MSTest's test output as plain ASCII tables, and markup is stripped from every line. Note that the stripping removes any `[...]`-bracketed text, so bracketed data in an assertion message does not appear in the MSTest output. A run stopped without a reason prints `Status: Stopped`.

---

See [Load Testing](load-testing.md) for simulations and assertions, and [InfluxDB & Grafana](influxdb-grafana.md) for streaming live metrics to dashboards while a load test runs.

---

[← Back to Table of Contents](README.md)
