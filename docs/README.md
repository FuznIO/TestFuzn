## 📚 Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Test Types](#test-types)
- [Writing Tests](#writing-tests)
  - [Basic Scenario](#basic-scenario)
  - [Test Attributes](#test-attributes)
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
  - [Warmup](#warmup)
  - [Simulations](#simulations)
  - [Assertions](#assertions)
  - [Statistics](#statistics)
  - [Including Scenarios](#including-scenarios)
- [HTTP Testing](#http-testing)
  - [Basic HTTP Requests](#basic-http-requests)
  - [Request Methods](#request-methods)
  - [JSON Serialization](#json-serialization)
  - [HTTP Load Testing](#http-load-testing)
- [Web UI Testing with Playwright](#web-ui-testing-with-playwright)
  - [Basic Browser Testing](#basic-browser-testing)
  - [Browser Interactions](#browser-interactions)
  - [UI Load Testing](#ui-load-testing)
- [API Reference](#api-reference)
- [Examples](#examples)

---

## Overview

TestFuzn is a modern testing framework for .NET that provides a fluent, scenario-based approach to writing both feature tests and load tests. It combines the simplicity of traditional unit testing with powerful capabilities for complex test scenarios, data-driven testing, and performance validation.

**Additional Features:**
- 🎯 Fluent API for readable, maintainable tests
- 📊 Built-in support for feature and load testing
- 🔄 Flexible input data management
- 📈 Real-time and post-execution statistics
- 🔌 Extensible with shared steps
- 📎 File attachments and logging support
- ⚡ Async/await support throughout
- 🎨 Custom context models for type-safe data sharing

---

## Installation

Install TestFuzn via NuGet:

```bash
dotnet add package TestFuzn
```

For HTTP testing support:

```bash
dotnet add package TestFuzn.Plugins.Http
```

For Playwright/browser testing support:

```bash
dotnet add package TestFuzn.Plugins.Playwright
```

---

## Quick Start

Here's a simple feature test to get you started:

```csharp
using Microsoft.Extensions.Logging;

[FeatureTest]
public class MyFirstTest : BaseFeatureTest
{
    public override string FeatureName => "My First Feature";
    public override string FeatureId => "FEAT-001";

    [ScenarioTest]
    public async Task SimpleScenario()
    {
        await Scenario("Hello TestFuzn")
            .Step("Step 1", context =>
            {
                context.Logger.LogInformation("Hello from TestFuzn!");
            })
            .Step("Step 2", async context =>
            {
                await Task.CompletedTask;
                Assert.IsTrue(true);
            })
            .Run();
    }
}
```

---

## Core Concepts

### Test Class Structure

All test classes should:
1. Inherit from `BaseFeatureTest`
2. Be decorated with `[FeatureTest]` attribute
3. Override `FeatureName` and `FeatureId` properties
4. Contain test methods decorated with `[ScenarioTest]`

### Execution Flow

```
Test Class Init
    ↓
Scenario Init (InitScenario)
    ↓
For each iteration:
    ↓
    Iteration Init (InitIteration)
    ↓
    Execute Steps (in order)
    ↓
    Iteration Cleanup (CleanupIteration)
    ↓
Scenario Cleanup (CleanupScenario)
```

---

## Test Types

TestFuzn supports two types of tests:

### Feature Test
A test that runs once with one set of input data, or runs multiple times with different input data. Ideal for functional testing and validation.

```csharp
[ScenarioTest]
public async Task FeatureTestExample()
{
    await Scenario("User Registration")
        .InputData("user1@example.com", "user2@example.com", "user3@example.com")
        .Step("Register User", context =>
        {
            var email = context.InputData<string>();
            // Registration logic here
        })
        .Run();
}
```

### Load Test
A test that runs multiple times with the same or different input data to simulate load on the system. Perfect for performance testing and bottleneck identification.

```csharp
[ScenarioTest]
public async Task LoadTestExample()
{
    await Scenario("API Load Test")
        .Step("Call API", async context =>
        {
            // API call logic
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.FixedConcurrentLoad(100, TimeSpan.FromSeconds(30));
        })
        .Run();
}
```

---

## Writing Tests

### Basic Scenario

Create a scenario using the fluent `Scenario` builder:

```csharp
[ScenarioTest]
public async Task BasicScenario()
{
    await Scenario("My Scenario Name")
        .Step("First Step", context =>
        {
            // Your test logic
        })
        .Step("Second Step", async context =>
        {
            // Async test logic
            await Task.CompletedTask;
        })
        .Run();
}
```

### Test Attributes

Control test execution and categorization with attributes:

```csharp
[ScenarioTest(ScenarioRunMode.Skip)]  // Skip this test
[TestCategory("Smoke")]               // Categorize tests
[TestCategory("Integration")]
[Environments("test", "staging")]     // Target specific environments
public async Task AttributeExample()
{
    // Test implementation
}
```

### Scenario Configuration

Add metadata and identifiers to scenarios:

```csharp
await Scenario("Configured Scenario")
    .Id("SCEN-1234")                    // Optional scenario ID
    .Metadata("priority", "high")        // Add metadata
    .Metadata("team", "backend")
    .Step("Step", context => { })
    .Run();
```

### Steps

Steps are the building blocks of scenarios. They execute in order, and if one fails, subsequent steps are skipped.

#### Synchronous Steps

```csharp
.Step("Sync Step", context =>
{
    // Synchronous logic
    context.Logger.LogInformation("Executing step");
})
```

#### Asynchronous Steps

```csharp
.Step("Async Step", async context =>
{
    await SomeAsyncOperation();
    context.Logger.LogInformation("Completed async step");
})
```

#### Steps with IDs

```csharp
.Step("Important Step", "STEP-001", async context =>
{
    // Step with explicit ID for tracking
})
```

#### Using Method References

```csharp
.Step("Shared Step", SharedStepAction)
.Step("Method Call", context => SharedMethod("value"))
```

### Input Data

Provide test data to drive multiple iterations:

#### Simple Input Data

```csharp
.InputData("user1", "user2", "user3")
```

#### Input Data from List (Sync)

```csharp
.InputDataFromList((context) =>
{
    return new List<object>
    {
        "user1",
        "user2",
        "user3"
    };
})
```

#### Input Data from List (Async)

```csharp
.InputDataFromList(async (context) =>
{
    // Load from database, API, file, etc.
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

[ScenarioTest]
public async Task CustomContextExample()
{
    await Scenario<LoginContext>("User Login Flow")
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

Control execution at different stages:

#### Scenario Level

```csharp
.InitScenario((context) =>
{
    // Runs once before any iterations
    // Setup shared resources
})
.CleanupScenario((context) =>
{
    // Runs once after all iterations
    // Cleanup resources
})
```

#### Iteration Level

```csharp
.InitIteration((context) =>
{
    // Runs before each iteration's steps
    // Setup iteration-specific resources
})
.CleanupIteration((context) =>
{
    // Runs after each iteration's steps
    // Cleanup iteration resources
})
```

#### Test Method Level

Implement interfaces for class-level hooks:

```csharp
[FeatureTest]
public class MyTest : BaseFeatureTest, IInitScenarioTestMethod, ICleanupScenarioTestMethod
{
    public Task InitScenarioTestMethod(Context context)
    {
        // Runs before scenario execution
        return Task.CompletedTask;
    }

    public Task CleanupScenarioTestMethod(Context context)
    {
        // Runs after scenario execution
        return Task.CompletedTask;
    }
}
```

---

## Advanced Features

### Sub-Steps

Create nested step hierarchies for better organization:

```csharp
.Step("Parent Step", context =>
{
    context.Step("Child Step 1", subcontext =>
    {
        subcontext.Step("Grandchild Step", grandcontext =>
        {
            // Deeply nested logic
        });
    });
    
    context.Step("Child Step 2", subcontext =>
    {
        // More nested logic
    });
})
```

### Shared Steps

Create reusable steps across tests:

#### Extension Method Approach

```csharp
public static class SharedSteps
{
    public static ScenarioBuilder<T> LoginStep<T>(this ScenarioBuilder<T> builder, string username)
        where T : new()
    {
        return builder.Step($"Login as {username}", context =>
        {
            // Reusable login logic
            context.Logger.LogInformation($"Logging in as {username}");
        });
    }
}

// Usage
await Scenario("My Test")
    .LoginStep("testuser")
    .Step("Next Step", context => { })
    .Run();
```

#### Method Reference Approach

```csharp
public async Task SharedLoginStep(IterationContext<EmptyModel> context)
{
    var username = context.InputData<string>();
    // Login logic
}

// Usage
.Step("Login", SharedLoginStep)
```

### Comments and Attachments

#### Comments

Add contextual information to test execution:

```csharp
.Step("Browser Actions", async context =>
{
    context.Comment("Opening browser");
    // Open browser
    
    context.Comment("Navigating to URL");
    // Navigate
    
    context.Comment("Closing browser");
    // Close
})
```

- **Feature tests**: Comments output to console and reports
- **Load tests**: Comments output to log files

#### Attachments

Attach files to steps for evidence and debugging:

```csharp
.Step("Capture Evidence", async context =>
{
    // Attach text content
    await context.Attach("log.txt", "Log content here");
    
    // Attach byte array
    var screenshot = CaptureScreenshot();
    await context.Attach("screenshot.png", screenshot);
    
    // Attach stream
    using var fileStream = File.OpenRead("evidence.json");
    await context.Attach("evidence.json", fileStream);
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
    context.Logger.LogDebug("Debug details: {Details}", detailsObject);
})
```

---

## Load Testing

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

#### Gradual Load Increase

Ramp up load gradually:

```csharp
.Load().Simulations((context, simulations) =>
{
    // Start with 1, increase to 100 over 30 seconds
    simulations.GradualLoadIncrease(1, 100, TimeSpan.FromSeconds(30));
})
```

#### Fixed Load

Maintain constant requests per second:

```csharp
simulations.FixedLoad(
    requestsPerSecond: 50,
    rampUpTime: TimeSpan.FromSeconds(10),
    duration: TimeSpan.FromMinutes(5)
);
```

#### One-Time Load

Execute a specific number of iterations:

```csharp
simulations.OneTimeLoad(1000); // Execute 1000 times
```

#### Fixed Concurrent Load

Maintain a constant number of concurrent users:

```csharp
simulations.FixedConcurrentLoad(
    concurrentUsers: 500,
    duration: TimeSpan.FromMinutes(10)
);
```

#### Random Load Per Second

Vary load randomly within a range:

```csharp
simulations.RandomLoadPerSecond(
    minRequestsPerSecond: 10,
    maxRequestsPerSecond: 100,
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
    simulations.FixedLoad(50, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(2));
    simulations.Pause(TimeSpan.FromSeconds(10));
    simulations.FixedConcurrentLoad(100, TimeSpan.FromMinutes(3));
})
```

#### Async Simulations

```csharp
.Load().Simulations(async (context, simulations) =>
{
    var config = await LoadConfigFromDatabase();
    simulations.FixedLoad(config.RPS, config.Duration);
})
```

### Assertions

Validate performance during and after test execution:

#### Assert While Running

Continuous validation during the test (doesn't stop the test):

```csharp
.Load().AssertWhileRunning((context, stats) =>
{
    Assert.IsTrue(stats.RequestCount < 10000, "Too many requests");
    Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(1), "Response too slow");
    Assert.IsTrue(stats.Failed.RequestCount == 0, "Should have no failures");
})
```

#### Assert When Done

Final validation after test completion:

```csharp
.Load().AssertWhenDone((context, stats) =>
{
    Assert.AreEqual(1000, stats.RequestCount, "Expected 1000 total requests");
    Assert.IsTrue(stats.Ok.RequestCount >= 950, "At least 95% success rate");
    Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(0.5), "Mean response time too high");
    Assert.IsTrue(stats.Ok.ResponseTimeStandardDeviation < TimeSpan.FromSeconds(0.2), "Too much variance");
})
```

### Statistics

Access comprehensive performance metrics:

#### Available Metrics

```csharp
stats.RequestCount                          // Total requests
stats.Ok.RequestCount                       // Successful requests
stats.Failed.RequestCount                   // Failed requests
stats.Ok.ResponseTimeMean                   // Average response time (successful)
stats.Failed.ResponseTimeMean               // Average response time (failed)
stats.Ok.ResponseTimeStandardDeviation      // Response time std deviation
stats.Ok.ResponseTimeMin                    // Minimum response time
stats.Ok.ResponseTimeMax                    // Maximum response time
```

#### Per-Step Statistics

Get statistics for specific steps:

```csharp
.Load().AssertWhenDone((context, stats) =>
{
    var loginStats = stats.GetStep("Login");
    Assert.IsTrue(loginStats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(0.3));
    
    var checkoutStats = stats.GetStep("Checkout");
    Assert.IsTrue(checkoutStats.Ok.RequestCount > 0);
})
```

### Including Scenarios

Compose load tests from multiple scenarios:

```csharp
.Load().IncludeScenario(
    Scenario("Additional Scenario")
        .Step("Step 1", context => { })
        .Step("Step 2", context => { })
)
```

---

## HTTP Testing

TestFuzn provides a fluent HTTP client plugin for testing REST APIs and HTTP endpoints. Test any HTTP-based system regardless of the technology stack.

### Basic HTTP Requests

Use `CreateHttpRequest()` to make HTTP calls:

```csharp
using Fuzn.TestFuzn.Plugins.Http;

[FeatureTest]
public class ApiTests : BaseFeatureTest
{
    public override string FeatureName => "API Tests";
    public override string FeatureId => "API-001";

    [ScenarioTest]
    public async Task GetProducts()
    {
        await Scenario("Get Products from API")
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
}
```

### Request Methods

#### GET Request

```csharp
.Step("GET request", async context =>
{
    var response = await context.CreateHttpRequest("https://api.example.com/users/123")
        .Get();
    
    Assert.IsTrue(response.Ok);
    var user = response.BodyAs<User>();
    Assert.IsNotNull(user.Email);
})
```

#### POST Request with Object

```csharp
.Step("POST request with object", async context =>
{
    var newProduct = new Product 
    { 
        Name = "Test Product", 
        Price = 99.99m 
    };
    
    var response = await context.CreateHttpRequest("https://api.example.com/products")
        .Body(newProduct)
        .Post();
    
    Assert.IsTrue(response.Ok);
    var created = response.BodyAs<Product>();
    Assert.IsNotNull(created.Id);
})
```

#### POST Request with JSON String

```csharp
.Step("POST with raw JSON", async context =>
{
    var response = await context.CreateHttpRequest("https://api.example.com/orders")
        .Body(@"{
            ""customerId"": ""12345"",
            ""items"": [
                { ""productId"": ""A1"", ""quantity"": 2 }
            ]
        }")
        .Post();
    
    Assert.IsTrue(response.Ok);
})
```

#### PUT Request

```csharp
.Step("PUT request", async context =>
{
    var updatedUser = new User { Id = 123, Name = "Updated Name" };
    
    var response = await context.CreateHttpRequest("https://api.example.com/users/123")
        .Body(updatedUser)
        .Put();
    
    Assert.IsTrue(response.Ok);
})
```

#### DELETE Request

```csharp
.Step("DELETE request", async context =>
{
    var response = await context.CreateHttpRequest("https://api.example.com/users/123")
        .Delete();
    
    Assert.IsTrue(response.Ok);
})
```

### JSON Serialization

TestFuzn supports both System.Text.Json and Newtonsoft.Json:

#### Using System.Text.Json (Default)

```csharp
.Step("Use System.Text.Json", async context =>
{
    var serializer = new SystemTextJsonSerializerProvider();
    
    var response = await context.CreateHttpRequest("https://api.example.com/products")
        .SerializerProvider(serializer)
        .Get();
    
    var products = response.BodyAs<List<Product>>();
})
```

#### Using Newtonsoft.Json

```csharp
.Step("Use Newtonsoft.Json", async context =>
{
    var serializer = new NewtonsoftSerializerProvider();
    
    var response = await context.CreateHttpRequest("https://api.example.com/products")
        .SerializerProvider(serializer)
        .Get();
    
    var products = response.BodyAs<List<Product>>();
})
```

#### Working with Dynamic JSON

```csharp
.Step("Parse JSON dynamically", async context =>
{
    var response = await context.CreateHttpRequest("https://api.example.com/config")
        .Get();
    
    var json = response.BodyAsJson();
    Assert.AreEqual("production", json.environment);
    Assert.AreEqual(true, json.features.newUI);
})
```

### HTTP Load Testing

Combine HTTP requests with load testing:

```csharp
[ScenarioTest]
public async Task ApiLoadTest()
{
    await Scenario("API Endpoint Load Test")
        .Step("Call API endpoint", async context =>
        {
            var response = await context.CreateHttpRequest("https://api.example.com/health")
                .Get();
            
            Assert.IsTrue(response.Ok);
            Assert.AreEqual("healthy", response.BodyAs<string>());
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.FixedLoad(50, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        })
        .Load().AssertWhenDone((context, stats) =>
        {
            Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(1));
            Assert.IsTrue(stats.Ok.RequestCount >= 3000); // 50 RPS * 60 seconds
        })
        .Run();
}
```

### Advanced HTTP Scenarios

#### CRUD Operations Test

```csharp
[ScenarioTest]
public async Task CompleteProductCRUD()
{
    await Scenario("Product CRUD Operations")
        .Step("Create product", async context =>
        {
            var product = new Product { Name = "Test Product", Price = 49.99m };
            var response = await context.CreateHttpRequest("https://api.example.com/products")
                .Body(product)
                .Post();
            
            Assert.IsTrue(response.Ok);
            var created = response.BodyAs<Product>();
            context.SetSharedData("productId", created.Id);
        })
        .Step("Read product", async context =>
        {
            var productId = context.GetSharedData<string>("productId");
            var response = await context.CreateHttpRequest($"https://api.example.com/products/{productId}")
                .Get();
            
            Assert.IsTrue(response.Ok);
            var product = response.BodyAs<Product>();
            Assert.AreEqual("Test Product", product.Name);
        })
        .Step("Update product", async context =>
        {
            var productId = context.GetSharedData<string>("productId");
            var updated = new Product { Id = productId, Name = "Updated Product", Price = 59.99m };
            
            var response = await context.CreateHttpRequest($"https://api.example.com/products/{productId}")
                .Body(updated)
                .Put();
            
            Assert.IsTrue(response.Ok);
        })
        .Step("Delete product", async context =>
        {
            var productId = context.GetSharedData<string>("productId");
            var response = await context.CreateHttpRequest($"https://api.example.com/products/{productId}")
                .Delete();
            
            Assert.IsTrue(response.Ok);
        })
        .Run();
}
```

---

## Web UI Testing with Playwright

TestFuzn integrates seamlessly with Microsoft Playwright for browser automation and UI testing. Test any web application regardless of its technology stack.

### Basic Browser Testing

Create browser-based tests using the `CreateBrowserPage()` extension method:

```csharp
using Fuzn.TestFuzn.Plugins.Playwright;

[FeatureTest]
public class WebUITests : BaseFeatureTest
{
    public override string FeatureName => "Web UI Tests";
    public override string FeatureId => "UI-001";

    [ScenarioTest]
    public async Task VerifyHomePage()
    {
        await Scenario("Verify Home Page Loads")
            .Step("Navigate and verify title", async context =>
            {
                var page = await context.CreateBrowserPage();
                await page.GotoAsync("https://example.com");
                
                var title = await page.TitleAsync();
                Assert.AreEqual("Example Domain", title);
            })
            .Run();
    }
}
```

### Browser Interactions

Playwright provides full browser automation capabilities:

```csharp
[ScenarioTest]
public async Task LoginFlow()
{
    await Scenario("User Login")
        .Step("Navigate to login page", async context =>
        {
            var page = await context.CreateBrowserPage();
            await page.GotoAsync("https://myapp.com/login");
            
            context.SetSharedData("page", page);
        })
        .Step("Enter credentials", async context =>
        {
            var page = context.GetSharedData<IPage>("page");
            
            await page.FillAsync("#username", "testuser");
            await page.FillAsync("#password", "password123");
            
            context.Comment("Credentials entered");
        })
        .Step("Submit and verify", async context =>
        {
            var page = context.GetSharedData<IPage>("page");
            
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync("**/dashboard");
            
            var welcomeText = await page.TextContentAsync(".welcome-message");
            Assert.IsTrue(welcomeText.Contains("Welcome"));
            
            // Take screenshot for evidence
            var screenshot = await page.ScreenshotAsync();
            await context.Attach("dashboard.png", screenshot);
        })
        .Run();
}
```

### Advanced Browser Testing

#### Multi-Step User Journey

```csharp
[ScenarioTest]
public async Task CompleteCheckoutJourney()
{
    await Scenario("E-commerce Checkout")
        .Step("Browse products", async context =>
        {
            var page = await context.CreateBrowserPage();
            await page.GotoAsync("https://shop.example.com");
            
            await page.ClickAsync(".product-card:first-child");
            await page.ClickAsync("button.add-to-cart");
            
            context.SetSharedData("page", page);
        })
        .Step("View cart", async context =>
        {
            var page = context.GetSharedData<IPage>("page");
            
            await page.ClickAsync(".cart-icon");
            var itemCount = await page.TextContentAsync(".cart-count");
            Assert.AreEqual("1", itemCount);
        })
        .Step("Proceed to checkout", async context =>
        {
            var page = context.GetSharedData<IPage>("page");
            
            await page.ClickAsync("button.checkout");
            await page.FillAsync("#email", "customer@example.com");
            await page.FillAsync("#address", "123 Main St");
            await page.SelectOptionAsync("#country", "US");
        })
        .Step("Complete payment", async context =>
        {
            var page = context.GetSharedData<IPage>("page");
            
            await page.FillAsync("#cardNumber", "4111111111111111");
            await page.FillAsync("#cardExpiry", "12/25");
            await page.FillAsync("#cardCvc", "123");
            
            await page.ClickAsync("button.pay-now");
            await page.WaitForSelectorAsync(".order-confirmation");
            
            var confirmationText = await page.TextContentAsync(".confirmation-message");
            Assert.IsTrue(confirmationText.Contains("Thank you"));
        })
        .Run();
}
```

#### Testing with Different Browsers

```csharp
[ScenarioTest]
public async Task CrossBrowserTesting()
{
    await Scenario("Cross-Browser Compatibility")
        .InputData("chromium", "firefox", "webkit")
        .Step("Test in different browsers", async context =>
        {
            var browserType = context.InputData<string>();
            
            var page = await context.CreateBrowserPage(browserType);
            await page.GotoAsync("https://myapp.com");
            
            var isVisible = await page.IsVisibleAsync(".main-content");
            Assert.IsTrue(isVisible, $"Content not visible in {browserType}");
            
            context.Logger.LogInformation("Test passed in {Browser}", browserType);
        })
        .Run();
}
```

### UI Load Testing

Combine Playwright with load testing to simulate multiple concurrent users:

```csharp
[ScenarioTest]
public async Task UILoadTest()
{
    await Scenario("UI Load Test")
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
        .Load().Warmup((context, simulations) =>
        {
            simulations.FixedConcurrentLoad(2, TimeSpan.FromSeconds(5));
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.OneTimeLoad(10);
        })
        .Load().AssertWhenDone((context, stats) =>
        {
            Assert.AreEqual(10, stats.Ok.RequestCount);
            Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(5));
        })
        .Run();
}
```

### Best Practices for Playwright Tests

1. **Reuse browser pages** - Store page instances in shared data for multi-step scenarios
2. **Use meaningful selectors** - Prefer data-testid or semantic selectors over CSS classes
3. **Wait for elements** - Always wait for elements before interacting
4. **Capture screenshots** - Attach screenshots on failures for debugging
5. **Clean up resources** - Browser pages are automatically disposed after each iteration
6. **Handle timeouts** - Configure appropriate timeouts for slow-loading pages
7. **Use headless mode** - Run in headless mode for CI/CD pipelines (default)

---

## API Reference

### ScenarioBuilder<T> Methods

| Method | Description |
|--------|-------------|
| `Id(string)` | Set scenario ID |
| `Metadata(string, string)` | Add metadata key-value pair |
| `InitScenario(Action/Func)` | Add scenario initialization hook |
| `CleanupScenario(Action/Func)` | Add scenario cleanup hook |
| `InitIteration(Action/Func)` | Add iteration initialization hook |
| `CleanupIteration(Action/Func)` | Add iteration cleanup hook |
| `InputData(params object[])` | Provide input data |
| `InputDataFromList(Func<List<object>>)` | Provide input data from function |
| `InputDataBehavior(InputDataBehavior)` | Set input data consumption behavior |
| `Step(string, Action/Func)` | Add a step |
| `Step(string, string, Action/Func)` | Add a step with ID |
| `Load()` | Access load testing configuration |
| `Run()` | Execute the scenario |

### IterationContext<T> Properties & Methods

| Member | Description |
|--------|-------------|
| `Model` | Access custom context model |
| `Logger` | ILogger instance for logging |
| `InputData<TData>()` | Get input data for current iteration |
| `SetSharedData(string, object)` | Store shared data |
| `GetSharedData<TData>(string)` | Retrieve shared data |
| `Step(string, Action)` | Create sub-step |
| `Comment(string)` | Add comment to execution |
| `Attach(string, string/byte[]/Stream)` | Attach file to step |
| `CreateHttpRequest(string)` | Create HTTP request builder |
| `CreateBrowserPage(string)` | Create Playwright browser page |

### Load Testing Methods

| Method | Description |
|--------|-------------|
| `Warmup(Action)` | Configure warmup simulations |
| `Simulations(Action/Func)` | Configure load simulations |
| `AssertWhileRunning(Action)` | Add runtime assertions |
| `AssertWhenDone(Action)` | Add post-execution assertions |
| `IncludeScenario(ScenarioBuilder)` | Include another scenario |

### HTTP Request Methods

| Method | Description |
|--------|-------------|
| `Get()` | Execute GET request |
| `Post()` | Execute POST request |
| `Put()` | Execute PUT request |
| `Delete()` | Execute DELETE request |
| `Body(object/string)` | Set request body |
| `SerializerProvider(ISerializer)` | Set JSON serializer |

### HTTP Response Methods

| Member | Description |
|--------|-------------|
| `Ok` | Boolean indicating success (2xx status) |
| `BodyAs<T>()` | Deserialize response to type T |
| `BodyAsJson()` | Parse response as dynamic JSON |

### Simulation Types

| Method | Parameters | Description |
|--------|------------|-------------|
| `GradualLoadIncrease` | start, end, duration | Ramp load gradually |
| `FixedLoad` | rps, rampUp, duration | Constant requests/second |
| `OneTimeLoad` | iterations | Execute N times |
| `FixedConcurrentLoad` | users, duration | Constant concurrent users |
| `RandomLoadPerSecond` | min, max, duration | Random load in range |
| `Pause` | duration | Pause between simulations |

---

## Examples

### Example 1: Simple Feature Test

```csharp
[FeatureTest]
public class UserTests : BaseFeatureTest
{
    public override string FeatureName => "User Management";
    public override string FeatureId => "USER-001";

    [ScenarioTest]
    [TestCategory("Smoke")]
    public async Task CreateUser()
    {
        await Scenario("Create New User")
            .Step("Prepare user data", context =>
            {
                context.SetSharedData("email", "test@example.com");
                context.SetSharedData("name", "Test User");
            })
            .Step("Create user", async context =>
            {
                var email = context.GetSharedData<string>("email");
                var name = context.GetSharedData<string>("name");
                
                var user = await userService.CreateUser(email, name);
                context.SetSharedData("userId", user.Id);
                
                context.Logger.LogInformation("User created: {UserId}", user.Id);
            })
            .Step("Verify user", async context =>
            {
                var userId = context.GetSharedData<int>("userId");
                var user = await userService.GetUser(userId);
                
                Assert.IsNotNull(user);
                Assert.AreEqual("test@example.com", user.Email);
            })
            .Run();
    }
}
```

### Example 2: Data-Driven Feature Test

```csharp
[ScenarioTest]
public async Task ValidateMultipleUsers()
{
    await Scenario("Validate User Creation")
        .InputDataFromList(async (context) =>
        {
            return new List<object>
            {
                new { Email = "user1@example.com", Name = "User One" },
                new { Email = "user2@example.com", Name = "User Two" },
                new { Email = "user3@example.com", Name = "User Three" }
            };
        })
        .InputDataBehavior(InputDataBehavior.Loop)
        .Step("Create and validate user", async context =>
        {
            dynamic userData = context.InputData<object>();
            
            var user = await userService.CreateUser(userData.Email, userData.Name);
            Assert.AreEqual(userData.Email, user.Email);
            
            context.Logger.LogInformation("Validated user: {Email}", userData.Email);
        })
        .Run();
}
```

### Example 3: Custom Context Example

```csharp
public class ShoppingCartContext
{
    public string UserId { get; set; }
    public string CartId { get; set; }
    public List<string> ProductIds { get; set; } = new();
    public decimal Total { get; set; }
}

[ScenarioTest]
public async Task ShoppingCartFlow()
{
    await Scenario<ShoppingCartContext>("Complete Purchase Flow")
        .InitScenario(context =>
        {
            context.Model.ProductIds = new List<string>();
        })
        .Step("Login", async context =>
        {
            var userId = await Login("customer@example.com");
            context.Model.UserId = userId;
        })
        .Step("Create Cart", async context =>
        {
            var cart = await cartService.CreateCart(context.Model.UserId);
            context.Model.CartId = cart.Id;
        })
        .Step("Add Products", async context =>
        {
            await cartService.AddItem(context.Model.CartId, "PROD-001");
            await cartService.AddItem(context.Model.CartId, "PROD-002");
            context.Model.ProductIds.Add("PROD-001");
            context.Model.ProductIds.Add("PROD-002");
        })
        .Step("Calculate Total", async context =>
        {
            var cart = await cartService.GetCart(context.Model.CartId);
            context.Model.Total = cart.Total;
            Assert.IsTrue(context.Model.Total > 0);
        })
        .Step("Checkout", async context =>
        {
            var order = await checkoutService.Checkout(context.Model.CartId);
            Assert.IsNotNull(order.Id);
            context.Logger.LogInformation("Order created: {OrderId}, Total: {Total}", 
                order.Id, context.Model.Total);
        })
        .Run();
}
```

### Example 4: Load Test with Assertions

```csharp
[ScenarioTest]
public async Task ApiLoadTest()
{
    await Scenario("API Endpoint Load Test")
        .Step("Call API", async context =>
        {
            var response = await apiClient.GetAsync("/api/products");
            response.EnsureSuccessStatusCode();
        })
        .Load().Warmup((context, simulations) =>
        {
            simulations.FixedConcurrentLoad(5, TimeSpan.FromSeconds(3));
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.GradualLoadIncrease(1, 50, TimeSpan.FromSeconds(10));
            simulations.FixedLoad(50, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
            simulations.FixedConcurrentLoad(100, TimeSpan.FromSeconds(30));
        })
        .Load().AssertWhileRunning((context, stats) =>
        {
            Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(2), 
                "Response time exceeds 2s during test");
        })
        .Load().AssertWhenDone((context, stats) =>
        {
            Assert.IsTrue(stats.RequestCount > 1000, "Expected more than 1000 requests");
            Assert.IsTrue(stats.Ok.RequestCount >= stats.RequestCount * 0.99m, 
                "Expected 99% success rate");
            Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(1), 
                "Mean response time should be under 1s");
            Assert.IsTrue(stats.Ok.ResponseTimeStandardDeviation < TimeSpan.FromMilliseconds(500), 
                "Response time variance too high");
            
            context.Logger.LogInformation(
                "Load test completed: {Total} requests, {Success} successful, Mean: {Mean}ms",
                stats.RequestCount,
                stats.Ok.RequestCount,
                stats.Ok.ResponseTimeMean.TotalMilliseconds);
        })
        .Run();
}
```

### Example 5: Shared Steps Pattern

```csharp
public static class AuthenticationSteps
{
    public static ScenarioBuilder<T> LoginAsUser<T>(
        this ScenarioBuilder<T> builder, 
        string email, 
        string password) where T : new()
    {
        return builder.Step($"Login as {email}", async context =>
        {
            var token = await authService.Login(email, password);
            context.SetSharedData("authToken", token);
            context.Logger.LogInformation("Authenticated as {Email}", email);
        });
    }
    
    public static ScenarioBuilder<T> Logout<T>(this ScenarioBuilder<T> builder) 
        where T : new()
    {
        return builder.Step("Logout", async context =>
        {
            var token = context.GetSharedData<string>("authToken");
            await authService.Logout(token);
            context.Logger.LogInformation("User logged out");
        });
    }
}

// Usage
[ScenarioTest]
public async Task SecureOperationTest()
{
    await Scenario("Secure Operation")
        .LoginAsUser("admin@example.com", "password123")
        .Step("Perform secure operation", async context =>
        {
            var token = context.GetSharedData<string>("authToken");
            await secureService.PerformOperation(token);
        })
        .Logout()
        .Run();
}
```

### Example 6: Complex Load Test with Multiple Scenarios

```csharp
[ScenarioTest]
public async Task MixedLoadTest()
{
    var readScenario = Scenario("Read Operations")
        .Step("Get Products", async context => 
        {
            await apiClient.GetAsync("/api/products");
        })
        .Step("Get Product Details", async context =>
        {
            await apiClient.GetAsync("/api/products/123");
        });
    
    var writeScenario = Scenario("Write Operations")
        .Step("Create Order", async context =>
        {
            await apiClient.PostAsync("/api/orders", content);
        });
    
    await Scenario("Mixed Workload")
        .Step("Execute Mixed Operations", async context =>
        {
            var random = new Random();
            if (random.Next(100) < 80) // 80% reads
            {
                await apiClient.GetAsync("/api/products");
            }
            else // 20% writes
            {
                await apiClient.PostAsync("/api/orders", content);
            }
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.FixedConcurrentLoad(50, TimeSpan.FromMinutes(5));
        })
        .Load().IncludeScenario(readScenario)
        .Load().IncludeScenario(writeScenario)
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

### Example 7: HTTP API Testing

```csharp
using Fuzn.TestFuzn.Plugins.Http;

[FeatureTest]
public class ProductApiTests : BaseFeatureTest
{
    public override string FeatureName => "Product API";
    public override string FeatureId => "PROD-API-001";

    [ScenarioTest]
    public async Task GetProductsList()
    {
        await Scenario("Get Products List")
            .Step("Call products endpoint", async context =>
            {
                var response = await context.CreateHttpRequest("https://api.example.com/products")
                    .Get();
                
                Assert.IsTrue(response.Ok);
                var products = response.BodyAs<List<Product>>();
                Assert.IsTrue(products.Count > 0);
            })
            .Run();
    }

    [ScenarioTest]
    public async Task CreateAndVerifyProduct()
    {
        await Scenario("Create and Verify Product")
            .Step("Create new product", async context =>
            {
                var newProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = $"Product_{Guid.NewGuid()}",
                    Price = 99.99m
                };

                var response = await context.CreateHttpRequest("https://api.example.com/products")
                    .Body(newProduct)
                    .Post();

                Assert.IsTrue(response.Ok);
                context.SetSharedData("productId", newProduct.Id);
            })
            .Step("Verify product exists", async context =>
            {
                var productId = context.GetSharedData<Guid>("productId");
                var response = await context.CreateHttpRequest($"https://api.example.com/products/{productId}")
                    .Get();

                Assert.IsTrue(response.Ok);
                var product = response.BodyAs<Product>();
                Assert.AreEqual(productId, product.Id);
            })
            .Run();
    }
}
```

### Example 8: Playwright UI Testing

```csharp
using Fuzn.TestFuzn.Plugins.Playwright;

[FeatureTest]
public class WebUITests : BaseFeatureTest
{
    public override string FeatureName => "Web UI Tests";
    public override string FeatureId => "UI-001";

    [ScenarioTest]
    public async Task VerifyPageTitle()
    {
        await Scenario("Verify Page Title")
            .Step("Open page and check title", async context =>
            {
                var page = await context.CreateBrowserPage();
                await page.GotoAsync("https://example.com");
                
                var title = await page.TitleAsync();
                Assert.AreEqual("Example Domain", title);
            })
            .Run();
    }
}
```

---

## Best Practices

1. **Use descriptive names** for scenarios and steps
2. **Leverage custom context** for type-safe data sharing in complex scenarios
3. **Use shared steps** to reduce code duplication
4. **Add meaningful logs and comments** for debugging
5. **Attach evidence** (screenshots, responses) for failed scenarios
6. **Use warmup** before load tests to stabilize the system
7. **Set realistic assertions** based on actual performance requirements
8. **Organize tests** by feature areas using test categories
9. **Clean up resources** in cleanup hooks
10. **Use async/await** consistently for better performance
11. **Verify HTTP responses** using the `Ok` property before deserializing
12. **Capture screenshots** on UI test failures for easier debugging
13. **Use meaningful selectors** for web elements (prefer data-testid attributes)