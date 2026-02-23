# MSTest Compatibility

TestFuzn is built on top of **MSTest v4** and reuses several MSTest concepts. This page maps MSTest attributes and patterns to their TestFuzn equivalents.

---

## Attributes

| MSTest | TestFuzn | Status | Notes |
|--------|----------|--------|-------|
| `[TestClass]` | `[TestClass]` | ✅ **Same** | Used as-is. Test classes must also inherit from `Test`. |
| `[TestMethod]` | `[Test]` | 🔄 **Replaced** | Use `[Test]` instead. Supports optional `Name`, `Id`, and `Description` properties. |
| `[DataRow]` | — | ❌ **Not supported** | Use [Input Data](input-data.md) (`InputData()`, `InputDataFromList()`) instead. |
| `[DynamicData]` | — | ❌ **Not supported** | Use [Input Data](input-data.md) (`InputDataFromList()`) instead. |
| `[DataTestMethod]` | — | ❌ **Not supported** | Use `[Test]` with [Input Data](input-data.md) instead. |
| `[TestCategory]` | `[Tags]` | 🔄 **Replaced** | Use `[Tags("API", "Critical")]` for categorization and filtering. See [Filtering](filtering.md). |
| `[Ignore]` | `[Skip]` | 🔄 **Replaced** | Use `[Skip("reason")]` or `[Skip]`. Can be applied to methods and classes. See [Filtering](filtering.md). |
| `[Timeout]` | `[Timeout]` | ✅ **Same** | MSTest's `[Timeout]` can be used to set a maximum execution time (in milliseconds) for a test method. |
| `[Description]` | `[Test(Description = "...")]` | 🔄 **Replaced** | Description is a property on the `[Test]` attribute. |
| `[Priority]` | `[Metadata("Priority", "High")]` | 🔄 **Replaced** | Use `[Metadata]` key-value pairs. See [Test Reports](test-reports.md). |
| `[Owner]` | `[Metadata("Owner", "Team")]` | 🔄 **Replaced** | Use `[Metadata]` key-value pairs. See [Test Reports](test-reports.md). |
| — | `[Group]` | 🆕 **New** | Groups tests in reports. See [Test Reports](test-reports.md). |
| — | `[Metadata]` | 🆕 **New** | Key-value pairs shown in reports. See [Test Reports](test-reports.md). |
| — | `[TargetEnvironments]` | 🆕 **New** | Restricts tests to specific environments. See [Environments](environments.md). |

---

## Test Lifecycle

| MSTest | TestFuzn | Status | Notes |
|--------|----------|--------|-------|
| `[AssemblyInitialize]` | `[AssemblyInitialize]` + `IBeforeSuite` | ✅ **Same** | `[AssemblyInitialize]` is required to call `TestFuznIntegration.Init()`. For suite-level setup logic, implement `IBeforeSuite` on the `Startup` class. See [Lifecycle Hooks](lifecycle.md). |
| `[AssemblyCleanup]` | `[AssemblyCleanup]` + `IAfterSuite` | ✅ **Same** | `[AssemblyCleanup]` is required to call `TestFuznIntegration.Cleanup()`. For suite-level teardown logic, implement `IAfterSuite` on the `Startup` class. See [Lifecycle Hooks](lifecycle.md). |
| `[TestInitialize]` | `IBeforeTest` | 🔄 **Replaced** | Implement `IBeforeTest` on your test class. See [Lifecycle Hooks](lifecycle.md). |
| `[TestCleanup]` | `IAfterTest` | 🔄 **Replaced** | Implement `IAfterTest` on your test class. See [Lifecycle Hooks](lifecycle.md). |
| `[ClassInitialize]` | — | ❌ **Not supported** | Use `IBeforeSuite` on the `Startup` class for suite-level setup, or `BeforeScenario()` for scenario-level setup. See [Lifecycle Hooks](lifecycle.md). |
| `[ClassCleanup]` | — | ❌ **Not supported** | Use `IAfterSuite` on the `Startup` class for suite-level teardown, or `AfterScenario()` for scenario-level teardown. See [Lifecycle Hooks](lifecycle.md). |

---

## Test Structure

### MSTest

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ProductTests
{
    [TestInitialize]
    public void Setup()
    {
        // Runs before each test
    }

    [TestCleanup]
    public void Teardown()
    {
        // Runs after each test
    }

    [TestMethod]
    [TestCategory("API")]
    [Description("Verify product creation")]
    public async Task Create_product()
    {
        // Test logic
        Assert.IsTrue(true);
    }

    [DataTestMethod]
    [DataRow("Laptop", 999.99)]
    [DataRow("Keyboard", 49.99)]
    public async Task Create_product_with_data(string name, double price)
    {
        // Test with parameters
        Assert.IsNotNull(name);
    }
}
```

### TestFuzn

```csharp
using Fuzn.TestFuzn;

[TestClass]
public class ProductHttpTests : Test, IBeforeTest, IAfterTest
{
    public Task BeforeTest(Context context)
    {
        // Runs before each test
        return Task.CompletedTask;
    }

    public Task AfterTest(Context context)
    {
        // Runs after each test
        return Task.CompletedTask;
    }

    [Test(Description = "Verify product creation")]
    [Tags("API")]
    public async Task Create_product()
    {
        await Scenario()
            .Step("Create product", context =>
            {
                // Test logic
                Assert.IsTrue(true);
            })
            .Run();
    }

    [Test]
    public async Task Create_product_with_data()
    {
        await Scenario()
            .InputData(
                new Product { Name = "Laptop", Price = 999.99m },
                new Product { Name = "Keyboard", Price = 49.99m }
            )
            .Step("Create product", context =>
            {
                var product = context.InputData<Product>();
                // Test with input data
                Assert.IsNotNull(product.Name);
            })
            .Run();
    }
}
```

---

## Startup Class

TestFuzn requires a `Startup` class that uses MSTest's `[AssemblyInitialize]` and `[AssemblyCleanup]` to wire up the framework. For suite-level setup and teardown logic, implement `IBeforeSuite` and `IAfterSuite` on the same class:

```csharp
[TestClass]
public class Startup : IStartup, IBeforeSuite, IAfterSuite
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
    {
        // Required: wires up TestFuzn (triggers IBeforeSuite after initialization)
        await TestFuznIntegration.Init(testContext);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        // Required: tears down TestFuzn (triggers IAfterSuite before cleanup)
        await TestFuznIntegration.Cleanup(testContext);
    }

    public void Configure(TestFuznConfiguration configuration)
    {
        // Configure plugins here
        configuration.UseHttp();
    }

    public Task BeforeSuite(Context context)
    {
        // Your suite-level setup logic here (runs once before all tests)
        return Task.CompletedTask;
    }

    public Task AfterSuite(Context context)
    {
        // Your suite-level teardown logic here (runs once after all tests)
        return Task.CompletedTask;
    }
}
```

---

## Assertions

TestFuzn does not include its own assertion library. You can use any assertion framework you prefer:

```csharp
// MSTest (built-in)
Assert.IsTrue(response.IsSuccessful);
Assert.AreEqual("Test Product", product.Name);
Assert.IsNotNull(response.Data);

// Or any other assertion library (e.g., FluentAssertions, Shouldly, etc.)
```

---

## Key Differences

| Concept | MSTest | TestFuzn |
|---------|--------|----------|
| **Test structure** | Flat test methods | Scenario → Steps pattern |
| **Parameterized tests** | `[DataRow]`, `[DynamicData]` | `InputData()`, `InputDataFromList()` |
| **Test categorization** | `[TestCategory]` | `[Tags]` |
| **Test lifecycle** | `[TestInitialize]` / `[TestCleanup]` | `IBeforeTest` / `IAfterTest` interfaces |
| **Class lifecycle** | `[ClassInitialize]` / `[ClassCleanup]` | `BeforeScenario()` / `AfterScenario()` |
| **Suite lifecycle** | `[AssemblyInitialize]` / `[AssemblyCleanup]` | Same (required for wiring) + `IBeforeSuite` / `IAfterSuite` for user logic |
| **Data sharing** | Class fields | `SetSharedData()` / `GetSharedData()` or custom context models |
| **Load testing** | Not available | Built-in via `Load().Simulations()` |
| **Reporting** | TRX files | HTML + XML reports with step details |

---

[← Back to Table of Contents](README.md)
