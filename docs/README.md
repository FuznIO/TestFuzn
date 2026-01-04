## 📚 Table of Contents

- [Overview](#overview)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Test Types](#test-types)
- [Test Attributes](#test-attributes)
- [Writing Tests](#writing-tests)
  - [Basic Scenario](#basic-scenario)
  - [Scenario Configuration](#scenario-configuration)
  - [Steps](#steps)
  - [Input Data](#input-data)
  - [Custom Context](#custom-context)
  - [Shared Data](#shared-data)
  - [Lifecycle Hooks](#lifecycle-hooks)
- [Advanced Features](#advanced-features)
  - [Sub-Steps](#sub-steps)
  - [Shared Steps](#shared-steps)
  - [Comments and Attachments](#comments-and-attachments)
  - [Logging](#logging)
- [Load Testing](#load-testing)
  - [Multiple Scenarios](#multiple-scenarios)
  - [Warmup](#warmup)
  - [Simulations](#simulations)
  - [Assertions](#assertions)
  - [Statistics](#statistics)
- [HTTP Testing](#http-testing)
  - [Basic HTTP Requests](#basic-http-requests)
  - [Request Methods](#request-methods)
  - [Authentication](#authentication)
  - [JSON Serialization](#json-serialization)
  - [HTTP Load Testing](#http-load-testing)
- [Web UI Testing with Playwright](#web-ui-testing-with-playwright)
  - [Basic Browser Testing](#basic-browser-testing)
  - [Browser Interactions](#browser-interactions)
  - [UI Load Testing](#ui-load-testing)
- [Real-Time Statistics with InfluxDB](#real-time-statistics-with-influxdb)
- [API Reference](#api-reference)
- [Examples](#examples)

---

## Overview

TestFuzn is a modern testing framework for .NET 10 that provides a fluent, scenario-based approach to writing both standard tests and load tests. It's built on top of **MSTest v4** and combines the simplicity of traditional unit testing with powerful capabilities for complex test scenarios, data-driven testing, and performance validation.

**Key Features:**
- 🎯 Fluent API for readable, maintainable tests
- 📊 Built-in support for standard and load testing
- 🔄 Flexible input data management
- 📈 Real-time statistics with InfluxDB/Grafana integration
- 🔌 Extensible with shared steps and plugins
- 📎 File attachments and logging support
- ⚡ Async/await support throughout
- 🎨 Custom context models for type-safe data sharing
- 🧪 MSTest v4 compatible with full Visual Studio integration

---

## Requirements

- **.NET 10** or later
- **MSTest v4** (included via `MSTest.Sdk/4.0.0`)
- Visual Studio 2022 17.8+ or VS Code with C# Dev Kit

---

## Installation

Install TestFuzn via NuGet:

```bash
dotnet add package Fuzn.TestFuzn
dotnet add package Fuzn.TestFuzn.Adapters.MSTest
```

For HTTP testing support:

```bash
dotnet add package Fuzn.TestFuzn.Plugins.Http
```

For Playwright/browser testing support:

```bash
dotnet add package Fuzn.TestFuzn.Plugins.Playwright
```

For InfluxDB real-time statistics:

```bash
dotnet add package Fuzn.TestFuzn.Sinks.InfluxDB
```

### Project Setup

Your test project should use `MSTest.Sdk`:

```xml
<Project Sdk="MSTest.Sdk/4.0.0">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TestFuzn\TestFuzn.csproj" />
    <ProjectReference Include="..\TestFuzn.Adapters.MSTest\TestFuzn.Adapters.MSTest.csproj" />
    <ProjectReference Include="..\TestFuzn.Plugins.Http\TestFuzn.Plugins.Http.csproj" />
  </ItemGroup>

</Project>
```

---

## Quick Start

### 1. Create a Startup Class

Every test project needs a `Startup` class to initialize TestFuzn:

```csharp
using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;

namespace MyTests;

[TestClass]
public class Startup : IStartup
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
    {
        await TestFuznIntegration.Init(testContext);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        await TestFuznIntegration.Cleanup(testContext);
    }

    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.UseHttp();
    }
}
```

### 2. Write Your First Test

```csharp
using Fuzn.TestFuzn;

namespace MyTests;

[TestClass]
[Group("My Test Group")]
public class MyFirstTests : Test
{
    [Test(Name = "My first TestFuzn test")]
    [Tags("Smoke")]
    public async Task My_first_test()
    {
        await Scenario()
            .Step("Step 1 - Setup", context =>
            {
                context.Logger.LogInformation("Hello from TestFuzn!");
            })
            .Step("Step 2 - Verify", context =>
            {
                Assert.IsTrue(true);
            })
            .Run();
    }
}
```

---

## Core Concepts

### Test Class Structure

All test classes must:
1. Inherit from `Test`
2. Be decorated with `[TestClass]` attribute (MSTest)
3. Contain test methods decorated with `[Test]` attribute (TestFuzn)

### Execution Flow

```
1. Startup.cs – IBeforeSuite.BeforeSuite() (if implemented)
    ↓
2. Test class – IBeforeTest.BeforeTest() (if implemented)
    ↓
3. Test method execution
    ↓
4. Scenario execution  
   - Standard test: exactly one scenario per test
   - Load test: multiple scenarios can be included and executed in parallel
    ↓
5. Scenario – BeforeScenario() (if configured)
    ↓
6. Scenario – InputDataFromList() (if configured)
    ↓
7. For each iteration:
    ├─ BeforeIteration() (if configured)
    ├─ Execute steps (in order)
    ├─ AfterIteration() (if configured)
    └─ AssertWhileRunning() (load tests only, optimized execution)
    ↓
8. Scenario – AfterScenario() (if configured)
    ↓
9. AssertWhenDone() (load tests only, if configured)
    ↓
10. Test class – IAfterTest.AfterTest() (if implemented)
    ↓
11. Startup.cs – IAfterSuite.AfterSuite() (if implemented)
```

---

## Test Types

TestFuzn supports two types of tests:

### Standard Test

A test that runs **one scenario** once with one set of input data, or runs multiple times sequentially with different input data. Ideal for functional testing, unit testing, and integration testing.

```csharp
[Test]
public async Task Standard_test_example()
{
    await Scenario()
        .InputData("user1@example.com", "user2@example.com", "user3@example.com")
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

## Writing Tests

### Basic Scenario

Create a scenario using the fluent `Scenario` builder:

```csharp
[Test]
public async Task Basic_scenario()
{
    await Scenario()
        .Step("First Step", context =>
        {
            // Synchronous test logic
        })
        .Step("Second Step", async context =>
        {
            // Async test logic
            await Task.CompletedTask;
        })
        .Run();
}
```

### Scenario Configuration

Scenarios can be assigned a stable identifier and an optional description.

```csharp
await Scenario()
    .Id("SCEN-1234")  // Optional identifier
    .Description("Scenario description")  // Optional description
    .Step("Step", context => { })
    .Run();
```

### Steps

Steps are the building blocks of scenarios. They execute in order, and if one fails, subsequent steps are skipped.

#### Synchronous Steps

```csharp
.Step("Sync Step", context =>
{
    context.Logger.LogInformation("Executing step");
})
```

#### Asynchronous Steps

```csharp
.Step("Async Step", async context =>
{
    await SomeAsyncOperation();
})
```

#### Steps with IDs

```csharp
.Step("Important Step", "STEP-001", async context =>
{
    // Step with explicit ID for tracking
})
```

### Input Data

Input data is used to drive multiple iterations of the same scenario.

Both simple types (e.g. `string`, `int`) and complex types (custom classes, records, DTOs) are supported.

#### Static Input Data

```csharp
.InputData("user1", "user2", "user3")
```

#### Static Input Data (Complex Type)

```csharp
.InputData(
    new User { Id = 1, Name = "user1" },
    new User { Id = 2, Name = "user2" }
)
```

#### Input Data from Function (Sync)

```csharp
.InputDataFromList((context) =>
{
    return new List<object> { "user1", "user2", "user3" };
})
```

#### Input Data from Function (Async)

```csharp
.InputDataFromList(async (context) =>
{
    var users = await database.GetTestUsers();
    return users.Cast<object>().ToList();
})
```

#### Input Data Behaviors

Control how input data is consumed:

```csharp
.InputDataBehavior(InputDataBehavior.Loop)              // Sequential (default)
.InputDataBehavior(InputDataBehavior.Random)            // Random selection
.InputDataBehavior(InputDataBehavior.LoopThenRandom)    // Loop first, then random (load tests)
.InputDataBehavior(InputDataBehavior.LoopThenRepeatLast) // Loop, then repeat last item (load tests)
```

#### Accessing Input Data

```csharp
.Step("Use Input Data", context =>
{
    var user = context.InputData<string>();
    context.Logger.LogInformation($"Processing user: {user}");
})
```

### Custom Context

Share typed data between steps using custom context models:

```csharp
public class LoginContext
{
    public string Username { get; set; }
    public string Token { get; set; }
    public DateTime LoginTime { get; set; }
}

[Test]
public async Task Custom_context_example()
{
    await Scenario<LoginContext>()
        .Step("Login", context =>
        {
            context.Model.Username = "testuser";
            context.Model.Token = "abc123";
            context.Model.LoginTime = DateTime.UtcNow;
        })
        .Step("Verify Session", context =>
        {
            Assert.IsNotNull(context.Model.Token);
            Assert.AreEqual("testuser", context.Model.Username);
        })
        .Run();
}
```

### Shared Data

Share untyped data between steps within the same iteration:

```csharp
.Step("Set Data", context =>
{
    context.SetSharedData("userId", 12345);
    context.SetSharedData("config", new Configuration());
})
.Step("Get Data", context =>
{
    var userId = context.GetSharedData<int>("userId");
    var config = context.GetSharedData<Configuration>("config");
})
```

### Lifecycle Hooks

#### Scenario Level

```csharp
.BeforeScenario((context) =>
{
    // Runs once before any iterations
})
.AfterScenario((context) =>
{
    // Runs once after all iterations
})
```

#### Iteration Level

```csharp
.BeforeIteration((context) =>
{
    // Runs before each iteration's steps
})
.AfterIteration((context) =>
{
    // Runs after each iteration's steps
})
```

#### Test Class Level

Implement interfaces for test-level hooks:

```csharp
[TestClass]
public class MyTests : Test, IBeforeTest, IAfterTest
{
    public Task BeforeTest(Context context)
    {
        context.Logger.LogInformation("Starting test: {TestName}", context.Info.TestName);
        return Task.CompletedTask;
    }

    public Task AfterTest(Context context)
    {
        context.Logger.LogInformation("Completed test: {TestName}", context.Info.TestName);
        return Task.CompletedTask;
    }
}
```

#### Suite Level

Implement in your Startup class:

```csharp
public class Startup : IStartup, IBeforeSuite, IAfterSuite
{
    public Task BeforeSuite(Context context)
    {
        // Runs once before all tests
        return Task.CompletedTask;
    }

    public Task AfterSuite(Context context)
    {
        // Runs once after all tests
        return Task.CompletedTask;
    }
    
    // ... Configure method
}
```

---

## Advanced Features

### Sub-Steps

Create nested step hierarchies for better organization:

```csharp
.Step("Parent Step", context =>
{
    context.Step("Child Step 1", subContext =>
    {
        subContext.Step("Grandchild Step", grandContext =>
        {
            // Deeply nested logic
        });
    });
    
    context.Step("Child Step 2", async subContext =>
    {
        // Async sub-step
        await Task.CompletedTask;
    });
})
```

### Shared Steps

Create reusable steps across tests using extension methods:

```csharp
public static class SharedSteps
{
    public static ScenarioBuilder<T> LoginStep<T>(this ScenarioBuilder<T> builder, string baseUrl)
        where T : new()
    {
        return builder.Step("Authenticate", async context =>
        {
            var response = await context.CreateHttpRequest($"{baseUrl}/api/Auth/token")
                .Body(new { Username = "admin", Password = "admin123" })
                .Post();
            
            Assert.IsTrue(response.Ok);
            var token = response.BodyAs<TokenResponse>().Token;
            context.SetSharedData("authToken", token);
        });
    }
}

// Usage
await Scenario()
    .LoginStep("https://localhost:44316")
    .Step("Call Protected API", async context =>
    {
        var token = context.GetSharedData<string>("authToken");
        // Use token...
    })
    .Run();
```

### Comments and Attachments

#### Comments

Add contextual information to test execution:

```csharp
.Step("Process", context =>
{
    context.Comment("Starting data processing");
    // Process data
    context.Comment("Processing complete");
})
```

- **Standard tests**: Comments output to console and reports
- **Load tests**: Comments output to log files

#### Attachments

Attach files to steps for evidence and debugging:

```csharp
.Step("Capture Evidence", async context =>
{
    // Attach text content
    await context.Attach("log.txt", "Log content here");
    
    // Attach byte array
    var screenshot = await CaptureScreenshot();
    await context.Attach("screenshot.png", screenshot);
    
    // Attach stream
    using var fileStream = File.OpenRead("data.json");
    await context.Attach("data.json", fileStream);
})
```

### Logging

Use the built-in logger for structured logging:

```csharp
.Step("Logged Step", context =>
{
    context.Logger.LogInformation("Informational message");
    context.Logger.LogWarning("Warning message");
    context.Logger.LogError("Error message");
    context.Logger.LogDebug("Debug: {Details}", detailsObject);
})
```

---

## Load Testing

Load tests are defined by adding `Load().Simulations()` to a scenario. This changes the test from a standard test to a load test.

Standard tests run once by default or once per input data item, while load tests ignore input-driven iteration and instead run the number of iterations defined by their simulations.

### Multiple Scenarios

Load tests can include multiple scenarios that execute in parallel:

```csharp
[Test]
public async Task Mixed_workload_load_test()
{
    var readScenario = Scenario("Read Operations")
        .Step("Get Products", async context => 
        {
            await apiClient.GetAsync("/api/products");
        });
    
    var writeScenario = Scenario("Write Operations")
        .Step("Create Order", async context =>
        {
            await apiClient.PostAsync("/api/orders", content);
        });
    
    await Scenario("Main Scenario")
        .Step("Primary Operation", async context =>
        {
            await apiClient.GetAsync("/api/health");
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.FixedConcurrentLoad(50, TimeSpan.FromMinutes(5));
        })
        .Load().IncludeScenario(readScenario)   // Runs in parallel
        .Load().IncludeScenario(writeScenario)  // Runs in parallel
        .Load().AssertWhenDone((context, stats) =>
        {
            var readStats = stats.GetStep("Get Products");
            var writeStats = stats.GetStep("Create Order");
            
            Assert.IsTrue(readStats.Ok.ResponseTimeMean < TimeSpan.FromMilliseconds(100));
            Assert.IsTrue(writeStats.Ok.ResponseTimeMean < TimeSpan.FromMilliseconds(500));
        })
        .Run();
}
```

### Warmup

Run warmup simulations before actual load tests. No statistics are recorded during warmup:

```csharp
.Load().Warmup((context, simulations) =>
{
    simulations.FixedConcurrentLoad(10, TimeSpan.FromSeconds(5));
})
```

### Simulations

Define load patterns to simulate real-world traffic:

#### One-Time Load

Execute a specific number of iterations:

```csharp
simulations.OneTimeLoad(1000); // Execute 1000 times
```

#### Gradual Load Increase

Ramp up load gradually:

```csharp
simulations.GradualLoadIncrease(
    startRate: 1, 
    endRate: 100, 
    duration: TimeSpan.FromSeconds(30)
);
```

#### Fixed Load

Maintain constant requests per second:

```csharp
simulations.FixedLoad(
    rate: 50,                              // Requests per second
    duration: TimeSpan.FromMinutes(5)
);

// Or with custom interval
simulations.FixedLoad(
    rate: 100,
    interval: TimeSpan.FromSeconds(1),
    duration: TimeSpan.FromMinutes(5)
);
```

#### Fixed Concurrent Load

Maintain a constant number of concurrent users:

```csharp
simulations.FixedConcurrentLoad(
    count: 500,                            // Concurrent users
    duration: TimeSpan.FromMinutes(10)
);
```

#### Random Load Per Second

Vary load randomly within a range:

```csharp
simulations.RandomLoadPerSecond(
    minRate: 10,
    maxRate: 100,
    duration: TimeSpan.FromMinutes(5)
);
```

#### Pause

Add pauses between simulations:

```csharp
simulations.Pause(TimeSpan.FromSeconds(30));
```

#### Combined Simulations

Chain multiple simulations together:

```csharp
.Load().Simulations((context, simulations) =>
{
    simulations.GradualLoadIncrease(1, 50, TimeSpan.FromSeconds(20));
    simulations.FixedLoad(50, TimeSpan.FromMinutes(2));
    simulations.Pause(TimeSpan.FromSeconds(10));
    simulations.FixedConcurrentLoad(100, TimeSpan.FromMinutes(3));
})
```

### Assertions

Validate performance during and after test execution:

#### Assert While Running

Continuous validation during the test. If assertion fails, the test stops:

```csharp
.Load().AssertWhileRunning((context, stats) =>
{
    Assert.IsTrue(stats.RequestCount < 10000, "Too many requests");
    Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(1), "Response too slow");
    Assert.AreEqual(0, stats.Failed.RequestCount, "Should have no failures");
})
```

#### Assert When Done

Final validation after test completion:

```csharp
.Load().AssertWhenDone((context, stats) =>
{
    Assert.AreEqual(0, stats.Failed.RequestCount, "Expected no failures");
    Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(0.5));
    Assert.IsTrue(stats.Ok.ResponseTimePercentile99 < TimeSpan.FromSeconds(1));
    
    // Per-step assertions
    var loginStats = stats.GetStep("Login");
    Assert.IsTrue(loginStats.Ok.ResponseTimeMean < TimeSpan.FromMilliseconds(300));
})
```

### Statistics

Available metrics for assertions:

| Metric | Description |
|--------|-------------|
| `RequestCount` | Total number of requests |
| `RequestsPerSecond` | Requests per second |
| `ResponseTimeMin` | Minimum response time |
| `ResponseTimeMax` | Maximum response time |
| `ResponseTimeMean` | Average response time |
| `ResponseTimeMedian` | 50th percentile response time |
| `ResponseTimePercentile75` | 75th percentile response time |
| `ResponseTimePercentile95` | 95th percentile response time |
| `ResponseTimePercentile99` | 99th percentile response time |
| `ResponseTimeStandardDeviation` | Response time standard deviation |

Access statistics separately for successful (`Ok`) and failed (`Failed`) requests:

```csharp
stats.Ok.RequestCount        // Successful requests
stats.Failed.RequestCount    // Failed requests
stats.Ok.ResponseTimeMean    // Average response time for successful requests
```

---

## HTTP Testing

TestFuzn provides a fluent HTTP client plugin for testing REST APIs.

### Basic HTTP Requests

```csharp
using Fuzn.TestFuzn.Plugins.Http;

[Test]
public async Task Get_products_from_api()
{
    await Scenario()
        .Step("Call API and verify response", async context =>
        {
            var response = await context.CreateHttpRequest("https://api.example.com/products")
                .Get();
            
            Assert.IsTrue(response.Ok);
            var products = response.BodyAs<List<Product>>();
            Assert.IsTrue(products.Count > 0);
        })
        .Run();
}
```

### Request Methods

```csharp
// GET
var response = await context.CreateHttpRequest(url).Get();

// POST with body
var response = await context.CreateHttpRequest(url)
    .Body(new { Name = "Product", Price = 99.99 })
    .Post();

// PUT
var response = await context.CreateHttpRequest(url)
    .Body(updatedProduct)
    .Put();

// DELETE
var response = await context.CreateHttpRequest(url).Delete();

// PATCH
var response = await context.CreateHttpRequest(url)
    .Body(patchData)
    .Patch();
```

### Authentication

```csharp
// Bearer token
var response = await context.CreateHttpRequest(url)
    .AuthBearer("your-jwt-token")
    .Get();

// Basic authentication
var response = await context.CreateHttpRequest(url)
    .AuthBasic("username", "password")
    .Get();
```

### Additional Request Options

```csharp
var response = await context.CreateHttpRequest(url)
    .Header("X-Custom-Header", "value")
    .Headers(new Dictionary<string, string> { { "Key", "Value" } })
    .Cookie("session", "abc123")
    .ContentType(ContentTypes.Json)
    .Accept(AcceptTypes.Json)
    .Timeout(TimeSpan.FromSeconds(30))
    .UserAgent("MyTestAgent/1.0")
    .Get();
```

### Response Handling

```csharp
// Check if successful
Assert.IsTrue(response.Ok);

// Get status code
Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

// Deserialize to type
var product = response.BodyAs<Product>();

// Parse as dynamic JSON
var json = response.BodyAsJson();
var name = json.name;

// Access raw body
var rawBody = response.Body;

// Access headers
var contentType = response.Headers.ContentType;

// Access cookies
var cookies = response.Cookies;

// Generate curl command for debugging
var curl = await response.GetCurlCommand();
```

### JSON Serialization

TestFuzn uses System.Text.Json by default but supports custom serializers:

```csharp
.Step("Use custom serializer", async context =>
{
    var serializer = new NewtonsoftSerializerProvider(); // If using Newtonsoft
    
    var response = await context.CreateHttpRequest(url)
        .SerializerProvider(serializer)
        .Get();
})
```

### HTTP Load Testing

```csharp
[Test]
public async Task API_load_test()
{
    await Scenario()
        .Step("Call API endpoint", async context =>
        {
            var response = await context.CreateHttpRequest("https://api.example.com/health")
                .Get();
            
            Assert.IsTrue(response.Ok);
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.FixedLoad(50, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        })
        .Load().AssertWhenDone((context, stats) =>
        {
            Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(1));
            Assert.AreEqual(0, stats.Failed.RequestCount);
        })
        .Run();
}
```

---

## Web UI Testing with Playwright

TestFuzn integrates with Microsoft Playwright for browser automation.

### Configuration

```csharp
public void Configure(TestFuznConfiguration configuration)
{
    configuration.UsePlaywright(c =>
    {
        c.BrowserTypes = new List<string> { "chromium" };
        c.ConfigureBrowserLaunchOptions = (browserType, launchOptions) =>
        {
            launchOptions.Headless = false; // Set to true for CI/CD
        };
    });
}
```

### Basic Browser Testing

```csharp
using Fuzn.TestFuzn.Plugins.Playwright;
using Microsoft.Playwright;

[Test]
public async Task Verify_login_flow()
{
    await Scenario()
        .Step("Navigate and login", async context =>
        {
            var page = await context.CreateBrowserPage();
            await page.GotoAsync("https://myapp.com/login");
            
            await page.GetByRole(AriaRole.Textbox, new() { Name = "Username" }).FillAsync("admin");
            await page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("password");
            await page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            
            await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Welcome");
        })
        .Run();
}
```

### Browser Interactions

```csharp
.Step("Complete checkout", async context =>
{
    var page = await context.CreateBrowserPage();
    await page.GotoAsync("https://shop.example.com");
    
    // Click elements
    await page.ClickAsync(".product-card:first-child");
    await page.ClickAsync("button.add-to-cart");
    
    // Fill forms
    await page.FillAsync("#email", "customer@example.com");
    await page.SelectOptionAsync("#country", "US");
    
    // Wait for elements
    await page.WaitForSelectorAsync(".order-confirmation");
    
    // Take screenshot
    var screenshot = await page.ScreenshotAsync();
    await context.Attach("confirmation.png", screenshot);
})
```

### UI Load Testing

```csharp
[Test]
public async Task UI_load_test()
{
    await Scenario()
        .Step("Simulate user interaction", async context =>
        {
            var page = await context.CreateBrowserPage();
            await page.GotoAsync("https://myapp.com");
            await page.ClickAsync("a.login");
            await page.FillAsync("#username", $"user{Random.Shared.Next(1000)}");
            await page.FillAsync("#password", "password");
            await page.ClickAsync("button[type='submit']");
            await page.WaitForSelectorAsync(".dashboard");
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.OneTimeLoad(10);
        })
        .Load().AssertWhenDone((context, stats) =>
        {
            Assert.AreEqual(10, stats.Ok.RequestCount);
        })
        .Run();
}
```

---

## Real-Time Statistics with InfluxDB

TestFuzn supports streaming load test statistics to InfluxDB for real-time visualization with Grafana.

### Configuration

```csharp
public void Configure(TestFuznConfiguration configuration)
{
    configuration.UseInfluxDB(config =>
    {
        config.Url = "http://localhost:8086";
        config.Token = "your-influxdb-token";
        config.Org = "your-org";
        config.Bucket = "testfuzn";
    });
}
```

### Using appsettings.json

```csharp
// Startup.cs
configuration.UseInfluxDB(); // Reads from appsettings.json
```

```json
// appsettings.json
{
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-influxdb-token",
    "Org": "your-org",
    "Bucket": "testfuzn"
  }
}
```

### Available Metrics

When using InfluxDB, the following metrics are streamed in real-time:

**Scenario Metrics** (measurement: `scenario_metrics`)
- `request_count`, `requests_per_second`, `total_execution_duration_ms`
- OK metrics: `ok_request_count`, `ok_response_time_mean_ms`, `ok_response_time_percentile_99_ms`, etc.
- Failed metrics: `failed_request_count`, `failed_response_time_mean_ms`, etc.

**Step Metrics** (measurement: `step_metrics`)
- Same metrics as scenario, but per individual step

### Grafana Dashboard

Create a Grafana dashboard to visualize:
- Requests per second over time
- Response time percentiles
- Error rate trends
- Concurrent user counts

---

## API Reference

### ScenarioBuilder Methods

| Method | Description |
|--------|-------------|
| `Id(string)` | Set scenario ID |
| `BeforeScenario(Action/Func)` | Add scenario initialization hook |
| `AfterScenario(Action/Func)` | Add scenario cleanup hook |
| `BeforeIteration(Action/Func)` | Add iteration initialization hook |
| `AfterIteration(Action/Func)` | Add iteration cleanup hook |
| `InputData(params object[])` | Provide static input data |
| `InputDataFromList(Func)` | Provide input data from function |
| `InputDataBehavior(InputDataBehavior)` | Set input data consumption behavior |
| `Step(string, Action/Func)` | Add a step |
| `Step(string, string, Action/Func)` | Add a step with ID |
| `Load()` | Access load testing configuration |
| `Run()` | Execute the scenario |

### IterationContext Members

| Member | Description |
|--------|-------------|
| `Model` | Access custom context model |
| `Logger` | ILogger instance for logging |
| `InputData<T>()` | Get input data for current iteration |
| `SetSharedData(string, object)` | Store shared data |
| `GetSharedData<T>(string)` | Retrieve shared data |
| `Step(string, Action/Func)` | Create sub-step |
| `Comment(string)` | Add comment to execution |
| `Attach(string, content)` | Attach file to step |
| `CreateHttpRequest(string)` | Create HTTP request (requires HTTP plugin) |
| `CreateBrowserPage()` | Create Playwright browser page (requires Playwright plugin) |

### Load Testing Methods

| Method | Description |
|--------|-------------|
| `Warmup(Action)` | Configure warmup simulations |
| `Simulations(Action/Func)` | Configure load simulations |
| `AssertWhileRunning(Action)` | Add runtime assertions |
| `AssertWhenDone(Action)` | Add post-execution assertions |
| `IncludeScenario(ScenarioBuilder)` | Include another scenario |

### Simulation Types

| Method | Description |
|--------|-------------|
| `OneTimeLoad(count)` | Execute N iterations |
| `GradualLoadIncrease(start, end, duration)` | Ramp load gradually |
| `FixedLoad(rate, duration)` | Constant requests/second |
| `FixedConcurrentLoad(count, duration)` | Constant concurrent users |
| `RandomLoadPerSecond(min, max, duration)` | Random load in range |
| `Pause(duration)` | Pause between simulations |

---

## Examples

### Example 1: Unit Test with Validation

```csharp
[TestClass]
[Group("Product Unit Tests")]
public class ProductUnitTests : Test
{
    [Test(Name = "Verify product validation rejects empty names", Id = "UT-001")]
    [Tags("Unit", "Validation")]
    public async Task Verify_product_name_validation()
    {
        await Scenario()
            .Step("Create product with empty name", context =>
            {
                var product = new Product { Name = "", Price = 100 };
                context.SetSharedData("product", product);
            })
            .Step("Validate product name is invalid", context =>
            {
                var product = context.GetSharedData<Product>("product");
                var isValid = !string.IsNullOrWhiteSpace(product.Name);
                Assert.IsFalse(isValid, "Empty product name should be invalid");
            })
            .Run();
    }
}
```

### Example 2: Data-Driven Test

```csharp
[Test]
[Tags("Unit", "DataDriven")]
public async Task Validate_multiple_products()
{
    await Scenario()
        .InputData(
            new Product { Name = "Product A", Price = 10 },
            new Product { Name = "Product B", Price = 20 },
            new Product { Name = "Product C", Price = 30 }
        )
        .InputDataBehavior(InputDataBehavior.Loop)
        .Step("Validate each product", context =>
        {
            var product = context.InputData<Product>();
            context.Comment($"Validating: {product.Name}");
            
            Assert.IsFalse(string.IsNullOrWhiteSpace(product.Name));
            Assert.IsTrue(product.Price > 0);
        })
        .Run();
}
```

### Example 3: Integration Test with Custom Context

```csharp
private class ProductTestContext
{
    public string AuthToken { get; set; }
    public Product CreatedProduct { get; set; }
}

[Test(Name = "Complete product lifecycle test", Id = "IT-001")]
[Tags("Integration", "CRUD")]
[Metadata("Category", "API")]
public async Task Product_lifecycle_test()
{
    await Scenario<ProductTestContext>()
        .BeforeScenario(context => context.Logger.LogInformation("Starting lifecycle test"))
        .Step("Authenticate", async context =>
        {
            var response = await context.CreateHttpRequest($"{BaseUrl}/api/Auth/token")
                .Body(new { Username = "admin", Password = "admin123" })
                .Post();
            
            Assert.IsTrue(response.Ok);
            context.Model.AuthToken = response.BodyAs<TokenResponse>().Token;
        })
        .Step("Create product", async context =>
        {
            context.Model.CreatedProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Product",
                Price = 99.99m
            };
            
            var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products")
                .AuthBearer(context.Model.AuthToken)
                .Body(context.Model.CreatedProduct)
                .Post();
            
            Assert.IsTrue(response.Ok);
        })
        .Step("Verify product exists", async context =>
        {
            var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.CreatedProduct.Id}")
                .AuthBearer(context.Model.AuthToken)
                .Get();
            
            Assert.IsTrue(response.Ok);
            var product = response.BodyAs<Product>();
            Assert.AreEqual(context.Model.CreatedProduct.Name, product.Name);
        })
        .Step("Delete product", async context =>
        {
            var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.CreatedProduct.Id}")
                .AuthBearer(context.Model.AuthToken)
                .Delete();
            
            Assert.IsTrue(response.Ok);
        })
        .AfterScenario(context => context.Logger.LogInformation("Lifecycle test complete"))
        .Run();
}
```

### Example 4: Load Test with Assertions

```csharp
[Test(Name = "API performance load test")]
[Tags("Load", "Performance")]
public async Task API_load_test()
{
    await Scenario()
        .Step("Call API endpoint", async context =>
        {
            var response = await context.CreateHttpRequest("https://api.example.com/health")
                .Get();
            
            Assert.IsTrue(response.Ok);
        })
        .Load().Warmup((context, simulations) =>
        {
            simulations.FixedConcurrentLoad(5, TimeSpan.FromSeconds(3));
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.GradualLoadIncrease(1, 100, TimeSpan.FromSeconds(10));
            simulations.FixedLoad(100, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
        })
        .Load().AssertWhileRunning((context, stats) =>
        {
            Assert.AreEqual(0, stats.Failed.RequestCount, "No failures during test");
        })
        .Load().AssertWhenDone((context, stats) =>
        {
            Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromMilliseconds(500));
            Assert.IsTrue(stats.Ok.ResponseTimePercentile99 < TimeSpan.FromSeconds(1));
            Assert.AreEqual(0, stats.Failed.RequestCount);
        })
        .Run();
}
```

### Example 5: Playwright UI Test

```csharp
[Test(Name = "Verify login and product creation")]
[Tags("UI", "E2E")]
public async Task Login_and_create_product()
{
    await Scenario()
        .Step("Login and create product", async context =>
        {
            var page = await context.CreateBrowserPage();
            await page.GotoAsync("https://localhost:44316/");
            
            // Login
            await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter username" }).FillAsync("admin");
            await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).FillAsync("admin123");
            await page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            
            await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Welcome");
            
            // Take screenshot
            var screenshot = await page.ScreenshotAsync();
            await context.Attach("login-success.png", screenshot);
        })
        .Run();
}
```

---

## Best Practices

1. **Use descriptive names** - Test and step names should clearly describe what's being tested
2. **Leverage custom context** - Use typed context models for complex scenarios
3. **Use shared steps** - Create extension methods for common patterns
4. **Add meaningful logs** - Use `context.Logger` and `context.Comment()` for debugging
5. **Attach evidence** - Capture screenshots, responses, etc. for failed tests
6. **Use warmup** - Always warm up before load tests
7. **Set realistic assertions** - Base load test assertions on actual SLAs
8. **Organize with groups and tags** - Use `[Group]` and `[Tags]` for organization
9. **Clean up resources** - Use `AfterIteration` and `AfterScenario` for cleanup
10. **Use async/await** - Leverage async throughout for better performance