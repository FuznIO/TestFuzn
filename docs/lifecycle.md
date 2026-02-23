# Lifecycle Hooks

## Scenario Level

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

---

## Iteration Level

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

---

## Test Class Level

Implement interfaces for test-level hooks:

```csharp
[TestClass]
public class ProductHttpTests : Test, IBeforeTest, IAfterTest
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

---

## Suite Level

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

[? Back to Table of Contents](README.md)
