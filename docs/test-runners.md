# Test Runners

TestFuzn tests can run under two runners. The same test code works under both.

| Runner | `--runner` | What you get |
|---|---|---|
| **MSTest** (default) | `mstest` | `dotnet test`, Test Explorer, CI. No console output while a test runs. |
| **TestFuzn** | `testfuzn` | A live view while a load test runs, and an interactive test picker. |

The MSTest runner is the default, so nothing changes for `dotnet test`, your IDE's Test Explorer, or CI. One test project serves both: it doubles as the runner through its entry point.

---

## Setup

There is nothing to set up. `Fuzn.TestFuzn.Adapters.MSTest` adds an entry point to the test project. It calls `TestFuznHost.Run(args)`, which finds your `Startup` class and routes `--runner=testfuzn` to the TestFuzn runner and everything else to the MSTest runner. Runner names are matched ignoring case.

### Your own Main

To run code around the test run, or to pick the startup class yourself, turn off the added entry point in the test project:

```xml
<TestFuznGenerateEntryPoint>false</TestFuznGenerateEntryPoint>
```

Then add a `Program.cs`:

```csharp
using Fuzn.TestFuzn;

namespace MyProject.Tests;

internal static class Program
{
    public static Task<int> Main(string[] args) => TestFuznHost.Run<Startup>(args);
}
```

A test project with more than one `IStartup` class needs this, because `TestFuznHost.Run(args)` cannot pick one.

---

## Running

```bash
# Interactive test picker
dotnet run --project MyProject.Tests -- --runner=testfuzn

# A specific test
dotnet run --project MyProject.Tests -- --runner=testfuzn --test-name=MyProject.Tests.ProductLoadTests.Load_products_endpoint

# The built executable needs no SDK and no rebuild, which suits containers and CI
MyProject.Tests.exe --runner=testfuzn --test-name=MyProject.Tests.ProductLoadTests.Load_products_endpoint

# All arguments and their environment variables
dotnet run --project MyProject.Tests -- --runner=testfuzn --help
```

The test name can also come from the `TESTFUZN_TEST_NAME` environment variable, which is handy in containers. With no test named, the runner shows the picker: type to filter, arrows to move, `Enter` to run, `Esc` to quit.

## The live view

A load test updates live while it runs, then prints the same sections as the summary when it ends. The names and columns match the HTML report:

```
════════════════════════════════════════════════════════════════════════════════════════════════
  ⚡ TestFuzn
  Execution Environment: -    Target Environment: test

  Scenario - Checkout flow                                                    ⠙ Running   2m 15s

  Test Phases ──────────────────────────────────────────────────────────────────────────────────
  Phase           Duration  Started   Ended
  Init            2s        14:04:21  14:04:23
  Warmup          30s       14:04:23  14:04:53
    Fixed Load - Rate: 10, Interval: 0:00:01, Duration: 0:00:30 (Warmup)
  Execution       1m 43s    14:04:53  —
    Fixed Load - Rate: 400, Interval: 0:00:01, Duration: 0:05:00
  Cleanup         —         —         —
  Total Test Run  2m 15s    14:04:21  —

  Iterations ───────────────────────────────────────────────────────────────────────────────────
  Total Iterations 12512    Successful 12480 (99.7%)    Failed 32 (0.3%)    Iterations/s 142
  Warmup 1203

  Step Performance ─────────────────────────────────────────────────────────────────────────────
  Step                 Type    Count  Per Second    Mean  Median     P75     P90     P95     P99
  All Steps (Summary)  Ok      12480         142   38 ms   35 ms   48 ms   60 ms   72 ms   94 ms
                       Failed     32           1  102 ms   99 ms  110 ms  125 ms  140 ms  160 ms
  Add to cart          Ok       6250          71   18 ms   16 ms   22 ms   31 ms   40 ms   55 ms
                       Failed      2           1  102 ms   99 ms  110 ms  125 ms  140 ms  160 ms
  Checkout             Ok       6230          69   58 ms   52 ms   70 ms   80 ms   90 ms  130 ms
                       Failed     30           1  102 ms   99 ms  110 ms  125 ms  140 ms  160 ms
    → Pay              Ok       6230          69   22 ms   20 ms   26 ms   44 ms   61 ms
  q quit
```

Press `q`, or `Ctrl+C` at any time, to stop the run: the load producers stop, in-flight iterations and the cleanup hooks complete, the summary is written for what completed, and the runner exits with code 1.

## Without a terminal

In CI, in a container, or with the output piped to a file, the runner writes no escape sequences. It prints one line per scenario per second instead, plus a line for each phase change, then the summary:

```
[Checkout flow] phase: Warmup
[Checkout flow] elapsed 00:00:08  iterations 105  successful 104  failed 1  iterations/s 40  p95 513 ms
[Checkout flow] phase: Execution
[Checkout flow] completed  elapsed 00:00:19  iterations 840  successful 807  failed 33  p95 519 ms
```

`iterations`, `successful` and `failed` count measurement iterations only; warmup is excluded. The final line reads `completed`, `stopped`, `failed` or `skipped`. Set `NO_COLOR` to drop the colors and keep the layout, and wrap the runner in `timeout --foreground`, not plain `timeout`, or it cannot read the terminal.

Under `dotnet test` and Test Explorer there is no live view: the summary goes to MSTest's test output as plain tables.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Test passed (or was skipped), or the picker was quit |
| 1 | Test failed, the run was stopped, the test name was not found, or the arguments were invalid |

Reports are written to the `TestFuznResults` folder next to the test assembly, the same place the MSTest adapter uses. The summary prints the path of the HTML report as its last line.

---

See [Load Testing](load-testing.md) for simulations and assertions, and [Cluster Runs](cluster-runs.md) for running a load test on other machines with `--cluster` while the live view stays on yours.

---

[← Back to Table of Contents](README.md)
