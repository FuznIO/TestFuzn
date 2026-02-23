# TestFuzn Documentation

**TestFuzn** (pronounced "testfusion") is a unified testing framework that brings together **unit tests**, **end-to-end tests**, and **load tests** in a single, streamlined experience.

---

## Table of Contents

### Getting Started
- [Getting Started](getting-started.md) — Installation, project setup, quick start

### Writing Tests
- [Scenarios](scenarios.md) — Standard vs load tests, `[Test]` attribute, execution flow
- [Steps](steps.md) — Steps, sub-steps, comments, attachments, logging
- [Input Data](input-data.md) — Static data, from functions, behaviors, display customization
- [Shared Data](shared-data.md) — Sharing data between steps, custom context models
- [Lifecycle Hooks](lifecycle.md) — Scenario, iteration, test, and suite level hooks
- [Shared Steps](shared-steps.md) — Helper methods, action references, extension methods

### Test Selection
- [Filtering Tests](filtering.md) — `[Tags]`, `[Skip]`, `[TargetEnvironments]`
- [Environments](environments.md) — Target/execution environments, config file overrides

### Plugins
- [HTTP Testing](http-testing.md) — HTTP client setup, requests, authentication, response handling
- [Web UI Testing with Playwright](playwright-testing.md) — Browser automation, device emulation, UI load testing

### Load Testing
- [Load Testing](load-testing.md) — Simulations, warmup, assertions, statistics
- [InfluxDB & Grafana](influxdb-grafana.md) — Real-time metrics streaming

### Configuration
- [Configuration](configuration.md) — `appsettings.json`, `ConfigurationManager` API

### Reporting
- [Test Reports](test-reports.md) — HTML/XML reports, `[Group]`, `[Metadata]`

### Reference
- [API Reference](api-reference.md) — Quick-reference tables for all APIs

---

## 💸 License & Usage

TestFuzn is **100% free** — for personal, organizational, and commercial use.