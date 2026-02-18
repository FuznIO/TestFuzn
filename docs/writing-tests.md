# Writing Tests

## Basic Scenario

Create a scenario using the fluent `Scenario` builder.
By default, a scenario is executed **once** as a **standard test**:

```csharp
using Fuzn.TestFuzn;

namespace MyTests;

[TestClass]
public class MyFirstTests : Test
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
public class MyFirstTests : Test
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
    .Id("SCEN-1234")  // Optional identifier
    .Description("Scenario description")  // Optional description
    .Step("Step", context => { })
    .Run();
```

---

## Steps

Steps are the building blocks of scenarios. They execute in order, and if one fails, subsequent steps are skipped.

### Synchronous Steps

```csharp
.Step("Sync Step", context =>
{
    context.Logger.LogInformation("Executing step");
})
```

### Asynchronous Steps

```csharp
.Step("Async Step", async context =>
{
    await SomeAsyncOperation();
})
```

### Steps with IDs

```csharp
.Step("Important Step", "STEP-001", async context =>
{
    // Step with explicit ID for tracking
})
```

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

### Comments

Add contextual information to step:

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

### Attachments

Attach files to steps for attachments and debugging:

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

## Input Data

Input data provides per-iteration values for a scenario. 
Standard tests use input data to determine how many times the scenario runs 
(once if no input data is defined, otherwise once per input item), 
while load tests run the number of iterations defined by their simulations. 
For load tests, input data is reused and fed multiple times when the number of iterations 
exceeds the number of input data items.

Both simple types (e.g. `string`, `int`) and complex types (custom classes, records, DTOs) are supported.

### Static Input Data

```csharp
.InputData("user1", "user2", "user3")
```

### Static Input Data (Complex Type)

```csharp
.InputData(
    new User { Id = 1, Name = "user1" },
    new User { Id = 2, Name = "user2" }
)
```

### Input Data from Function (Sync)

```csharp
.InputDataFromList((context) =>
{
    return new List<object> { "user1", "user2", "user3" };
})
```

### Input Data from Function (Async)

```csharp
.InputDataFromList(async (context) =>
{
    var users = await database.GetTestUsers();
    return users.Cast<object>().ToList();
})
```

### Input Data Behaviors

Control how input data is consumed:

```csharp
.InputDataBehavior(InputDataBehavior.Loop)              // Sequential (default)
.InputDataBehavior(InputDataBehavior.Random)            // Random selection
.InputDataBehavior(InputDataBehavior.LoopThenRandom)    // Loop first, then random (load tests)
.InputDataBehavior(InputDataBehavior.LoopThenRepeatLast) // Loop, then repeat last item (load tests)
```

### Accessing Input Data

```csharp
.Step("Use Input Data", context =>
{
    var user = context.InputData<string>();
    context.Logger.LogInformation($"Processing user: {user}");
})
```

### Customizing Input Data Display

For **standard tests**, input data is displayed in console output and HTML reports. Override `ToString()` to customize how it appears:

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    public override string ToString()
    {
        return $"User: {Name}";
    }
}
```

Without overriding `ToString()`, the output shows the fully qualified type name (e.g., `MyNamespace.User`).

> **Note**: Load tests do not display individual input data values in reports.

---
## Share Data Between Steps

In **standard tests**, you can use method-scoped variables to share data between steps since iterations
run sequentially:

```csharp
[Test]
public async Task Standard_test_with_variables()
{
     string token = null; // Method variable works for standard tests

    await Scenario()
        .Step("Login", context => { token = "abc123"; })
        .Step("Use Token", context => { Console.WriteLine(token); })
        .Run();
}
```

In **load tests**, iterations run in parallel across multiple threads. Method-scoped variables are **not thread-safe** and will cause race conditions. Instead, use one of these approaches:

### SetSharedData / GetSharedData
Share untyped data between steps within the same iteration. Data is isolated per iteration and is safe to use in parallel load tests and reusable steps.

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

### Custom Context Models

Share typed data between steps using custom context models. The context is iteration-scoped and safe to use in parallel load tests and reusable steps.

```csharp
public class LoginModel
{
    public string Username { get; set; }
    public string Token { get; set; }
    public DateTime LoginTime { get; set; }
}

[Test]
public async Task Custom_context_example()
{
    await Scenario<LoginModel>()
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

---

## Lifecycle Hooks

### Scenario Level

```csharp
.BeforeScenario((context) =>
{
    // Runs once per scenario, before any iterations
})
.AfterScenario((context) =>
{
    // Runs once per scenario, after all iterations
})
```

### Iteration Level

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

### Test Class Level

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

### Suite Level

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

## Shared Steps

Create reusable steps across tests using one of three approaches:

### 1. Helper Methods

Extract step logic into a helper method and call it inline:

```csharp
private async Task<string> PerformLogin(Context context)
{
    var response = await context.CreateHttpRequest("https://localhost:44316/api/Auth/token")
        .WithContent(new { Username = "admin", Password = "admin123" })
        .Post<TokenResponse>();
    
    Assert.IsTrue(response.IsSuccessful);
    return response.Data.Token;
}

// Usage
await Scenario()
    .Step("Authenticate", async context =>
    {
        var token = await PerformLogin(context);
        context.SetSharedData("authToken", token);
    })
    .Step("Call Protected API", async context =>
    {
        var token = context.GetSharedData<string>("authToken");
        // Use token...
    })
    .Run();
```


### 2. Action References

Define step logic as a separate function and pass it by reference:

```csharp
private async Task LoginStep(Context context)
{
    var response = await context.CreateHttpRequest("https://localhost:44316/api/Auth/token")
        .WithContent(new { Username = "admin", Password = "admin123" })
        .Post<TokenResponse>();
    
    Assert.IsTrue(response.IsSuccessful);
    var token = response.Data.Token;
    context.SetSharedData("authToken", token);
}

// Usage
await Scenario()
    .Step("Authenticate", LoginStep)
    .Step("Call Protected API", async context =>
    {
        var token = context.GetSharedData<string>("authToken");
        // Use token...
    })
    .Run();
```

### 3. Extension Methods on ScenarioBuilder

Extension methods provide a fluent, reusable API for common step sequences:

```csharp
public static class SharedSteps
{
    public static ScenarioBuilder<T> LoginStep<T>(this ScenarioBuilder<T> builder, string baseUrl)
        where T : new()
    {
        return builder.Step("Authenticate", async context =>
        {
            var response = await context.CreateHttpRequest($"{baseUrl}/api/Auth/token")
                .WithContent(new { Username = "admin", Password = "admin123" })
                .Post<TokenResponse>();
            
            Assert.IsTrue(response.IsSuccessful);
            var token = response.Data.Token;
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



---

[← Back to Table of Contents](README.md)
