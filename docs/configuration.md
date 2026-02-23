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

Use `ConfigurationManager.GetRequiredValue<T>()` to retrieve values from `TestFuzn:Values`:

```csharp
// Get a string value
var baseUrl = ConfigurationManager.GetRequiredValue<string>("BaseUrl");

// Get a numeric value
var timeout = ConfigurationManager.GetRequiredValue<int>("Timeout");

// Check if a value exists before accessing
if (ConfigurationManager.HasValue("MaxRetries"))
{
    var retries = ConfigurationManager.GetRequiredValue<int>("MaxRetries");
}
```

### Configuration Sections

Use `ConfigurationManager.GetRequiredSection<T>()` to bind entire sections to strongly-typed objects:

```csharp
public class AuthConfig
{
    public string Username { get; set; }
    public string Password { get; set; }
}

// Retrieve and bind the section
var authConfig = ConfigurationManager.GetRequiredSection<AuthConfig>("Auth");

// Check if a section exists
if (ConfigurationManager.HasSection("Auth"))
{
    var config = ConfigurationManager.GetRequiredSection<AuthConfig>("Auth");
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
                var baseUrl = ConfigurationManager.GetRequiredValue<string>("BaseUrl");
                var timeout = ConfigurationManager.GetRequiredValue<int>("Timeout");

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
| `ConfigurationManager.HasValue(key)` | Returns `true` if `TestFuzn:Values:{key}` exists |
| `ConfigurationManager.GetRequiredValue<T>(key)` | Gets value from `TestFuzn:Values:{key}`, throws if not found |
| `ConfigurationManager.HasSection(sectionName)` | Returns `true` if `TestFuzn:{sectionName}` exists |
| `ConfigurationManager.GetRequiredSection<T>(sectionName)` | Binds `TestFuzn:{sectionName}` to type `T`, throws if not found |

---

[← Back to Table of Contents](README.md)
