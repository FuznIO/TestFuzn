# Configuration Management

TestFuzn provides a flexible configuration system that allows you to manage test settings through `appsettings.json` files.

For environment-specific configuration file overrides, see [Environments](environments.md).

---

## Basic Configuration

Configuration is loaded from `appsettings.json` in your test project directory. All TestFuzn-specific settings should be placed under the `TestFuzn` section:

```json
{
  "TestFuzn": {
    "Values": {
      "BaseUrl": "https://localhost:44316",
      "Timeout": 30,
      "MaxRetries": 3
    },
    "Auth": {
      "Username": "admin",
      "Password": "admin123"
    }
  }
}
```

---

## Accessing Configuration Values

### Simple Values

Use `context.AppConfiguration.GetRequiredValue<T>()` to retrieve values from `TestFuzn:Values`:

```csharp
// Get a string value
var baseUrl = context.AppConfiguration.GetRequiredValue<string>("BaseUrl");

// Get a numeric value
var timeout = context.AppConfiguration.GetRequiredValue<int>("Timeout");

// Check if a value exists before accessing
if (context.AppConfiguration.HasValue("MaxRetries"))
{
    var retries = context.AppConfiguration.GetRequiredValue<int>("MaxRetries");
}
```

### Configuration Sections

Use `context.AppConfiguration.GetRequiredSection<T>()` to bind entire sections to strongly-typed objects:

```csharp
public class AuthConfig
{
    public string Username { get; set; }
    public string Password { get; set; }
}

// Retrieve and bind the section
var authConfig = context.AppConfiguration.GetRequiredSection<AuthConfig>("Auth");

// Check if a section exists
if (context.AppConfiguration.HasSection("Auth"))
{
    var config = context.AppConfiguration.GetRequiredSection<AuthConfig>("Auth");
}
```

---

## Using Configuration in Tests

```csharp
[TestClass]
[Group("Product API Tests")]
public class ProductHttpTests : Test
{
    [Test]
    public async Task Call_api_with_configured_settings()
    {
        await Scenario()
            .Step("Call Products API", async context =>
            {
                var baseUrl = context.AppConfiguration.GetRequiredValue<string>("BaseUrl");
                var timeout = context.AppConfiguration.GetRequiredValue<int>("Timeout");

                var response = await context.CreateHttpRequest($"{baseUrl}/api/Products")
                    .WithTimeout(TimeSpan.FromSeconds(timeout))
                    .Get();

                Assert.IsTrue(response.IsSuccessful);
            })
            .Run();
    }
}
```
---

## API Reference

| Method | Description |
|--------|-------------|
| `context.AppConfiguration.HasValue(key)` | Returns `true` if `TestFuzn:Values:{key}` exists |
| `context.AppConfiguration.GetRequiredValue<T>(key)` | Gets value from `TestFuzn:Values:{key}`, throws if not found |
| `context.AppConfiguration.HasSection(sectionName)` | Returns `true` if `TestFuzn:{sectionName}` exists |
| `context.AppConfiguration.GetRequiredSection<T>(sectionName)` | Binds `TestFuzn:{sectionName}` to type `T`, throws if not found |

---

[← Back to Table of Contents](README.md)
