# Core Concepts

## Test Types

TestFuzn supports two types of tests:

### Standard Test

A test that executes a single scenario **once**, or **multiple times** sequentially with different input data, without parallel iterations—ideal for functional, integration, and end-to-end testing.

```csharp
[Test]
public async Task Standard_test_example()
{
    await Scenario()
        .Step("Process user", context =>
        {
            var email = context.InputData<string>();
            context.Logger.LogInformation($"Processing: {email}");
        })
        .Run();
}
```

### Load Test

A test that runs **one or more scenarios** in parallel to simulate load on the system. Load tests use `Load().Simulations()` to define the load pattern and can include multiple scenarios.

```csharp
[Test]
public async Task Load_test_example()
{
    await Scenario()
        .Step("Call API", async context =>
        {
            // API call logic
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.FixedConcurrentLoad(100, TimeSpan.FromSeconds(30));
        })
        .Load().AssertWhenDone((context, stats) =>
        {
            Assert.AreEqual(0, stats.Failed.RequestCount);
        })
        .Run();
}
```

---

## Test Class Structure

All test classes must:
1. Inherit from `Test`
2. Be decorated with `[TestClass]` attribute (MSTest)
3. Contain test methods decorated with `[Test]` attribute (TestFuzn)

---

## Execution Flow

```
1. Startup.cs - IBeforeSuite.BeforeSuite() (if implemented)
    ↓
2. Test class - IBeforeTest.BeforeTest() (if implemented)
    ↓
3. Test method execution
    ↓
4. Scenario execution  
   - Standard test: exactly one scenario per test
   - Load test: multiple scenarios can be included and executed in parallel
    ↓
5. Scenario - BeforeScenario() (if configured)
    ↓
6. Scenario - InputDataFromList() (if configured)
    ↓
7. For each iteration:
    ├─ BeforeIteration() (if configured)
    ├─ Execute steps (in order)
    ├─ AfterIteration() (if configured)
    ├─ AssertWhileRunning() (load tests only, optimized execution, runs every x-iteration/seconds)
    ↓
8. Scenario - AfterScenario() (if configured)
    ↓
9. AssertWhenDone() (load tests only, if configured)
    ↓
10. Test class - IAfterTest.AfterTest() (if implemented)
    ↓
11. Startup.cs - IAfterSuite.AfterSuite() (if implemented)
```

---

## Test Attributes

TestFuzn provides several attributes to control test execution, categorization, and metadata.

### `[Test]` Attribute

Marks a method as a TestFuzn test. Extends MSTest's `[TestMethod]`.

| Property | Description |
|----------|-------------|
| `Name` (optional) | Name for the test. If not provided, the method name is used with underscores replaced by spaces. |
| `Id` (optional) | Unique identifier for tracking across test renames. |
| `Description` (optional) | Description of what the test validates. |

```csharp
[Test(Name = "Verify product creation works correctly",
    Description = "Tests the complete product creation flow",
    Id = "PROD-001")]
public async Task Verify_product_creation() { ... }
```

### `[Group]` Attribute

Specifies the group name for a test class. Tests in the same group are reported together.  
If not specified, the fully qualified class name (namespace + class name) is used.

```csharp
[TestClass]
[Group("Product API Tests")]
public class ProductTests : Test { ... }
```

### `[Tags]` Attribute

Specifies tags for categorization and filtering. Can be used to run specific subsets of tests.

```csharp
[Test]
[Tags("Unit", "Validation", "Critical")]
public async Task Validate_product_name() { ... }
```

### `[Metadata]` Attribute

Adds key-value metadata pairs to tests. Multiple attributes can be applied.

```csharp
[Test]
[Metadata("Category", "API")]
[Metadata("Priority", "High")]
[Metadata("Owner", "Team-Backend")]
public async Task API_test_with_metadata() { ... }
```

### `[Skip]` Attribute

Marks a test to be skipped during execution.

```csharp
[Test]
[Skip("Waiting for API deployment")]
public async Task Skipped_test() { ... }

// Or without a reason:
[Test]
[Skip]
public async Task Another_skipped_test() { ... }
```

### `[TargetEnvironments]` Attribute

Specifies which environments the test should run against. The current environment is determined by the `TESTFUZN_TARGET_ENVIRONMENT` environment variable or `--target-environment` argument.

```csharp
[Test]
[TargetEnvironments("Dev", "Test", "Staging")]
public async Task Test_that_should_not_run_in_production() { ... }
```

---

[← Back to Table of Contents](README.md)
