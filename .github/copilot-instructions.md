# Copilot Instructions for TestFuzn
This file provides instructions for GitHub Copilot when assisting with code in this repository. It ensures that generated code and suggestions follow the design principles, naming conventions, and architectural style of TestFuzn.

# Project Purpose

TestFuzn is a C# testing framework that unifies:
- Unit testing
- Integration testing
- End-to-end testing (with Playwright, HTTP mocks, etc.)
- Load testing

The framework enables developers to:
- Write scenarios consisting of steps (with support for nested sub-steps).
- Run scenarios as standard tests or load tests.
- Collect results in structured formats (XML, HTML, InfluxDB + Grafana).

## Test Types
- Standard Test → validates correctness of a feature with input data.
- Load Test → stresses the system with concurrent iterations.
- Both are supported via [Test] attribute.

# PR Review Instructions

When reviewing pull requests:
- **DO NOT review test projects** - Skip any files in projects ending with `.Tests` or containing `Tests` in their name (e.g., TestFuzn.Tests, SampleApp.Tests, TestFuzn.Tests.Attributes, TestFuzn.Tests.CustomHttpClient, TestFuzn.Tests.Runner).
- **DO NOT review TestWebApp** - This is a test application used for framework testing.
- Focus reviews only on framework implementation code in core projects like TestFuzn, TestFuzn.Plugins.Http, TestFuzn.Plugins.Playwright, TestFuzn.Plugins.WebSocket, TestFuzn.Adapters.MSTest, and TestFuzn.Sinks.InfluxDB.
- Test files are maintained by the development team and don't require automated review.

# Implementation Guidance for Copilot

When generating code:
- The framework isn't released yet so don't have to care about being backwards compatible.
- Ensure thread safety in load tests – no shared mutable state between iterations.
- Default to framework-agnostic test attributes (our framework provides [Test]...).

# Things to Avoid
- Don’t generate code using Console.WriteLine unless it’s inside an example.
- Don’t name steps or scenarios with vague titles like “Test1” – use descriptive names.
