# HTTP Testing

TestFuzn provides a fluent HTTP client plugin for testing REST APIs.

---

## Basic HTTP Requests

```csharp
using Fuzn.TestFuzn.Plugins.Http;

[Test]
public async Task Get_products_from_api()
{
    await Scenario()
        .Step("Call API and verify response", async context =>
        {
            var response = await context.CreateHttpRequest("https://api.example.com/products")
                .Get();
            
            Assert.IsTrue(response.Ok);
            var products = response.BodyAs<List<Product>>();
            Assert.IsTrue(products.Count > 0);
        })
        .Run();
}
```

---

## Request Methods

```csharp
// GET
var response = await context.CreateHttpRequest(url).Get();

// POST with body
var response = await context.CreateHttpRequest(url)
    .Body(new { Name = "Product", Price = 99.99 })
    .Post();

// PUT
var response = await context.CreateHttpRequest(url)
    .Body(updatedProduct)
    .Put();

// DELETE
var response = await context.CreateHttpRequest(url).Delete();

// PATCH
var response = await context.CreateHttpRequest(url)
    .Body(patchData)
    .Patch();
```

---

## Authentication

```csharp
// Bearer token
var response = await context.CreateHttpRequest(url)
    .AuthBearer("your-jwt-token")
    .Get();

// Basic authentication
var response = await context.CreateHttpRequest(url)
    .AuthBasic("username", "password")
    .Get();
```

---

## Additional Request Options

```csharp
var response = await context.CreateHttpRequest(url)
    .Header("X-Custom-Header", "value")
    .Headers(new Dictionary<string, string> { { "Key", "Value" } })
    .Cookie("session", "abc123")
    .ContentType(ContentTypes.Json)
    .Accept(AcceptTypes.Json)
    .Timeout(TimeSpan.FromSeconds(30))
    .UserAgent("MyTestAgent/1.0")
    .Get();
```

---

## Response Handling

```csharp
// Check if successful
Assert.IsTrue(response.Ok);

// Get status code
Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

// Deserialize to type
var product = response.BodyAs<Product>();

// Parse as dynamic JSON
var json = response.BodyAsJson();
var name = json.name;

// Access raw body
var rawBody = response.Body;

// Access headers
var contentType = response.Headers.ContentType;

// Access cookies
var cookies = response.Cookies;

// Generate curl command for debugging
var curl = await response.GetCurlCommand();
```

---

## JSON Serialization

TestFuzn uses System.Text.Json by default but supports custom serializers:

```csharp
.Step("Use custom serializer", async context =>
{
    var serializer = new NewtonsoftSerializerProvider(); // If using Newtonsoft
    
    var response = await context.CreateHttpRequest(url)
        .SerializerProvider(serializer)
        .Get();
})

public class NewtonsoftSerializerProvider : ISerializerProvider
{
    private readonly JsonSerializerSettings _jsonSerializerSettings;

    public NewtonsoftSerializerProvider()
    {
        _jsonSerializerSettings = new JsonSerializerSettings();
    }

    public NewtonsoftSerializerProvider(JsonSerializerSettings jsonSerializerSettings)
    {
        _jsonSerializerSettings = jsonSerializerSettings;
    }

    public string Serialize<T>(T obj) where T : class
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var json = JsonConvert.SerializeObject(obj, _jsonSerializerSettings);
        return json;
    }

    public T Deserialize<T>(string json) where T : class
    {
        if (json == null)
            throw new ArgumentNullException(nameof(json));

        var obj = JsonConvert.DeserializeObject<T>(json, _jsonSerializerSettings);
        if (obj == null)
            throw new Exception($"Could not deserialize {typeof(T).Name} from JSON: {json}");
        return obj;
    }
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
            
            Assert.IsTrue(response.Ok);
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

[? Back to Table of Contents](README.md)
