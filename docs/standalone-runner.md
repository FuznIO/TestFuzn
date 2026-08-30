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
dotnet run --project MyProject.Tests -- run --demo --demo-duration=120
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

- **Title line** — the scenario name and a status badge (`Running` with a spinner, then `Passed`, `Failed` or `Skipped`), with the compact logo top-right. When the section shows no timeline, the current phase follows the badge after a middle dot. A `Failed` status is never reason-less: the reason renders as its own line under the timeline.
- **Tile row** — boxed KPIs for the newest closed interval: `rps`, `p95`, `errors` (the interval's failed share), `requests` (the measurement total, with `warmup n` on the unit line once warmup requests exist) and `elapsed` (the run clock, the planned total after a slash, and a progress gauge ending in the time remaining). The `rps` and `p95` tiles carry a signed delta against the sample ten seconds back and a sparkline trend of the last 60 samples. A declared `rps`, `p95` or error-rate threshold turns that tile into a gauge — the live reading against the limit, green while it holds, yellow inside the warning band, red once breached; a `mean` or `p99` threshold, which has no tile of its own, adds one. Without a declared threshold the `errors` and `p95` tiles colour themselves heuristically instead.
- **Timeline** — the configured simulations as one bar of segments with a marker at the current position, over a legend line naming them. The marker's segment *is* the phase, which is why the phase label is only written separately when a plan gives the marker no position. A plan with any count-based simulation (`OneTimeLoad`, or `FixedConcurrentLoad` with a total count) has no planned duration, so it is indeterminate.
- **Charts** — `requests — ok / failed`, the per-interval ok and failed counts as two areas on one count scale, and `latency — p99 / p95 / p50`, the per-interval p99 and p95 as bands under the median as a line. Each panel's header is its legend, naming the series in paint order; the first name is the series whose newest value the `▶` annotation at the right of the plot shows. A declared p95 or p99 threshold draws its limit as a flat line across the latency chart, and the panel title names it (`· limit 500 ms`). An interval with no successful request is a gap in the bands, never a dip to zero.
- **Latency heatmap** — a full-width panel under the charts: one column per interval, newest at the right, one row per response-time bucket with the slowest on top, each cell shaded by how many requests landed there. There are fifteen buckets, labelled by their upper bounds — `≤ 1 ms` up to `≤ 30 s`, and an open-ended `> 30 s` — and adjacent buckets merge into one row, from the middle outward, to fit the rows the panel is given.
- **Requests** — `ok` and `failed` rows with count, current rate and the response-time spread (min, mean, p50, p75, p95, p99, max). Warmup counts are shown on their own line and excluded from the totals.
- **Steps** — one row per step, sorted by pain: failure share descending, then the newest interval p95 descending, so the rows a shrinking window keeps are the ones that hurt. The columns are the step name, count, rps, mean, p95, failed, a `fail%` bar and a rate trend sparkline. The selected step's row is marked `▸` and highlighted.
- **Errors** — the ticker: the distinct errors, most recently active first, each with its count, its current rate, the step it occurred in, the message and how long ago it was first and last seen.
- **Footer** — the key hints for the current view, always ending in `q quit`.

When the test completes, is stopped or fails, the terminal is always restored and the summary is printed to the normal screen, so it lands in the scrollback. If the live view itself fails, the terminal is restored at once, the run continues without it, and the failure is printed after the summary.

Standard (non-load) tests have no live view — they print their result table when they finish.

#### Views and Keys

The dashboard has three views and a help overlay. Keys act on the next frame; `Alt` and `Ctrl` chords are ignored.

| Key | Action |
|-----|--------|
| `1` | Overview — the sections above |
| `2` or `Enter` | Step detail for the selected step (selects the top row when nothing is selected; does nothing when the scenario has no steps) |
| `3` | Error log |
| `↑` / `↓` | Move the step selection in the overview and the step detail; scroll the error log one entry at a time in the log view |
| `PgUp` / `PgDn` | Scroll the error log by ten entries (log view only) |
| `Esc` | Close the help if it is up, otherwise return to the overview |
| `p` | Pause / resume the picture |
| `+` / `-` | Widen / narrow the charts' time window |
| `?` | Toggle the help overlay |
| `q` | Stop the run (see [Stopping a Run](#stopping-a-run)) |

**Step detail** replaces the overview with one full-width section for the first scenario's selected step: the scenario title line, a `step 2/3 · Add to cart · Esc back` header, four tiles (`rps`, `p95`, `fail%`, `count`, the last carrying `skipped n` once iterations were skipped), the step's own requests and latency charts, and the ticker entries recorded against that step. The selection is keyed to the step, not to its row, so it follows the step as the pain sort reshuffles the table.

**Error log** replaces the overview with the first scenario's errors as one scrollable log, grouped by step under `▸ <step>` headers, in the ticker's order. The header line — `errors · 3–14 of 27 · Esc back` — counts the entries on the page and in the log, and adds `+N unlisted` when more distinct errors have been recorded than the model keeps. Unlike the ticker, log entries are not capped at three lines, so a long message is shown whole.

**Pause** (`p`) freezes the picture, not the sampling: the run keeps going and the model keeps recording, but the frame holds the numbers as they were when the pause began, under a `⏸ paused` badge next to the status. Keys still work against the frozen picture; resuming jumps to now.

**Time window** (`+` / `-`) sets how many of the newest samples the charts and the heatmap show. The ladder is 60 → 120 → 300 → every sample, and it does not wrap: `+` at every sample and `-` at 60 stay where they are. The default is every sample; the tiles' deltas and trends keep their own fixed windows either way.

**Help** (`?`) composes a panel of every binding over whatever view is up. It is not modal — the other keys still work — and `?` or `Esc` closes it.

#### Side-by-Side Scenarios

Two or more scenarios render as columns from 122 columns of width, and three abreast from 184 with three or more (never more than three); below that, or with one scenario, the sections stack with a blank line between them. Each column is the full section laid out at the column's width, so its pieces drop by that width and not the window's — the tiles wrap, the charts stack, the ticker sheds its ages. A column never shows the heatmap whatever the height, its rows being what a column cannot spare. With more scenarios than columns, the next row of columns starts under the first. The logo rides the top-right of the frame and the footer belongs to the frame, not to a column.

#### Terminal Size

No line ever exceeds the terminal's width, and the footer always owns the last row. As the window shrinks, pieces go in a fixed order rather than being cut short.

By **width**: the logo, the tile trends and the error ticker's ages go below 80 columns; the five standard tiles wrap onto two rows below 64; the chart panels stop sitting side by side below 100 and stack instead; the ticker's rates go below 60; and below 60 the charts and the heatmap go altogether. The requests and steps tables narrow by dropping whole columns — `min`, `p75` and `p99` first, then `p50` and `max`; the trend first, then `mean` and `failed` — so a number is never truncated into a smaller-looking number.

By **height**: the heatmap needs 36 rows; below 30 the chart bodies shrink from 6 rows to 4 and the tile trends go; below 20 the tile gauges go; below 15 the timeline goes and the phase moves onto the title line. A section that still overruns its budget then gives rows back in this order: the heatmap body a row at a time (down to 4, merging more buckets per row), the heatmap panel whole, the chart bodies compacted, step rows (least painful first, down to one), error entries (least recently active first, down to one), the latency chart, the requests chart, the Steps panel, the Errors panel, and finally the frame is clipped from the requests panel's bottom border up. The footer is never given back. Hidden rows are never silent: a table that hides rows says `+N more`, and a panel that cannot keep even one row is dropped whole instead.

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
[Checkout flow] completed  elapsed 00:00:19  total 840  ok 807  failed 33  p95 519 ms  thresholds: ok
```

- `elapsed hh:mm:ss / planned` — the planned duration is omitted when the plan has no fixed duration.
- `total`, `ok`, `failed` — measurement requests only; warmup is excluded, as on the dashboard.
- `rps` — the current interval's rate (warmup traffic included), `N/A` before the first interval has closed.
- `p95` — the cumulative p95 of successful requests, the same number the summary reports; `N/A` before the first successful request.
- `[name] failed: <reason>` is written once when an assertion fails.
- The final line reads `completed`, `stopped` (`Ctrl+C`, `q`, or an assertion that stopped the run), `failed` or `skipped`, followed by the totals, then `thresholds: ok` or `thresholds: breached (n)` and, when there is one, `reason: <detail>`.
- The `thresholds` field is left out entirely when the scenario has no verdict to report — it declared none, or the run was stopped before the verdict was taken. A stopped run is never judged.

Phase changes are observed at the one-second sampling cadence, so a phase shorter than a second gets no line. The summary follows the final line.

### Final Summary

After a load test, the summary is printed as a column of panels per scenario:

- **Load Test Summary** — scenario, execution time, test run time and status.
- **Load Simulations** — every configured simulation, warmup ones marked.
- **Global Metrics** — the **Scenario Requests** table (total, ok and failed counts with their rates) beside the **Response Times** table (min, mean, max, standard deviation, median, p75, p95, p99, for ok and failed requests).
- **Thresholds** — right after the metrics it is read from: one row per declared threshold in declaration order, with the metric (`p95`, `error rate`, …), the **Limit** it was declared with (`≤ 500 ms`, `≥ 50`), the **Actual** cumulative value at completion, and `✓` or `✗`. The panel is only rendered for a scenario that has a verdict, so one that declared no threshold, and one whose run was stopped before the verdict was taken, has none. See [Thresholds](load-testing.md#thresholds).
- **Step … Details** — the same two tables per step; the **Step Requests** table adds a **Skipped** row, since iterations that fail at an earlier step never reach the later ones.
- **Errors by Step** — every distinct error message with its count, when any step recorded errors.

Numbers are never cut short: on a narrow terminal the two tables stack, and the response-time spread splits into smaller tables. The summary is laid out at the terminal's width on an ANSI terminal at least 40 columns wide (and wide enough to show every number), and at 120 columns otherwise, in which case the terminal wraps it. Standard tests print their result table the same way.

A run that fails prints the exception with its stack trace, `Caused by:` chains and numbered aggregate inner exceptions — styled on an ANSI terminal, plain otherwise.

### Demo Mode

`run --demo` plays a scripted synthetic load run through the real dashboard, so you can see how it looks in your terminal — and how it behaves under an incident — without a `Startup`, `appsettings`, InfluxDB or target system. It writes no reports and exits with code 0; `q` and `Ctrl+C` stop it like a real run (exit code 1). With redirected output it prints the plain stats lines instead.

```bash
# 60 seconds, the default
dotnet run --project MyProject.Tests -- run --demo

# A shorter or longer run; the same shape plays out at any duration
dotnet run --project MyProject.Tests -- run --demo --demo-duration=120
```

`--demo-duration` takes whole seconds and is at least 20. A value that is not — a fraction, text, a bare `--demo-duration`, or anything under 20 — prints `--demo-duration takes whole seconds, at least 20: --demo-duration=<seconds>` and exits with code 1.

The run is one scenario, **Checkout flow (demo)**, with the steps *Browse products*, *Add to cart* and *Place order*. Init and cleanup take a fixed second each; the rest is split between three simulations — 8 % warmup at 10 rps, 25 % ramping from 10 to 100 rps, and the remaining 67 % steady at 100 rps — so the same shape plays out whatever the duration. Response times grow with the load and wobble under per-request jitter, and every 200th measurement iteration fails at one of the three steps — only once the run has reached the steady phase, and cycling through three distinct errors, so the ticker has something in it while the run is otherwise healthy.

Two fifths into the steady phase a scripted incident starts, which is what gives the charts, the heatmap and the threshold tiles a shape to show:

- *Place order* starts taking four times as long for one iteration in five — a partly degraded dependency — over the next 15 % of the steady phase;
- for the opening third of that window (5 % of the steady phase) one iteration in four fails it outright with `HTTP 503 Service Unavailable`;
- then both recover.

The scenario declares thresholds of `p95 ≤ 500 ms` and `error rate ≤ 2 %`, chosen so that the incident breaches them *live* — the tiles turn red and the latency chart's bands climb past the limit line — while the cumulative statistics over the whole measurement stay inside them. So the completion verdict passes, the **Thresholds** panel closes with two `✓`, and `run --demo` exits 0.

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Test passed (or was skipped), the demo completed, or the selection menu was quit |
| 1 | Test failed — a step, an assertion or a violated threshold — the run was stopped (`q` or `Ctrl+C`), the test name was not found, or the arguments were invalid |

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

A scenario's threshold verdict is written there too, under a `Thresholds:` heading after the metrics, as a `Metric` / `Limit` / `Actual` / `Result` table — with the same content as the standalone runner's panel, but in plain ASCII: the relation reads `<=` and `>=` rather than `≤` and `≥`, and the result is the word `Ok` or `Breached` rather than a glyph. As in the standalone summary, nothing is written for a scenario without a verdict.

---

See [Load Testing](load-testing.md) for simulations and assertions, and [InfluxDB & Grafana](influxdb-grafana.md) for streaming live metrics to dashboards while a load test runs.

---

[← Back to Table of Contents](README.md)
