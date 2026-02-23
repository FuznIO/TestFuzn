# Getting Started

## Overview

**TestFuzn** (pronounced "testfusion") is a unified testing framework that brings together **unit tests**, **end-to-end tests**, and **load tests** in a single, streamlined experience. It's designed to bridge the gap between developers and testers by offering clean, readable tests, reports, and a consistent testing approach.

Built on top of **MSTest v4**, TestFuzn provides a fluent, scenario-based approach to writing tests.

### ✨ Key Features

- 🧪 **One framework for all test types** — Write and run unit, end-to-end, and load tests using the same framework — no need to mix and match tools.
- 📊 **Readable and useful reports** — Get clear HTML and XML test results that are easy to understand for both developers and testers.
- 🧼 **Slim and clean by design** — Built to be lightweight and focused — includes just the right features to stay easy to use and maintain.
- 💬 **Test any system over HTTP** — End-to-end and load tests support HTTP-based systems, regardless of the underlying technology stack.
- 🌐 **Web UI testing with Microsoft Playwright** — Automate and validate browser-based applications using Playwright — works with any web app, no matter what language or framework it's built in.
- 💻 **C# / .NET first** — Write all your tests in C#. Leverages the power of .NET to keep things fast and flexible.
- ✅ **MSTest compatible** — Built-in support for the widely used MSTest framework — reuse what you already know and love.
- 📈 **Real-time statistics** — Stream load test metrics to InfluxDB/Grafana for live monitoring.

### 💸 License & Usage

TestFuzn is **100% free** — for personal, organizational, and commercial use.

---

## Requirements

- **.NET 10** or later
- **MSTest v4** (included via `MSTest.Sdk/4.0.0`)

---

## Installation

Install TestFuzn via NuGet:

```bash
dotnet add package Fuzn.TestFuzn
dotnet add package Fuzn.TestFuzn.Adapters.MSTest
```

For HTTP testing support:

```bash
dotnet add package Fuzn.TestFuzn.Plugins.Http
```

For Playwright/browser testing support:

```bash
dotnet add package Fuzn.TestFuzn.Plugins.Playwright
```

For InfluxDB real-time statistics:

```bash
dotnet add package Fuzn.TestFuzn.Sinks.InfluxDB
```

### Project Setup

Your test project should use `MSTest.Sdk`:

```xml
<Project Sdk="MSTest.Sdk/4.0.0">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TestFuzn\TestFuzn.csproj" />
    <ProjectReference Include="..\TestFuzn.Adapters.MSTest\TestFuzn.Adapters.MSTest.csproj" />
    <ProjectReference Include="..\TestFuzn.Plugins.Http\TestFuzn.Plugins.Http.csproj" />
  </ItemGroup>

</Project>
```

---

## Quick Start

### 1. Create a Startup Class

Every test project needs a `Startup` class to initialize TestFuzn:

```csharp
using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;

namespace SampleApp.Tests;

[TestClass]
public class Startup : IStartup
{
    [AssemblyInitialize]
    public static async Task Initialize(TestContext testContext)
    {
        await TestFuznIntegration.Init(testContext);
    }

    [AssemblyCleanup]
    public static async Task Cleanup(TestContext testContext)
    {
        await TestFuznIntegration.Cleanup(testContext);
    }

    public void Configure(TestFuznConfiguration configuration)
    {
        // For HTTP testing, optional.
        configuration.UseHttp();
    }
}
```

### 2. Write Your First Test

```csharp
using Fuzn.TestFuzn;

namespace SampleApp.Tests;

[TestClass]
public class ProductHttpTests : Test
{
    [Test]
    public async Task My_first_test()
    {
        await Scenario()
            .Step("Step 1 - Setup", context =>
            {
                context.Logger.LogInformation("Hello from TestFuzn!");
            })
            .Step("Step 2 - Verify", context =>
            {
                Assert.IsTrue(true);
            })
            .Run();
    }
}
```

---

[← Back to Table of Contents](README.md)
