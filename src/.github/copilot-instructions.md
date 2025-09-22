# Copilot Instructions for TestFuzn
This file provides instructions for GitHub Copilot when assisting with code in this repository. It ensures that generated code and suggestions follow the design principles, naming conventions, and architectural style of TestFuzn.

# Project Purpose

TestFuzn is a C# testing framework that unifies:
- Unit testing
- Integration testing
- End-to-end testing (with Playwright, HTTP mocks, etc.)
- Load testing (With concurrent scenarios)

The framework enables developers to:
- Write scenarios consisting of steps (with support for nested sub-steps).
- Run scenarios as feature tests (once with multiple input sets) or load tests (many iterations with the same input).
- Collect results in structured formats (XML, HTML, InfluxDB + Grafana).

## Test Types
- FeatureTest → validates correctness of a feature with input data.
- LoadTest → stresses the system with concurrent iterations.
- Both are supported via [FeatureTest] and [ScenarioTest] attributes.

# Implementation Guidance for Copilot

When generating code:
- The framework isn't released yet so don't have to care about being backwards compatible.
- Ensure thread safety in load tests – no shared mutable state between iterations.
- Default to framework-agnostic test attributes (our framework provides [FeatureTest], [ScenarioTest].

# Reporting & Output
- XML format uses <Feature>, <Scenario>, <Iteration>, <Steps>.

# Things to Avoid
- Don’t generate code using Console.WriteLine unless it’s inside an example.
- Don’t introduce third-party testing frameworks (NUnit, xUnit, MSTest) unless explicitly bridging.
- Don’t name steps or scenarios with vague titles like “Test1” – use descriptive names.
