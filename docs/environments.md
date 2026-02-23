# Environments

TestFuzn supports two types of environments and node-specific overrides. Environments control both **which configuration files are loaded** and **which tests are executed**.

---

## Target Environment

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
      "BaseUrl": "https://staging.example.com",
      "ApiKey": "staging-key-12345"
    }
  }
}
```

### Filtering Tests by Target Environment

Use the `[TargetEnvironments]` attribute to restrict a test to specific environments. Tests are skipped when the current target environment does not match:

```csharp
[Test]
[TargetEnvironments("Dev", "Test", "Staging")]
public async Task Test_that_should_not_run_in_production() { ... }
```

See [Filtering Tests](filtering.md) for more on `[Tags]`, `[Skip]`, and other test selection options.

---

## Execution Environment

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

---

## Combined Environment Overrides

You can create files that apply only when both execution and target environments match:

```json
// appsettings.exec-ci.target-staging.json
{
  "TestFuzn": {
    "Values": {
      "BaseUrl": "https://internal-staging.example.com",
      "UseInternalNetwork": true
    }
  }
}
```

---

## Node-Specific Overrides

You can have node-specific configuration. The **node name** comes from **Environment.MachineName**

Example: `appsettings.node-worker-01.json`

---

## Configuration File Loading Order

Environment and node-specific configuration files are loaded in a specific order, with later files overriding values from earlier ones:

1. `appsettings.json` (required)
2. `appsettings.exec-{executionEnv}.json` (optional)
3. `appsettings.target-{targetEnv}.json` (optional)
4. `appsettings.exec-{executionEnv}.target-{targetEnv}.json` (optional)
5. `appsettings.{nodeName}.json` (optional)

### Precedence Example

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

See [Configuration](configuration.md) for details on `appsettings.json` structure and the `ConfigurationManager` API.

---

```markdown
[← Back to Table of Contents](README.md)
