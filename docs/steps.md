# Steps

Steps are the building blocks of scenarios. They execute in order, and if one fails, subsequent steps are skipped.

## Steps Without a Name

For simple tests where a step name isn't needed, the name can be omitted. Steps are auto-named "Step 1", "Step 2", etc.:

```csharp
.Step(context =>
{
    context.Logger.LogInformation("Auto-named step");
})
```

## Synchronous Steps

```csharp
.Step("Sync Step", context =>
{
    context.Logger.LogInformation("Executing step");
})
```

## Asynchronous Steps

```csharp
.Step("Async Step", async context =>
{
    await SomeAsyncOperation();
})
```

## Steps with IDs

Every step has an id. Reports and Cockpit use it to follow a step from run to run.

- **Not set:** TestFuzn makes one from the parent's id and the step name: the first 12 characters of a SHA-256 hash, such as `3f9a2c7e81b4`. It stays the same from run to run, and changes when the step or its parent is renamed.
- **Set by hand:** the id is kept as written, so the step keeps its history when you rename it.

```csharp
.Step("Important Step", "STEP-001", async context =>
{
})
```

A sub-step takes an id the same way: `context.Step("Child Step", "STEP-002", ...)`.

Step ids must be unique within a scenario. Two steps with the same id stop the test with an error that names both. The HTML report shows only ids set by hand; the XML report always has them.

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

## Comments

Add contextual information to step:

```csharp
.Step("Call POST /Products to create a new product", context =>
{
    context.Comment("Creating product with generated ID");
    // Create product
    context.Comment("Product created successfully");
})
```

- **Standard tests**: Comments output to console and reports
- **Load tests**: Comments output to log files

---

## Attachments

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

---

## Cleanup

Register cleanup actions from within a step. Cleanup actions run at the end of the iteration in reverse registration order (LIFO), before `AfterIteration`:

```csharp
.Step("Create test user", async context =>
{
    var userId = await CreateUser();
    context.SetSharedData("UserId", userId);

    // Runs at end of iteration, even if a later step fails
    context.Cleanup(async () =>
    {
        await DeleteUser(userId);
    });
})
.Step("Verify user", context =>
{
    var userId = context.GetSharedData<string>("UserId");
    // Use userId...
})
```

Synchronous overload:

```csharp
context.Cleanup(() =>
{
    File.Delete(tempFilePath);
});
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

[← Back to Table of Contents](README.md)
