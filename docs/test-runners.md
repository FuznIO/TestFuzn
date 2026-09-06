# Test Runners

TestFuzn tests can run under two runners. The same test code works under both.

| Runner | `--runner` | What you get |
|---|---|---|
| **MSTest** (default) | `mstest` | `dotnet test`, Test Explorer, CI. No console output while a test runs. |
| **TestFuzn** | `testfuzn` | Real-time console output while a load test runs, and an interactive test picker. |

The MSTest runner is the default, so nothing changes for `dotnet test`, your IDE's Test Explorer, or CI. Pass `--runner=testfuzn` to use TestFuzn's own runner instead.

One test project serves both. No separate runner project is needed — the test project doubles as the runner through a custom entry point.

---

## Setup

### 1. Disable the generated entry point

The MSTest runner normally generates the `Main` method for the test project. Add this to the test project from [Getting Started](getting-started.md#project-setup) so it can provide its own instead:

```xml
<GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
```

### 2. Add a Program.cs

`TestFuznHost.Run` routes `--runner=testfuzn` to the TestFuzn runner and everything else to the MSTest runner:

```csharp
using Fuzn.TestFuzn;

namespace MyProject.Tests;

internal static class Program
{
    public static Task<int> Main(string[] args) => TestFuznHost.Run<Startup>(args);
}
```

That is the whole setup. `dotnet test`, Test Explorer and CI never pass `--runner`, so they keep using the MSTest runner exactly as before. Runner names are matched ignoring case.

---

## Running with the TestFuzn Runner

Pass `--runner=testfuzn` to run a test under TestFuzn's own runner:

```bash
# Interactive test selection menu
dotnet run --project MyProject.Tests -- --runner=testfuzn

# Run a specific test (fully qualified class name + method name)
dotnet run --project MyProject.Tests -- --runner=testfuzn --test-name=MyProject.Tests.ProductLoadTests.Load_products_endpoint
```

The test project builds to an executable, so you can run that directly instead. It needs no SDK and no rebuild, which suits containers and CI:

```bash
MyProject.Tests.exe --runner=testfuzn --test-name=MyProject.Tests.ProductLoadTests.Load_products_endpoint
```

The test name can also be provided via the `TESTFUZN_TEST_NAME` environment variable, which is useful in containers:

```bash
TESTFUZN_TEST_NAME=MyProject.Tests.ProductLoadTests.Load_products_endpoint \
  MyProject.Tests.exe --runner=testfuzn
```

With no test named, the runner discovers the assembly's tests and shows a numbered selection menu. A `--test-name` that names no test prints `Test '<name>' not found.` and the runner exits.

Press `Ctrl+C` to stop a running test gracefully: the load producers stop, in-flight iterations and the cleanup hooks complete, and the summary is written for the completed iterations.

### Results

Reports are written to the `TestFuznResults` folder next to the test assembly (the project's build output directory), the same location the MSTest adapter uses.

---

See [Load Testing](load-testing.md) for simulations and assertions, and [InfluxDB & Grafana](influxdb-grafana.md) for streaming live metrics to dashboards while a load test runs.

---

[← Back to Table of Contents](README.md)
