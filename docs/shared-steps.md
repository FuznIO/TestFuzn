# Shared Steps

Create reusable steps across tests using one of three approaches:

---

## 1. Helper Methods

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

---

## 2. Action References

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

---

## 3. Extension Methods on ScenarioBuilder

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

[? Back to Table of Contents](README.md)
