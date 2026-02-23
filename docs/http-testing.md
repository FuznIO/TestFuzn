# HTTP Testing

TestFuzn provides HTTP testing capabilities through the [Fuzn.FluentHttp](https://github.com/FuznIO/FluentHttp) NuGet package, which offers a clean, chainable API for building and sending HTTP requests.

> **📚 Full FluentHttp Documentation**: For complete details on all request options, response handling, streaming, custom serialization, and more, see the [FluentHttp documentation](https://github.com/FuznIO/FluentHttp).

---

## Installation

Install the HTTP plugin via NuGet:

```bash
dotnet add package Fuzn.TestFuzn.Plugins.Http
```

---

## HTTP Client Setup

### Quick Start (Built-in Default Client)

The simplest way to get started is to call `UseHttp()` with no configuration. The plugin provides a built-in `TestFuznHttpClient` that is automatically registered and set as the default:

```csharp
public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.UseHttp();
    }
}
```

You can then use full URLs in your tests:

```csharp
var response = await context.CreateHttpRequest("https://api.example.com/api/Products")
    .Get<List<Product>>();
```

### Configuring the Default Client

If you need to set a base address, timeout, or other `HttpClient` settings, you can configure the built-in `TestFuznHttpClient` using `AddHttpClient<TestFuznHttpClient>()`:

```csharp
public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.UseHttp(httpConfig =>
        {
            httpConfig.Services.AddHttpClient<TestFuznHttpClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.example.com");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        });
    }
}
```

> **💡 Tip**: Setting a `BaseAddress` on the HttpClient allows you to use relative URLs in your tests (e.g., `/api/products`). Without a base address, you must use absolute URLs (e.g., `https://api.example.com/api/products`).

### Creating a Custom HTTP Client

If you need custom behavior (e.g., custom serialization, default headers), create a class that implements `IHttpClient`:

```csharp
using Fuzn.FluentHttp;
using Fuzn.TestFuzn.Plugins.Http;

public class MyHttpClient : IHttpClient
{
    public HttpClient HttpClient { get; }

    public MyHttpClient(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    public FluentHttpRequest CreateHttpRequest()
    {
        // Configure common settings for all requests (e.g., serializer, default headers)
        return HttpClient.Request();
    }
}
```

Register it and set it as the default:

```csharp
public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.UseHttp(httpConfig =>
        {
            httpConfig.Services.AddHttpClient<MyHttpClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.example.com");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            httpConfig.DefaultHttpClient<MyHttpClient>();
        });
    }
}
```

### Using Multiple HTTP Clients

If your test project talks to multiple APIs and you want to use base addresses, create a separate HTTP client for each API:

```csharp
public class InternalApiClient : IHttpClient
{
    public HttpClient HttpClient { get; }
    public InternalApiClient(HttpClient httpClient) => HttpClient = httpClient;
    public FluentHttpRequest CreateHttpRequest() => HttpClient.Request();
}

public class ExternalApiClient : IHttpClient
{
    public HttpClient HttpClient { get; }
    public ExternalApiClient(HttpClient httpClient) => HttpClient = httpClient;
    public FluentHttpRequest CreateHttpRequest() => HttpClient.Request();
}
```

Register them in your startup:

```csharp
configuration.UseHttp(httpConfig =>
{
    httpConfig.Services.AddHttpClient<InternalApiClient>(client =>
    {
        client.BaseAddress = new Uri("https://internal-api.example.com");
    });

    httpConfig.Services.AddHttpClient<ExternalApiClient>(client =>
    {
        client.BaseAddress = new Uri("https://external-api.example.com");
    });

    // Set one as the default (optional — if not set, the built-in TestFuznHttpClient is used)
    httpConfig.DefaultHttpClient<InternalApiClient>();
});
```

**Using in Tests**:

```csharp
// Uses the default client (InternalApiClient in this example):
var response = await context.CreateHttpRequest("/users").Get<User>();

// Explicitly specify which client to use:
var response = await context.CreateHttpRequest<ExternalApiClient>("/data").Get<Data>();
```

---

## Basic HTTP Requests

Once you've [configured an HTTP client](#http-client-setup), use it in your tests via `context.CreateHttpRequest()`:

### GET Request

```csharp
using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;

[Test]
public async Task Get_products_from_api()
{
    await Scenario()
        .Step("Get products and verify response", async context =>
        {
            var response = await context.CreateHttpRequest("/api/Products")
                .Get<List<Product>>();
            
            Assert.IsTrue(response.IsSuccessful);
            Assert.IsNotNull(response.Data);
            Assert.IsNotEmpty(response.Data);
        })
        .Run();
}
```

### POST Request

```csharp
[Test]
public async Task Create_product()
{
    await Scenario()
        .Step("Create new product", async context =>
        {
            var newProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Product",
                Price = 100
            };
            
            var response = await context.CreateHttpRequest("/api/Products")
                .WithContent(newProduct)
                .Post();
            
            Assert.IsTrue(response.IsSuccessful);
        })
        .Run();
}
```

### PUT Request

```csharp
[Test]
public async Task Update_product()
{
    await Scenario()
        .Step("Update existing product", async context =>
        {
            var updatedProduct = new Product
            {
                Id = existingProductId,
                Name = "Updated Test Product",
                Price = 150
            };
            
            var response = await context.CreateHttpRequest("/api/Products")
                .WithContent(updatedProduct)
                .Put();
            
            Assert.IsTrue(response.IsSuccessful);
        })
        .Run();
}
```

### DELETE Request

```csharp
[Test]
public async Task Delete_product()
{
    await Scenario()
        .Step("Delete product", async context =>
        {
            var response = await context.CreateHttpRequest($"/api/Products/{productId}")
                .Delete();
            
            Assert.IsTrue(response.IsSuccessful);
        })
        .Run();
}
```

---

## Authentication

### Bearer Token Authentication

```csharp
[Test]
public async Task Call_authenticated_endpoint()
{
    await Scenario()
        .Step("Authenticate and retrieve JWT token", async context =>
        {
            var response = await context.CreateHttpRequest("/api/Auth/token")
                .WithContent(new { Username = "admin", Password = "admin123" })
                .Post<TokenResponse>();

            Assert.IsTrue(response.IsSuccessful);
            context.SetSharedData("authToken", response.Data.Token);
        })
        .Step("Get products with auth token", async context =>
        {
            var authToken = context.GetSharedData<string>("authToken");

            var response = await context.CreateHttpRequest("/api/Products")
                .WithAuthBearer(authToken)
                .Get<List<Product>>();
            
            Assert.IsTrue(response.IsSuccessful);
        })
        .Run();
}
```

### Basic Authentication

```csharp
[Test]
public async Task Call_with_basic_auth()
{
    await Scenario()
        .Step("Get protected resource", async context =>
        {
            var response = await context.CreateHttpRequest("/api/Products")
                .WithAuthBasic("admin", "admin123")
                .Get<List<Product>>();
            
            Assert.IsTrue(response.IsSuccessful);
        })
        .Run();
}
```

---

## Request Options

### Headers

```csharp
var response = await context.CreateHttpRequest("/api/Products")
    .WithHeader("X-Custom-Header", "value")
    .WithHeader("Accept-Language", "en-US")
    .Get<List<Product>>();
```

### Query Parameters

```csharp
var response = await context.CreateHttpRequest("/api/Products")
    .WithQueryParam("name", "Laptop")
    .WithQueryParam("maxPrice", "1000")
    .Get<List<Product>>();
```

### Timeout

```csharp
var response = await context.CreateHttpRequest("/api/Products")
    .WithTimeout(TimeSpan.FromSeconds(60))
    .Get<List<Product>>();
```

---

## Response Handling

### Status Code Checks

```csharp
var response = await context.CreateHttpRequest("/api/Products")
    .WithAuthBearer(authToken)
    .Get<List<Product>>();

Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
Assert.IsTrue(response.IsSuccessful);
```

### Accessing Response Headers

```csharp
var response = await context.CreateHttpRequest("/api/Products")
    .WithAuthBearer(authToken)
    .Get<List<Product>>();

var contentType = response.Headers.GetValues("Content-Type").FirstOrDefault();
```

### Error Handling

```csharp
var response = await context.CreateHttpRequest("/api/Products")
    .Get<List<Product>>();

if (!response.IsSuccessful)
{
    context.Logger.LogError($"Request failed: {response.StatusCode}");
    context.Logger.LogError($"Error content: {response.Content}");
}
```

---

## Configuration

Configure the HTTP plugin in your `Startup` class:

```csharp
public void Configure(TestFuznConfiguration configuration)
{
    configuration.UseHttp(httpConfig =>
    {
        // Register and configure your HTTP client
        httpConfig.Services.AddHttpClient<SampleAppHttpClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:44316");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = true
        });

        httpConfig.DefaultHttpClient<SampleAppHttpClient>();

        // Enable logging of HTTP details to test console when a step fails (default: true)
        // Only applies to standard tests, not load tests
        httpConfig.WriteHttpDetailsToConsoleOnStepFailure = true;
        
        // Configure correlation ID header name (default: "X-Correlation-ID")
        httpConfig.CorrelationIdHeaderName = "X-Request-ID";
    });
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `WriteHttpDetailsToConsoleOnStepFailure` | `true` | Write HTTP request/response details to console when a step fails. **Only applies to standard tests, not load tests.** |
| `CorrelationIdHeaderName` | `"X-Correlation-ID"` | Name of the header used for correlation ID injection |

> **📝 Note**: When `WriteHttpDetailsToConsoleOnStepFailure` is enabled, the most recent HTTP request/response details are automatically written to the test console if a step fails. This helps with debugging without requiring full verbosity logging.

---

## HTTP Load Testing

HTTP requests can be used in load tests to simulate concurrent API traffic:

```csharp
[Test]
public async Task Product_api_load_test()
{
    await Scenario()
        .Step("Call GET /Products", async context =>
        {
            var response = await context.CreateHttpRequest("/api/Products")
                .WithAuthBearer(authToken)
                .Get<List<Product>>();
            
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

See the [Load Testing](load-testing.md) documentation for more details on load test configuration and assertions.

---

[← Back to Table of Contents](README.md)
