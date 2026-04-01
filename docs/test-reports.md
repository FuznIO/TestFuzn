# Test Reports

TestFuzn automatically generates HTML and XML reports for both standard tests and load tests.

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
- Performance metrics (RPS, response times, percentiles)
- Per-step breakdown and error summary
- Snapshot timeline showing metrics over time

---

## Report Location

Reports are saved under a `TestFuznResults` directory:

```
{base}/TestFuznResults/{assembly-name}/{run-id}/
├──  TestReport.html
├──  TestReport.xml
├──  LoadTestReport-*.html
├──  LoadTestReport-*.xml
├──  Attachments/
```

By default, `{base}` is the test framework's results directory (e.g., MSTest's `TestResults` folder). You can override it with:
- Environment variable: `TESTFUZN_OUTPUT_DIRECTORY`
- Command line: `--output-directory <path>`

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
