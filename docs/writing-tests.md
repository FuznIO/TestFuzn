# Writing Tests

## Basic Scenario

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

---

## Input Data

Input data is used to drive multiple iterations of the same scenario.

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

---

## Custom Context

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

---

## Shared Data

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

> **?? Important for Load Tests:**
> 
> In **standard tests**, you can use method-scoped variables to share data between steps since iterations run sequentially:
> 
> ```csharp
> [Test]
> public async Task Standard_test_with_variables()
> {
>     string token = null; // Method variable works for standard tests
>     
>     await Scenario()
>         .Step("Login", context => { token = "abc123"; })
>         .Step("Use Token", context => { Console.WriteLine(token); })
>         .Run();
> }
> ```
> 
> However, in **load tests**, iterations run in parallel across multiple threads. Method-scoped variables are **not thread-safe** and will cause race conditions. Instead, use one of these approaches:
> 
> 1. **Custom Context Model** (recommended for type safety):
>    ```csharp
>    await Scenario<MyContext>()
>        .Step("Login", context => { context.Model.Token = "abc123"; })
>        .Step("Use Token", context => { Console.WriteLine(context.Model.Token); })
>        .Load().Simulations(...)
>        .Run();
>    ```
> 
> 2. **Shared Data Methods**:
>    ```csharp
>    await Scenario()
>        .Step("Login", context => { context.SetSharedData("token", "abc123"); })
>        .Step("Use Token", context => { var token = context.GetSharedData<string>("token"); })
>        .Load().Simulations(...)
>        .Run();
>    ```

---

## Lifecycle Hooks

### Scenario Level

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

## Sub-Steps

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

---

## Shared Steps

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

---

## Comments and Attachments

### Comments

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

### Attachments

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

---

## Logging

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

[? Back to Table of Contents](README.md)
