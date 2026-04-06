# TestFuzn
**TestFuzn** (pronounced "testfusion") is a unified testing framework that brings together **unit tests**, **end-to-end tests**, and **load tests** in a single, streamlined experience. It’s designed to bridge the gap between developers and testers by offering clean, readable tests, reports and a consistent testing approach.

🚧 **Beta**  
> TestFuzn is currently in **beta**. Breaking changes may occur until a stable release.

📚 **Documentation:**  
👉 https://github.com/FuznIO/TestFuzn/blob/main/docs/README.md

## ✨ Key Features
- 🧪 **One framework for all test types**  
  Write and run unit, end-to-end, and load tests using the same framework — no need to mix and match tools.
- 📊 **Readable and useful reports**  
  Get clear test results that are easy to understand for both developers and testers.
- 🧼 **Slim and clean by design**  
  Built to be lightweight and focused — includes just the right features to stay easy to use and maintain.
- 💬 **Test any system over HTTP**  
  End-to-end and load tests support HTTP-based systems, regardless of the underlying technology stack.
- 🌐 **Web UI testing with Microsoft Playwright**  
  Automate and validate browser-based applications using Playwright — works with any web app, no matter what language or framework it’s built in.
- 💻 **C# / .NET first**  
  Write all your tests in C#. Leverages the power of .NET to keep things fast and flexible.
- ✅ **MSTest compatible**  
  Built-in support for the widely used MSTest framework — reuse what you already know and love.

## 📦 NuGet Packages

| Package | Description |
|---------|-------------|
| [Fuzn.TestFuzn](https://www.nuget.org/packages/Fuzn.TestFuzn) | Core framework |
| [Fuzn.TestFuzn.Adapters.MSTest](https://www.nuget.org/packages/Fuzn.TestFuzn.Adapters.MSTest) | MSTest integration |
| [Fuzn.TestFuzn.Plugins.Http](https://www.nuget.org/packages/Fuzn.TestFuzn.Plugins.Http) | HTTP testing via Fuzn.FluentHttp |
| [Fuzn.TestFuzn.Plugins.Playwright](https://www.nuget.org/packages/Fuzn.TestFuzn.Plugins.Playwright) | Browser automation with Playwright |
| [Fuzn.TestFuzn.Plugins.WebSocket](https://www.nuget.org/packages/Fuzn.TestFuzn.Plugins.WebSocket) | WebSocket testing |
| [Fuzn.TestFuzn.Sinks.InfluxDB](https://www.nuget.org/packages/Fuzn.TestFuzn.Sinks.InfluxDB) | Real-time metrics to InfluxDB |

## 💸 License & Usage
- 100% **free** — for personal, organizational, and commercial use.
