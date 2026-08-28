# Standalone Runner

The MSTest runner works well for standard tests, CI, and Test Explorer, but MSTest does not support real-time console output during execution. The standalone runner hosts tests in the test project's own executable and provides live feedback — step-by-step progress, live metrics, and a load test summary — which matters for complex or long-running load tests.

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
```

The test name can also be provided via the `TESTFUZN_TEST_NAME` environment variable, which is useful in containers:

```bash
TESTFUZN_TEST_NAME=MyProject.Tests.ProductLoadTests.Load_products_endpoint \
  dotnet run --project MyProject.Tests -- run
```

Press `Ctrl+C` to stop a running test gracefully — cancellation cascades through the execution pipeline and the summary is written for the completed iterations.

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Test passed (or was skipped) |
| 1 | Test failed, or the test name was not found |

### Results

Reports are written to the `TestFuznResults` folder next to the test assembly (the project's build output directory), the same location the MSTest adapter uses.

---

See [Load Testing](load-testing.md) for simulations and assertions, and [InfluxDB & Grafana](influxdb-grafana.md) for streaming live metrics to dashboards while a load test runs.

---

```markdown
[← Back to Table of Contents](README.md)
