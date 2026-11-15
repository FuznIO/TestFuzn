# Test Reports

TestFuzn automatically generates HTML and XML reports for both standard tests and load tests.

The HTML reports look like [Cockpit](cockpit.md), so a run reads the same in both places. They follow the light or dark mode of your browser.

---

## Standard Test Reports

| File | Description |
|------|-------------|
| `TestReport.html` | Visual HTML report with test status, step details, comments, and attachments |
| `TestReport.xml` | Machine-readable XML report for CI/CD integration |

---

## Load Test Reports

Each load test generates its own dedicated report files:

| File | Description |
|------|-------------|
| `LoadTestReport-{GroupName}-{TestName}.html` | Visual HTML report with performance metrics |
| `LoadTestReport-{GroupName}-{TestName}.xml` | Machine-readable XML report with detailed statistics |

The load test reports include:
- Phase timings (Init, Warmup, Execution, Cleanup)
- Performance metrics for ok and failed iterations: count, iterations per second, mean, median, P75, P90, P95, P99, min, max and standard deviation
- Charts over the run: iterations per second, duration (P95, mean, P50, P90, P99) and virtual users
- Response time against load: P95 next to iterations per second, when the load changed enough during the run
- Duration distribution: how many ok and failed iterations fell in each time range
- Per-step breakdown and error summary
- Snapshot details with the totals at each snapshot
- Load machines: CPU, memory and iteration start lag of each machine that made the load. A run without a cluster shows one row, named after the machine; above 80 % CPU the row is marked, because a busy load machine makes durations look slow.

---

## Report Location

By default, reports are saved under a `TestFuznResults` directory in the test exe's folder, for example `bin/Debug/net10.0`. Both runners use this folder:

```
{base}/TestFuznResults/{assembly-name}/{run-id}/
├──  TestReport.html
├──  TestReport.xml
├──  LoadTestReport-*.html
├──  LoadTestReport-*.xml
├──  Attachments/
```

You can override the location with:
- Environment variable: `TESTFUZN_RESULTS_DIRECTORY`
- Command line: `--results-directory <path>`

When a custom results directory is supplied, the extra `TestFuznResults` segment is **not** appended — TestFuzn writes directly into the directory you specified:

```
{custom-results-directory}/{assembly-name}/{run-id}/
```

### Report Retention

By default, TestFuzn keeps the last **10 test runs** per assembly and automatically deletes older ones during cleanup. Only directories created by TestFuzn (identified by an internal `.testfuzn` marker file) are deleted.

To change the retention limit:
- Environment variable: `TESTFUZN_KEEP_LAST_N_RUNS`
- Command line: `--keep-last-n-runs <number>`

Set to `0` to disable cleanup and keep all runs.

---

## `[Group]` Attribute

Specifies the group name for a test class. Tests in the same group are reported together.  
If not specified, the fully qualified class name (namespace + class name) is used.

```csharp
[TestClass]
[Group("Product API Tests")]
public class ProductHttpTests : Test { ... }
```

---

## `[Metadata]` Attribute

Adds key-value metadata pairs to tests. Multiple attributes can be applied. Metadata is displayed in test reports.

```csharp
[Test]
[Metadata("Category", "API")]
[Metadata("Priority", "High")]
[Metadata("Owner", "Team-Backend")]
public async Task Verify_product_crud_operations() { ... }
```

---

[← Back to Table of Contents](README.md)
