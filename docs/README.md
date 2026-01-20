# TestFuzn Documentation

**TestFuzn** (pronounced "testfusion") is a unified testing framework that brings together **unit tests**, **end-to-end tests**, and **load tests** in a single, streamlined experience.

---

## 📚 Table of Contents

### [Getting Started](getting-started.md)
- [Overview](getting-started.md#overview)
- [Requirements](getting-started.md#requirements)
- [Installation](getting-started.md#installation)
- [Quick Start](getting-started.md#quick-start)

### [Core Concepts](core-concepts.md)
- [Test Types](core-concepts.md#test-types)
- [Test Class Structure](core-concepts.md#test-class-structure)
- [Execution Flow](core-concepts.md#execution-flow)
- [Test Attributes](core-concepts.md#test-attributes)

### [Writing Tests](writing-tests.md)
- [Basic Scenario](writing-tests.md#basic-scenario)
- [Load Scenario](writing-tests.md#load-scenario)
- [Scenario Configuration](writing-tests.md#scenario-configuration)
- [Steps](writing-tests.md#steps)
- [Input Data](writing-tests.md#input-data)
- [Share Data Between Steps](writing-tests.md#share-data-between-steps)
- [Lifecycle Hooks](writing-tests.md#lifecycle-hooks)
- [Shared Steps](writing-tests.md#shared-steps)

### [Load Testing](load-testing.md)
- [Single Scenario](load-testing.md#single-scenario)
- [Multiple Scenarios](load-testing.md#multiple-scenarios)
- [Warmup](load-testing.md#warmup)
- [Simulations](load-testing.md#simulations)
- [Assertions](load-testing.md#assertions)
- [Statistics](load-testing.md#statistics)

### [Configuration](configuration.md)
- [Basic Configuration](configuration.md#basic-configuration)
- [Accessing Configuration Values](configuration.md#accessing-configuration-values)
- [Environment-Specific Overrides](configuration.md#environment-specific-overrides)
- [Configuration Precedence Example](configuration.md#configuration-precedence-example)
- [Using Configuration in Tests](configuration.md#using-configuration-in-tests)
- [API Reference](configuration.md#api-reference)

### [HTTP Testing](http-testing.md)
- [Basic HTTP Requests](http-testing.md#basic-http-requests)
- [Request Methods](http-testing.md#request-methods)
- [Authentication](http-testing.md#authentication)
- [Additional Request Options](http-testing.md#additional-request-options)
- [Response Handling](http-testing.md#response-handling)
- [JSON Serialization](http-testing.md#json-serialization)
- [HTTP Load Testing](http-testing.md#http-load-testing)

### [Web UI Testing with Playwright](playwright-testing.md)
- [Configuration](playwright-testing.md#configuration)
- [Basic Browser Testing](playwright-testing.md#basic-browser-testing)
- [UI Load Testing](playwright-testing.md#ui-load-testing)

### [Real-Time Statistics with InfluxDB](influxdb.md)
- [Configuration](influxdb.md#configuration)
- [Using appsettings.json](influxdb.md#using-appsettingsjson)
- [Available Metrics](influxdb.md#available-metrics)
- [Grafana Dashboard](influxdb.md#grafana-dashboard)

### [Test Reports](test-reports.md)
- [Standard Test Reports](test-reports.md#standard-test-reports)
- [Load Test Reports](test-reports.md#load-test-reports)
- [Report Location](test-reports.md#report-location)

### [API Reference](api-reference.md)
- [ScenarioBuilder Methods](api-reference.md#scenariobuilder-methods)
- [IterationContext Members](api-reference.md#iterationcontext-members)
- [Load Testing Methods](api-reference.md#load-testing-methods)
- [Simulation Types](api-reference.md#simulation-types)

---

## 💸 License & Usage

TestFuzn is **100% free** — for personal, organizational, and commercial use.