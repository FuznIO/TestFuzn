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
- 🖥️ **Live load test view** — Run load tests with `--runner=testfuzn` and watch the phases, per-step stats and errors live in the terminal. See [Test Runners](test-runners.md).
- 🌍 **Cluster runs** — Run a load test on one or many worker machines with `--cluster`, watch it live and get the report back. See [Cluster Runs](cluster-runs.md).

### 💸 License & Usage

TestFuzn is **100% free** — for personal, organizational, and commercial use.

---

## Requirements

- **.NET 10** or later
- **MSTest v4** (included via `MSTest.Sdk`)

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

Cluster runs need no extra package: they are part of TestFuzn (see [Cluster Runs](cluster-runs.md)).

### Project Setup

Your test project should use `MSTest.Sdk`:

```xml
<Project Sdk="MSTest.Sdk/4.3.3">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Fuzn.TestFuzn" Version="0.9.0" />
    <PackageReference Include="Fuzn.TestFuzn.Adapters.MSTest" Version="0.9.0" />
    <PackageReference Include="Fuzn.TestFuzn.Plugins.Http" Version="0.9.0" />
  </ItemGroup>

</Project>
```

### Microsoft.Testing.Platform

`MSTest.Sdk` runs your tests on [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro), Microsoft's lightweight, portable alternative to VSTest and the runner MSTest v4 is built around. It's embedded directly in your test project — no `vstest.console`, no external runner. Your test project simply compiles to an executable you can run anywhere, including CI.

`Fuzn.TestFuzn.Adapters.MSTest` adds the entry point of that executable. It starts TestFuzn before the first test and writes the reports after the last test, so you need no `Program.cs` and no setup code. To write your own `Main`, see [Test Runners](test-runners.md#your-own-main).

### IDE setup

**JetBrains Rider** does not discover Testing Platform tests until you turn that support on, so your tests simply never appear in the Unit Tests window. Enable it once in **Settings | Build, Execution, Deployment | Unit Testing | Testing Platform** (`Ctrl+Alt+S`), using the option at the top of that page, then reload the solution.

**Visual Studio**, **VS Code** (with the C# Dev Kit extension) and `dotnet test` need no extra setup.

For live console output while a load test runs, see [Test Runners](test-runners.md).

---

## Quick Start

### 1. Create a Startup Class

Every test project needs one `Startup` class. It configures TestFuzn:

```csharp
using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;

namespace SampleApp.Tests;

public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
        // For HTTP testing, optional.
        configuration.UseHttp();

        // Register custom services for dependency injection, optional.
        // Resolved via context.ServicesProvider or GlobalState.ServiceProvider.
        configuration.Services.AddSingleton<IMyService, MyService>();
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
            });
    }
}
```

---

[← Back to Table of Contents](README.md)
