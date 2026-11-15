# Scenarios

## Test Types

TestFuzn supports two types of tests:

### Standard Test

A test that executes a single scenario **once**, or **multiple times** sequentially with different input data, without parallel iterations—ideal for functional, integration, and end-to-end testing.

```csharp
[Test]
public async Task Standard_test_example()
{
    await Scenario()
        .Step("Process product", context =>
        {
            var productName = context.InputData<string>();
            context.Logger.LogInformation($"Processing: {productName}");
        });
}
```

The scenario runs when you `await` it. A test that builds a scenario but never awaits it fails, unless the scenario is added to another one with `Load().IncludeScenario(...)`.

### Load Test

A test that runs **one or more scenarios** in parallel to simulate load on the system. Load tests use `Load().Simulations()` to define the load pattern and can include multiple scenarios.

```csharp
[Test]
public async Task Load_test_example()
{
    await Scenario()
        .Step("Call GET /Products", async context =>
        {
            var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                .WithAuthBearer(authToken)
                .Get<List<Product>>();

            Assert.IsTrue(response.IsSuccessful);
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.FixedConcurrentLoad(100, TimeSpan.FromSeconds(30));
        })
        .Load().AssertWhenDone((context, stats) =>
        {
            Assert.AreEqual(0, stats.Failed.Count);
        });
}
```

---

## Basic Scenario

Create a scenario using the fluent `Scenario` builder.
By default, a scenario is executed **once** as a **standard test**:

```csharp
using Fuzn.TestFuzn;

namespace SampleApp.Tests;

[TestClass]
public class ProductHttpTests : Test
{
    [Test]
    public async Task Basic_standard_test()
    {
        await Scenario()
            .Step("First Step", context =>
            {
                // Synchronous test logic, e.g., calculations, assertions etc.
                var result = 2 + 2;

                Assert.AreEqual(4, result);
            })
            .Step("Second Step", async context =>
            {
                // Async test logic - e.g., calling an API, database, assertions etc.
                await Task.CompletedTask;
            });
    }
}
```

## Load Scenario

The same scenario can be turned into a **load test** by adding one or more simulations.
In a load test, the scenario is executed **with multiple iterations running in parallel** according to the defined simulations:

```csharp
[TestClass]
public class ProductLoadTests : Test
{
    [Test]
    public async Task Basic_load_test()
    {
        await Scenario()
            .Step("First Step", context =>
            {
                // Synchronous test logic, e.g., calculations etc.
                var result = 2 + 2;

                Assert.AreEqual(4, result);
            })
            .Step("Second Step", async context =>
            {
                // Async test logic - e.g., calling an API, database etc.
                await Task.CompletedTask;
            })
            .Load().Simulations((context, simulations) =>
            {
                simulations.FixedLoad(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(100));
            });
    }
}
```

---

## Scenario Configuration

Scenarios can be assigned a stable identifier and an optional description.

```csharp
await Scenario()
    .Id("PROD-CRUD-001")  // Optional identifier
    .Description("Verify product CRUD operations")  // Optional description
    .Step("Step", context => { });
```

---

## Test Class Structure

All test classes must:
1. Inherit from `Test`
2. Be decorated with `[TestClass]` attribute (MSTest)
3. Contain test methods decorated with `[Test]` attribute (TestFuzn)

---

## `[Test]` Attribute

Marks a method as a TestFuzn test. Extends MSTest's `[TestMethod]`.

| Property | Description |
|----------|-------------|
| `Name` (optional) | Name for the test. See [Test name](#test-name) for the order. |
| `Id` (optional) | Unique identifier for tracking across test renames. Without it, TestFuzn makes one from the full test name (namespace, class and method). See [Ids](#ids). |
| `Description` (optional) | Description of what the test validates. Wins over `.Description()` on the scenario. |

```csharp
[Test(Name = "Verify product CRUD operations",
    Description = "Tests the complete product creation, read, update, and delete flow",
    Id = "PROD-001")]
public async Task Verify_product_crud_operations() { ... }
```

### Ids

Every test, scenario and step has an id, which reports and Cockpit use to follow it from run to run.

- **Not set:** TestFuzn makes one, the first 12 characters of a SHA-256 hash, such as `3f9a2c7e81b4`. A test's id comes from its full name, a scenario's from the test id and the scenario name, and a step's from its parent's id and its name. A standard test's scenario uses the test id. The ids stay the same from run to run, and change on a rename.
- **Set by hand:** `[Test(Id = "...")]`, `.Id("...")` or `.Step("name", "id", ...)` keep their id as written. Set one when a test or step must keep its history through a rename. Its steps then build their ids on yours, so they keep their history too.
- **Unique:** test ids within the test project, scenario ids within the test, step ids within the scenario. A clash stops the test with an error that names both.

### Test name

The test name is the first one that is set:

1. `[Test(Name = "...")]`
2. The scenario name: `Scenario("...")` (standard tests only)
3. The method name, with underscores replaced by spaces

```csharp
[Test]
public async Task Login_valid()
{
    await Scenario("User can log in with a valid password")
        .Description("Checks the happy path")
        .Step("Open login page", context => { });
}
```

A skipped test never reaches `Scenario()`, so it shows the attribute name or the method name. Use `Name` if skipped tests must show a readable name in the report.

---

## Execution Flow

The order in which TestFuzn runs your code. Each hook runs only when you add it.

1. `IBeforeSuite.BeforeSuite()` in `Startup`, once before all tests.
2. `IBeforeClass.BeforeClass()`, once before the first test of the class.
3. Load tests: `.Load().BeforeScenarioOnce()`.
4. `IBeforeTest.BeforeTest()`, before each test.
5. For each scenario: `BeforeScenario()`, then the input data (`InputDataFromList()`). A standard test has one scenario; a load test can run several in parallel.
6. Load tests: the warmup, with `.Load().AssertWhileWarmingUp()`. It is not measured.
7. Each iteration:
   - `BeforeIteration()`
   - The steps, in order
   - The cleanup actions from `context.Cleanup()`, in reverse order
   - `AfterIteration()`
8. Load tests: `.Load().AssertWhileRunning()` runs now and then while the iterations run.
9. Load tests: `.Load().AssertWhenDone()`.
10. `AfterScenario()`, for each scenario.
11. Load tests: `.Load().AfterScenarioOnce()`, also after a stop or failure.
12. `IAfterTest.AfterTest()`, after each test.
13. `IAfterClass.AfterClass()`, once after the last test of the class.
14. `IAfterSuite.AfterSuite()` in `Startup`, once after all tests.

---

[← Back to Table of Contents](README.md)
