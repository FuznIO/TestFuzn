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

### Creating an HTTP Client

Create a class that implements `IHttpClient`:

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

### Registering the HTTP Client

Register your HTTP client in the `Startup` class using `AddHttpClient<T>()`:

```csharp
public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.UseHttp(httpConfig =>
        {
            // Register the HTTP client with HttpClientFactory
            httpConfig.Services.AddHttpClient<MyHttpClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.example.com");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Configure the default HTTP client for context.CreateHttpRequest() calls
            httpConfig.DefaultHttpClient<MyHttpClient>();
        });
    }
}
```

> **💡 Tip**: Setting a `BaseAddress` on the HttpClient allows you to use relative URLs in your tests (e.g., `/api/products`). Without a base address, you must use absolute URLs (e.g., `https://api.example.com/api/products`).

### Minimal Setup (No Configuration)

If you don't need to customize the HTTP plugin settings, you can simply call `UseHttp()` without parameters:

```csharp
public void Configure(TestFuznConfiguration configuration)
{
    configuration.UseHttp();
}
```

However, you'll still need to register at least one HTTP client in your service collection separately.

### Using Multiple HTTP Clients

You can register multiple HTTP clients and use them explicitly. **One** can be set as the default:

```csharp
configuration.UseHttp(httpConfig =>
{
    // Register multiple clients
    httpConfig.Services.AddHttpClient<InternalApiClient>(client =>
    {
        client.BaseAddress = new Uri("https://internal-api.example.com");
    });

    httpConfig.Services.AddHttpClient<ExternalApiClient>(client =>
    {
        client.BaseAddress = new Uri("https://external-api.example.com");
    });

    // Set one as the default (optional)
    httpConfig.DefaultHttpClient<InternalApiClient>();
});
```

**Using in Tests**:

```csharp
// If you set a default client, you can use the parameterless overload:
var response = await context.CreateHttpRequest("/users").Get<User>();

// Or explicitly specify which client to use:
var response = await context.CreateHttpRequest<ExternalApiClient>("/data").Get<Data>();
```

> **⚠️ Note**: If you don't call `DefaultHttpClient<T>()`, you **must** use the generic `CreateHttpRequest<THttpClient>(url)` overload to specify which client to use.

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
            var response = await context.CreateHttpRequest("/api/products")
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
            var newProduct = new { Name = "Widget", Price = 99.99 };
            
            var response = await context.CreateHttpRequest("/api/products")
                .WithContent(newProduct)
                .Post<Product>();
            
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.IsNotNull(response.Data);
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
            var updatedProduct = new { Id = 123, Name = "Updated Widget", Price = 149.99 };
            
            var response = await context.CreateHttpRequest("/api/products/123")
                .WithContent(updatedProduct)
                .Put<Product>();
            
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
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
            var response = await context.CreateHttpRequest("/api/products/123")
                .Delete();
            
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
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
        .Step("Get user profile", async context =>
        {
            var response = await context.CreateHttpRequest("/api/user/profile")
                .WithAuthBearer("your-jwt-token")
                .Get<UserProfile>();
            
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
            var response = await context.CreateHttpRequest("/api/protected/resource")
                .WithAuthBasic("username", "password")
                .Get<Resource>();
            
            Assert.IsTrue(response.IsSuccessful);
        })
        .Run();
}
```

---

## Request Options

### Headers

```csharp
var response = await context.CreateHttpRequest("/api/data")
    .WithHeader("X-Custom-Header", "value")
    .WithHeader("Accept-Language", "en-US")
    .Get<Data>();
```

### Query Parameters

```csharp
var response = await context.CreateHttpRequest("/api/products")
    .WithQueryParam("category", "electronics")
    .WithQueryParam("maxPrice", "1000")
    .Get<List<Product>>();
```

### Timeout

```csharp
var response = await context.CreateHttpRequest("/api/slow-endpoint")
    .WithTimeout(TimeSpan.FromSeconds(60))
    .Get<Result>();
```

---

## Response Handling

### Status Code Checks

```csharp
var response = await context.CreateHttpRequest("/api/endpoint")
    .Get<Data>();

Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
Assert.IsTrue(response.IsSuccessful);
```

### Accessing Response Headers

```csharp
var response = await context.CreateHttpRequest("/api/endpoint")
    .Get<Data>();

var contentType = response.Headers.GetValues("Content-Type").FirstOrDefault();
```

### Error Handling

```csharp
var response = await context.CreateHttpRequest("/api/endpoint")
    .Get<Data>();

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
        httpConfig.Services.AddHttpClient<MyHttpClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.example.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = true
        });

        httpConfig.DefaultHttpClient<MyHttpClient>();

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
public async Task API_load_test()
{
    await Scenario()
        .Step("Call API endpoint", async context =>
        {
            var response = await context.CreateHttpRequest("/api/health")
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

See the [Load Testing](load-testing.md) documentation for more details on load test configuration and assertions.

---

[← Back to Table of Contents](README.md)
