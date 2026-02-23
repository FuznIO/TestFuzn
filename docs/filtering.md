# Filtering Tests

## `[Tags]` Attribute

Specifies tags for categorization and filtering. Can be used to run specific subsets of tests.

```csharp
[Test]
[Tags("API", "Products", "Critical")]
public async Task Verify_product_crud_operations() { ... }
```

---

## `[Skip]` Attribute

Marks a test to be skipped during execution.

```csharp
[Test]
[Skip("Waiting for API deployment")]
public async Task Skipped_test() { ... }

// Or without a reason:
[Test]
[Skip]
public async Task Another_skipped_test() { ... }
```

---

## `[TargetEnvironments]` Attribute

Specifies which environments the test should run against. Tests are skipped when the current environment does not match the specified list.

The current environment is determined by the `TESTFUZN_TARGET_ENVIRONMENT` environment variable or `--target-environment` argument. See [Environments](environments.md) for details on how environments are configured.

```csharp
[Test]
[TargetEnvironments("Dev", "Test", "Staging")]
public async Task Test_that_should_not_run_in_production() { ... }
```

---

[? Back to Table of Contents](README.md)
