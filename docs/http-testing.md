# HTTP Testing

TestFuzn provides HTTP testing capabilities through the [Fuzn.FluentHttp](https://github.com/FuznIO/FluentHttp) NuGet package, which offers a clean, chainable API for building and sending HTTP requests.

> **📚 Full FluentHttp Documentation**: For complete details on all request options, response handling, streaming, custom serialization, and more, see the [FluentHttp documentation](https://github.com/FuznIO/FluentHttp).

---

## Quick Start

```csharp
using Fuzn.TestFuzn.Plugins.Http;

[Test]
public async Task Get_products_from_api()
{
    await Scenario()
        .Step("Call API and verify response", async context =>
        {
            var response = await context.CreateHttpRequest("https://api.example.com/products")
                .Get<List<Product>>();
            
            Assert.IsTrue(response.IsSuccessful);
            Assert.IsNotNull(response.Data);
            Assert.IsNotEmpty(response.Data);
        })
        .Run();
}
```

---

## Basic Usage

TestFuzn extends `Context` with `CreateHttpRequest()` which returns a `FluentHttpRequest` builder:

```csharp
// GET with typed response
var response = await context.CreateHttpRequest(url).Get<Product>();
var product = response.Data;

// POST with content
var response = await context.CreateHttpRequest(url)
    .WithContent(new { Name = "Product", Price = 99.99 })
    .Post();

// Authentication
var response = await context.CreateHttpRequest(url)
    .WithAuthBearer("your-jwt-token")
    .Get();

// Check response
Assert.IsTrue(response.IsSuccessful);
Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
```

For all available request methods (`.WithHeader()`, `.WithQueryParam()`, `.WithTimeout()`, etc.) and response handling options, see the [FluentHttp documentation](https://github.com/FuznIO/FluentHttp).

---

## Custom HTTP Client

You can create a custom HTTP client by implementing the `IHttpClient` interface:

```csharp
using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;

public class CustomHttpClient : IHttpClient
{
    public HttpClient HttpClient { get; }

    public CustomHttpClient(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    public FluentHttpRequest CreateHttpRequest()
    {
        // Add custom logic here (logging, metrics, etc.)
        return HttpClient.Request();
    }
}
```

### Using a Custom HTTP Client Per Request

```csharp
var response = await context.CreateHttpRequest<CustomHttpClient>("https://api.example.com/products")
    .Get<Product>();
```

### Setting a Custom HTTP Client as Default

Configure a custom HTTP client as the default in your `Startup`:

```csharp
public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.UseHttp(httpConfig =>
        {
            // Register the custom HTTP client with HttpClientFactory
            httpConfig.Services.AddHttpClient<CustomHttpClient>(client =>
            {
                // Configure the HttpClient as needed
            });

            // Set as the default HTTP client for all requests
            httpConfig.UseDefaultHttpClient<CustomHttpClient>();
        });
    }
}
```

> **⚠️ Important**: Custom HTTP clients must be registered using `httpConfig.Services.AddHttpClient<T>()` **within** the `UseHttp()` configuration block. Registering the client outside of `UseHttp()` will prevent TestFuzn's HTTP logging and request tracking from working correctly.

---

## HTTP Plugin Configuration

Configure the HTTP plugin in your `Startup` class:

```csharp
public void Configure(TestFuznConfiguration configuration)
{
    configuration.UseHttp(httpConfig =>
    {
        // Set a default base address for all requests
        httpConfig.DefaultBaseAddress = new Uri("https://api.example.com");
        
        // Set default request timeout (default: 10 seconds)
        httpConfig.DefaultRequestTimeout = TimeSpan.FromSeconds(30);
        
        // Configure auto-redirect behavior (default: false)
        httpConfig.DefaultAllowAutoRedirect = true;
        
        // Enable logging of failed requests to test console
        httpConfig.LogFailedRequestsToTestConsole = true;
        
        // Configure correlation ID header name (default: "X-Correlation-ID")
        httpConfig.CorrelationIdHeaderName = "X-Request-ID";
        
        // Set logging verbosity
        httpConfig.LoggingVerbosity = LoggingVerbosity.Verbose;
    });
}
```

---

## HTTP Load Testing

```csharp
[Test]
public async Task API_load_test()
{
    await Scenario()
        .Step("Call API endpoint", async context =>
        {
            var response = await context.CreateHttpRequest("https://api.example.com/health")
                .Get();
            
            Assert.IsTrue(response.IsSuccessful);
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.FixedLoad(50, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        })
        .Load().AssertWhenDone((context, stats) =>
        {
            Assert.IsTrue(stats.Ok.ResponseTimeMean < TimeSpan.FromSeconds(1));
            Assert.AreEqual(0, stats.Failed.RequestCount);
        })
        .Run();
}
```

---

[← Back to Table of Contents](README.md)
