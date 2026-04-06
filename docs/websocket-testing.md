# WebSocket Testing

TestFuzn provides WebSocket testing capabilities through the [Fuzn.FluentWebSocket](https://github.com/FuznIO/FluentWebSocket) NuGet package, which offers a clean, chainable API for building WebSocket connections, sending and receiving messages, and streaming.

> **Full FluentWebSocket Documentation**: For complete details on all connection options, streaming, custom serialization, auto-reconnect, and more, see the [FluentWebSocket documentation](https://github.com/FuznIO/FluentWebSocket).

---

## Installation

Install the WebSocket plugin via NuGet:

```bash
dotnet add package Fuzn.TestFuzn.Plugins.WebSocket
```

---

## Setup

Enable the WebSocket plugin in your `Startup` class:

```csharp
public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.UseWebSocket();
    }
}
```

### Configuration Options

You can customize the default settings:

```csharp
configuration.UseWebSocket(config =>
{
    config.DefaultConnectionTimeout = TimeSpan.FromSeconds(15);
    config.DefaultKeepAliveInterval = TimeSpan.FromSeconds(60);
    config.ReceiveBufferSize = 8192;
    config.MaxMessageSize = 1024 * 1024; // 1 MB limit (0 = no limit)
    config.LogFailedConnectionsToTestConsole = true;
    config.Serializer = new SystemTextJsonSerializerProvider();
});
```

| Option | Default | Description |
|--------|---------|-------------|
| `DefaultConnectionTimeout` | 10 seconds | Timeout for establishing WebSocket connections |
| `DefaultKeepAliveInterval` | 30 seconds | Interval for keep-alive ping messages |
| `ReceiveBufferSize` | 4096 | Receive buffer size in bytes |
| `MaxMessageSize` | 0 (no limit) | Maximum allowed message size in bytes |
| `LogFailedConnectionsToTestConsole` | `false` | Log failed connections to test console |
| `Serializer` | `SystemTextJsonSerializerProvider` | JSON serializer for typed messages |

---

## Creating Connections

Use `context.CreateWebSocketConnection(url)` in your tests to create and connect a WebSocket connection:

```csharp
[Test]
public async Task Connect_to_websocket_server()
{
    await Scenario()
        .Step("Connect and send message", async context =>
        {
            var connection = await context.CreateWebSocketConnection("wss://example.com/ws");

            Assert.IsTrue(connection.IsConnected);

            await connection.Close();
        })
        .Run();
}
```

### Configuring Connections

Pass a configuration callback to customize the connection before it is established:

```csharp
var connection = await context.CreateWebSocketConnection("wss://example.com/ws", request => request
    .WithHeader("X-Custom-Header", "value")
    .WithAuthBearer("my-token")
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithKeepAliveInterval(TimeSpan.FromSeconds(60))
    .WithSubProtocol("graphql-ws")
    .WithCompression()
    .WithAutoReconnect());
```

All [FluentWebSocket builder methods](https://github.com/FuznIO/FluentWebSocket) are available on the request parameter, including:

| Method | Description |
|--------|-------------|
| `WithHeader(name, value)` | Add a custom header |
| `WithHeaders(dictionary)` | Add multiple headers |
| `WithAuthBearer(token)` | Set Bearer token authentication |
| `WithAuthBasic(user, pass)` | Set Basic authentication |
| `WithAuthApiKey(key, headerName?)` | Set API key authentication |
| `WithSubProtocol(protocol)` | Add a sub-protocol |
| `WithTimeout(duration)` | Set connection timeout |
| `WithKeepAliveInterval(interval)` | Set keep-alive interval |
| `WithCompression()` | Enable per-message deflate compression |
| `WithAutoReconnect(delay?, max?, retries?)` | Enable auto-reconnect with exponential backoff |
| `WithSerializer(provider)` | Set custom serializer |

---

## Sending Messages

### Text Messages

```csharp
await connection.SendText("Hello, WebSocket!");
```

### Binary Messages

```csharp
var data = new byte[] { 0x01, 0x02, 0x03 };
await connection.SendBinary(data);
```

### JSON Messages

```csharp
var message = new MyMessage { Type = "greeting", Content = "Hello!" };
await connection.Send(message);
```

---

## Receiving Messages

### Single Message

```csharp
// Receive with timeout
var message = await connection.Receive(TimeSpan.FromSeconds(5));
var text = message.Text;

// Receive with typed deserialization
var typedMessage = await connection.Receive<MyMessage>(TimeSpan.FromSeconds(5));
var data = typedMessage.Data;
```

### Request-Response Pattern

```csharp
// Send text and receive response
var response = await connection.SendTextAndReceive("ping");
Assert.AreEqual("pong", response.Text);

// Send typed request and receive typed response
var result = await connection.SendAndReceive<Request, Response>(myRequest);
Assert.IsNotNull(result.Data);
```

### Streaming with IAsyncEnumerable

```csharp
await foreach (var message in connection.Listen())
{
    if (message.IsClose) break;

    context.Comment($"Received: {message.Text}");
}
```

### Callback-based Listening

```csharp
await connection.OnMessage(async (message) =>
{
    context.Comment($"Received: {message.Text}");
    return true; // Return true to continue listening, false to stop
});
```

---

## Closing Connections

### Explicit Close

```csharp
var closeResult = await connection.Close();
```

### Automatic Cleanup

All connections created via `context.CreateWebSocketConnection()` are automatically tracked and closed when the test iteration ends. You don't need to explicitly close connections, but it's recommended for long-running tests.

```csharp
await Scenario()
    .Step("WebSocket test", async context =>
    {
        var connection = await context.CreateWebSocketConnection("wss://example.com/ws");

        await connection.SendText("Hello!");
        var response = await connection.Receive(TimeSpan.FromSeconds(5));

        // No need to close - framework handles cleanup automatically
    })
    .Run();
```

---

## WebSocket Load Testing

WebSocket connections can be used in load tests:

```csharp
[Test]
public async Task Websocket_load_test()
{
    await Scenario()
        .Step("Send and receive messages", async context =>
        {
            var connection = await context.CreateWebSocketConnection("wss://example.com/ws");

            var message = $"Message from {context.Info.CorrelationId}";
            await connection.SendText(message);

            var response = await connection.Receive(TimeSpan.FromSeconds(5));
            Assert.AreEqual(message, response.Text);

            await connection.Close();
        })
        .Load().Simulations((context, simulations) =>
        {
            simulations.FixedLoad(50, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        })
        .Run();
}
```

Each load test iteration gets its own connection manager, so connections are isolated and cleaned up per iteration.

See the [Load Testing](load-testing.md) documentation for more details on load test configuration and assertions.

---

## Custom Serialization

The plugin uses `System.Text.Json` by default. You can provide a custom serializer by implementing `Fuzn.FluentWebSocket.ISerializerProvider`:

```csharp
using Fuzn.FluentWebSocket;

public class NewtonsoftSerializerProvider : ISerializerProvider
{
    public string Serialize<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    public T? Deserialize<T>(string content)
    {
        return JsonConvert.DeserializeObject<T>(content);
    }
}
```

Configure it globally:

```csharp
configuration.UseWebSocket(config =>
{
    config.Serializer = new NewtonsoftSerializerProvider();
});
```

Or per-connection:

```csharp
var connection = await context.CreateWebSocketConnection("wss://example.com/ws", request => request
    .WithSerializer(new NewtonsoftSerializerProvider()));
```

---

[Back to Table of Contents](README.md)
