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
    // Runs after each iteration's steps and cleanup actions
})
```

**Execution order within an iteration:**

1. `BeforeIteration`
2. Steps (in order)
3. Cleanup actions registered via `context.Cleanup()` (in reverse order)
4. `AfterIteration`
5. Plugin `CleanupIteration`

---

## Class Level

Implement interfaces for class-level hooks that run once per test class:

```csharp
[TestClass]
public class ProductHttpTests : Test, IBeforeClass, IAfterClass
{
    public Task BeforeClass(Context context)
    {
        // Runs once before the first test in this class
        return Task.CompletedTask;
    }

    public Task AfterClass(Context context)
    {
        // Runs once after all tests in this class complete
        return Task.CompletedTask;
    }
}
```

---

## Test Level

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

```markdown
[← Back to Table of Contents](README.md)
