# WebSocket Test Server - SampleApp

This document explains the WebSocket test server implementation in SampleApp.WebApp and how to run the WebSocket tests.

## WebSocket Endpoints

The SampleApp.WebApp provides two WebSocket echo endpoints:

- **`ws://localhost:5131/ws`** - Main WebSocket echo endpoint
- **`ws://localhost:5131/ws/echo`** - Alternative WebSocket echo endpoint

Both endpoints echo back any text or binary messages they receive, making them perfect for testing WebSocket functionality.

## Running the WebSocket Tests

### Prerequisites

1. **Start the SampleApp.WebApp server** (must be running before tests):
   ```bash
   cd SampleApp.WebApp
   dotnet run
   ```
   
   The server will start on:
   - HTTP: `http://localhost:5131`
   - HTTPS: `https://localhost:44316`

2. **Run the WebSocket tests** (in a separate terminal):
   ```bash
   cd TestFuzn.Tests
   dotnet test --filter "FeatureName~WebSocket"
   ```

### Available Test Suites

#### 1. WebSocketConnectionTests (10 tests)
Tests connection lifecycle and configuration:
- Connect and disconnect
- Custom headers
- Custom timeouts
- Connection state transitions
- Disposal behavior

#### 2. WebSocketMessagingTests (10 tests)
Tests message sending and receiving:
- Text messages
- JSON serialization
- Binary data
- Message buffering
- Timeout behavior

#### 3. WebSocketHooksTests (7 tests)
Tests the hook system:
- PreConnect, PostConnect, OnMessageReceived, OnDisconnect
- Hook execution order
- Hook context access

#### 4. WebSocketLoadTests (8 tests)
Tests load testing scenarios:
- Concurrent connections
- High-throughput messaging
- Long-running connections
- Performance measurements

#### 5. WebSocketSubStepsTests (4 tests)
Tests nested step functionality:
- WebSocket operations in sub-steps
- Deep nesting with JSON
- Hook integration in sub-steps

## Implementation Details

### WebSocket Handler (`SampleApp.WebApp/WebSockets/WebSocketHandler.cs`)

The handler implements a simple echo server that:
- Accepts WebSocket connections
- Receives text and binary messages
- Echoes messages back to the client
- Handles connection lifecycle (open, close, errors)
- Logs all WebSocket activity

### Configuration

WebSocket support is configured in `Program.cs`:

```csharp
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
        await handler.HandleWebSocketConnection(context, webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});
```

## Testing Locally

You can manually test the WebSocket server using any WebSocket client tool:

### Using Browser Console
```javascript
const ws = new WebSocket('ws://localhost:5131/ws');
ws.onopen = () => {
    console.log('Connected');
    ws.send('Hello Server!');
};
ws.onmessage = (event) => {
    console.log('Received:', event.data);
};
```

### Using wscat (npm package)
```bash
npm install -g wscat
wscat -c ws://localhost:5131/ws
```

Then type messages and press Enter to send them. You'll see them echoed back.

## Troubleshooting

### Tests Fail with Connection Errors

**Problem**: Tests fail with "Connection refused" or similar errors.

**Solution**: Make sure SampleApp.WebApp is running before executing tests:
```bash
# Terminal 1
cd SampleApp.WebApp
dotnet run

# Terminal 2 (wait for server to start)
cd TestFuzn.Tests
dotnet test --filter "FeatureName~WebSocket"
```

### Port Already in Use

**Problem**: SampleApp won't start because port 5131 is in use.

**Solution**: Either:
1. Stop the process using port 5131
2. Or change the port in `SampleApp.WebApp/Properties/launchSettings.json` and update the test constant `WebSocketServerUrl` in all test files

### SSL/TLS Errors

**Problem**: Connection errors with `wss://` (secure WebSocket).

**Solution**: The tests use `ws://` (non-secure) by default. If you need to test with HTTPS/WSS:
1. Update test constant to `wss://localhost:44316/ws`
2. Ensure dev certificates are trusted: `dotnet dev-certs https --trust`

## Architecture

```
SampleApp.WebApp
??? WebSockets/
?   ??? WebSocketHandler.cs      # Echo handler implementation
?   ??? ChatMessage.cs            # Example message model
??? Program.cs                    # WebSocket middleware configuration

TestFuzn.Tests/WebSocket/
??? WebSocketConnectionTests.cs  # Connection lifecycle tests
??? WebSocketMessagingTests.cs   # Messaging functionality tests
??? WebSocketHooksTests.cs       # Hook system tests
??? WebSocketLoadTests.cs        # Load testing scenarios
??? WebSocketSubStepsTests.cs    # Nested steps tests
??? WebSocketMessage.cs          # Test message model
```

## Future Enhancements

Potential improvements for the WebSocket test server:

1. **Broadcast Support**: Allow messages to be broadcast to multiple connected clients
2. **Room/Channel Support**: Group connections into rooms/channels
3. **Authentication**: Add token-based WebSocket authentication
4. **Message Filtering**: Add endpoints that filter or transform messages
5. **Delayed Echo**: Add configurable delays to test timeout scenarios
6. **Error Simulation**: Add endpoints that deliberately trigger errors for negative testing

## Related Documentation

- [TestFuzn.Plugins.WebSocket README](../../TestFuzn.Plugins.WebSocket/README.md) - Plugin API documentation
- [TestFuzn Load Testing Guide](../../docs/LoadTesting.md) - Load testing best practices
- [ASP.NET Core WebSockets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets) - Microsoft documentation
