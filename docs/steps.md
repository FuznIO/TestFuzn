# Steps

Steps are the building blocks of scenarios. They execute in order, and if one fails, subsequent steps are skipped.

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

```csharp
.Step("Important Step", "STEP-001", async context =>
{
    // Step with explicit ID for tracking
})
```

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

```markdown
[← Back to Table of Contents](README.md)
