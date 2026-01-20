# Configuration Management

TestFuzn provides a flexible configuration system that allows you to manage test settings through `appsettings.json` files with support for environment-specific overrides.

---

## Basic Configuration

Configuration is loaded from `appsettings.json` in your test project directory. All TestFuzn-specific settings should be placed under the `TestFuzn` section:

```json
{
  "TestFuzn": {
    "Values": {
      "BaseUrl": "https://api.example.com",
      "Timeout": 30,
      "MaxRetries": 3
    },
    "Database": {
      "ConnectionString": "Server=localhost;Database=TestDb;",
      "CommandTimeout": 60
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
public class DatabaseConfig
{
    public string ConnectionString { get; set; }
    public int CommandTimeout { get; set; }
}

// Retrieve and bind the section
var dbConfig = ConfigurationManager.GetRequiredSection<DatabaseConfig>("Database");

// Check if a section exists
if (ConfigurationManager.HasSection("Database"))
{
    var config = ConfigurationManager.GetRequiredSection<DatabaseConfig>("Database");
}
```

---

## Environment-Specific Overrides

TestFuzn supports multiple configuration files that are loaded in a specific order, allowing environment-specific overrides. Later files override values from earlier ones.

### Configuration File Loading Order

1. `appsettings.json` (required)
2. `appsettings.exec-{executionEnv}.json` (optional)
3. `appsettings.target-{targetEnv}.json` (optional)
4. `appsettings.exec-{executionEnv}.target-{targetEnv}.json` (optional)
5. `appsettings.{nodeName}.json` (optional)

### Execution Environment

The **execution environment** represents where the tests are running (e.g., local machine, CI server, cloud agent).

Set via:
- Environment variable: `TESTFUZN_EXECUTION_ENVIRONMENT`
- Command line: `--execution-environment <value>`

Example files:
- `appsettings.exec-local.json` - Settings for local development
- `appsettings.exec-ci.json` - Settings for CI/CD pipeline
- `appsettings.exec-cloud.json` - Settings for cloud-based test agents

```json
// appsettings.exec-ci.json
{
  "TestFuzn": {
    "Values": {
      "Timeout": 60,
      "Headless": true
    }
  }
}
```

### Target Environment

The **target environment** represents the system under test (e.g., dev, staging, production).

Set via:
- Environment variable: `TESTFUZN_TARGET_ENVIRONMENT`
- Command line: `--target-environment <value>`

Example files:
- `appsettings.target-dev.json` - Settings for testing against dev
- `appsettings.target-staging.json` - Settings for testing against staging
- `appsettings.target-prod.json` - Settings for testing against production

```json
// appsettings.target-staging.json
{
  "TestFuzn": {
    "Values": {
      "BaseUrl": "https://staging-api.example.com",
      "ApiKey": "staging-key-12345"
    }
  }
}
```

### Combined Environment Overrides

You can create files that apply only when both execution and target environments match:

```json
// appsettings.exec-ci.target-staging.json
{
  "TestFuzn": {
    "Values": {
      "BaseUrl": "https://internal-staging-api.example.com",
      "UseInternalNetwork": true
    }
  }
}
```

### Node-Specific Overrides

You can have node-specific configuration. The **node name** comes from **Environment.MachineName**

Example: `appsettings.node-worker-01.json`

---

## Configuration Precedence Example

Given:
- Execution environment: `ci`
- Target environment: `staging`
- Node name: `worker-01`

Files are loaded in this order (later values override earlier):

1. `appsettings.json`
2. `appsettings.exec-ci.json`
3. `appsettings.target-staging.json`
4. `appsettings.exec-ci.target-staging.json`
5. `appsettings.worker-01.json`

---

## Using Configuration in Tests

```csharp
[TestClass]
[Group("API Tests")]
public class ApiTests : Test
{
    [Test]
    public async Task Call_api_with_configured_settings()
    {
        await Scenario()
            .Step("Call API", async context =>
            {
                var baseUrl = ConfigurationManager.GetRequiredValue<string>("BaseUrl");
                var timeout = ConfigurationManager.GetRequiredValue<int>("Timeout");

                var response = await context.CreateHttpRequest($"{baseUrl}/health")
                    .Timeout(TimeSpan.FromSeconds(timeout))
                    .Get();

                Assert.IsTrue(response.Ok);
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
