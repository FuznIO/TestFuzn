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
            Assert.AreEqual(0, stats.Failed.RequestCount);
        })
        .Run();
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
            })
            .Run();
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
            })
            .Run();
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
    .Step("Step", context => { })
    .Run();
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
| `Name` (optional) | Name for the test. If not provided, the method name is used with underscores replaced by spaces. |
| `Id` (optional) | Unique identifier for tracking across test renames. |
| `Description` (optional) | Description of what the test validates. |

```csharp
[Test(Name = "Verify product CRUD operations",
    Description = "Tests the complete product creation, read, update, and delete flow",
    Id = "PROD-001")]
public async Task Verify_product_crud_operations() { ... }
```

---

## Execution Flow

```
1. Startup.cs - IBeforeSuite.BeforeSuite() (if implemented)
    ?
2. Test class - IBeforeTest.BeforeTest() (if implemented)
    ?
3. Test method execution
    ?
4. Scenario execution  
   - Standard test: exactly one scenario per test
   - Load test: multiple scenarios can be included and executed in parallel
    ?
5. Scenario - BeforeScenario() (if configured)
    ?
6. Scenario - InputDataFromList() (if configured)
    ?
7. For each iteration:
    ?? BeforeIteration() (if configured)
    ?? Execute steps (in order)
    ?? AfterIteration() (if configured)
    ?? AssertWhileRunning() (load tests only, optimized execution, runs every x-iteration/seconds)
    ?
8. Scenario - AfterScenario() (if configured)
    ?
9. AssertWhenDone() (load tests only, if configured)
    ?
10. Test class - IAfterTest.AfterTest() (if implemented)
    ?
11. Startup.cs - IAfterSuite.AfterSuite() (if implemented)
```

---

[? Back to Table of Contents](README.md)
