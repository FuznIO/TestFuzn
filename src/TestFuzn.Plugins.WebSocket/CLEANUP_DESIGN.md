# WebSocket Plugin - Automatic Connection Cleanup

## Overview

The WebSocket plugin implements automatic connection tracking and cleanup, following the same pattern as the Playwright plugin. This ensures that all WebSocket connections are properly closed and disposed when a test scenario completes, even if the test code doesn't explicitly close them.

## How It Works

### Architecture

1. **WebSocketManager** - Tracks all connections created within a context
2. **WebSocketPlugin** - Creates a manager per context and triggers cleanup
3. **WebSocketConnectionBuilder** - Registers connections with the manager when built
4. **Automatic Cleanup** - Triggered after each test scenario completes

### Component Responsibilities

#### WebSocketManager (`Internals/WebSocketManager.cs`)
```csharp
internal class WebSocketManager
{
    private readonly IList<WebSocketConnection> _connections;
    
    public void TrackConnection(WebSocketConnection connection);
    public async ValueTask CleanupContext();
}
```

- Maintains a thread-safe list of all connections created in a context
- `TrackConnection()` - Registers a new connection for cleanup
- `CleanupContext()` - Called automatically after scenario completion:
  - Checks each connection's state
  - Gracefully closes open connections
  - Disposes all connections to release resources
  - Swallows exceptions for already-closed connections

#### WebSocketPlugin (`Internals/WebSocketPlugin.cs`)
```csharp
internal class WebSocketPlugin : IContextPlugin
{
    public bool RequireState => true;
    
    public object InitContext() 
        => new WebSocketManager();
    
    public async Task CleanupContext(object state)
        => await ((WebSocketManager)state).CleanupContext();
}
```

- `RequireState = true` - Indicates the plugin maintains per-context state
- `InitContext()` - Creates a new manager for each test context
- `CleanupContext()` - Automatically called by the framework after scenario completion

#### IContextExtensions
```csharp
public static WebSocketConnectionBuilder CreateWebSocketConnection(
    this Context context, 
    string url)
{
    var manager = context.Internals.Plugins
        .GetState<WebSocketManager>(typeof(WebSocketPlugin));
    
    return new WebSocketConnectionBuilder(context, url, manager);
}
```

- Retrieves the manager from the plugin state
- Passes it to the builder for connection tracking

#### WebSocketConnectionBuilder
```csharp
public WebSocketConnection Build()
{
    var connection = new WebSocketConnection(...);
    
    // Track the connection for automatic cleanup
    _manager.TrackConnection(connection);
    
    return connection;
}
```

- Registers every connection with the manager immediately upon creation
- Tracking happens in `Build()`, so both `Build()` and `Connect()` are tracked

## Usage

### Basic Usage (Automatic Cleanup)

```csharp
await Scenario()
    .Step("WebSocket test", async (context) =>
    {
        var connection = await context.CreateWebSocketConnection("wss://example.com")
            .Connect();
        
        await connection.SendText("Hello!");
        var response = await connection.WaitForMessage();
        
        // No need to call connection.Close()
        // Framework will auto-close when scenario completes
    })
    .Run();
```

### Explicit Cleanup (Recommended for Long Tests)

```csharp
await Scenario()
    .Step("WebSocket test with explicit close", async (context) =>
    {
        var connection = await context.CreateWebSocketConnection("wss://example.com")
            .Connect();
        
        await connection.SendText("Hello!");
        var response = await connection.WaitForMessage();
        
        // Explicitly close when done (recommended)
        await connection.Close();
        
        // Automatic cleanup will handle gracefully even if already closed
    })
    .Run();
```

### Multiple Connections

```csharp
await Scenario()
    .Step("Multiple connections", async (context) =>
    {
        var conn1 = await context.CreateWebSocketConnection("wss://server1.com").Connect();
        var conn2 = await context.CreateWebSocketConnection("wss://server2.com").Connect();
        var conn3 = await context.CreateWebSocketConnection("wss://server3.com").Connect();
        
        // Use connections...
        
        // All three will be auto-closed when scenario completes
    })
    .Run();
```

### Load Testing

```csharp
await Scenario()
    .Step("Load test", async (context) =>
    {
        var connection = await context.CreateWebSocketConnection("wss://example.com")
            .Connect();
        
        await connection.SendText($"Message {context.Info.CorrelationId}");
        
        // Each iteration gets its own manager
        // Connections are cleaned up after each iteration completes
    })
    .Load()
    .Simulations((context, builder) => builder.OneTimeLoad(100))
    .Run();
```

### Sub-Steps

```csharp
await Scenario()
    .Step("Parent step", async (context) =>
    {
        context.Step("Sub-step 1", async (subContext) =>
        {
            var conn = await subContext.CreateWebSocketConnection("wss://example.com")
                .Connect();
            // Will be auto-closed
        });
        
        context.Step("Sub-step 2", async (subContext) =>
        {
            var conn = await subContext.CreateWebSocketConnection("wss://example.com")
                .Connect();
            // Will be auto-closed
        });
    })
    .Run();
```

## Cleanup Behavior

### When Cleanup Occurs

- **Feature Tests**: After each scenario completes
- **Load Tests**: After each iteration completes
- **Sub-Steps**: After the parent step completes (all sub-step connections tracked together)

### What Gets Cleaned Up

For each tracked connection:
1. Check if connection state is `WebSocketState.Open`
2. If open, gracefully close with status `NormalClosure` and message "Test scenario completed - auto cleanup"
3. Call `DisposeAsync()` to release all resources
4. Exceptions during cleanup are caught and swallowed (connection may already be closed/disposed)

### Thread Safety

- Connection tracking uses lock-based synchronization
- Safe for concurrent load tests
- Each iteration gets its own manager instance

## Benefits

1. **Resource Cleanup** - Prevents connection leaks in tests
2. **Test Isolation** - Each scenario/iteration gets fresh manager state
3. **Simplicity** - Tests don't need explicit cleanup code
4. **Robustness** - Handles already-closed connections gracefully
5. **Consistency** - Follows established Playwright plugin pattern

## Testing

See `TestFuzn.Tests\WebSocket\WebSocketCleanupTests.cs` for comprehensive tests:
- Auto-close of open connections
- Multiple connection cleanup
- Already-closed connection handling
- Mixed open/closed connection scenarios
- Sub-step connection tracking
- Load test iteration cleanup
- Error scenario handling

## Design Rationale

### Why Track in Build() Instead of Connect()?

Connections are tracked when `Build()` is called because:
- Some tests may create connections without immediately connecting
- Ensures all created connections are tracked, regardless of usage pattern
- Disposal should happen for all connection objects, not just connected ones

### Why Swallow Cleanup Exceptions?

Cleanup occurs after the test completes:
- Test may have already closed/disposed connections
- Connection may be in error state
- Logger/context may not be available
- Cleanup is best-effort to prevent resource leaks
- Exceptions shouldn't fail the test retroactively

### Why Per-Context State?

Each test context (scenario/iteration) gets its own manager:
- Provides test isolation
- Prevents connection sharing between tests
- Supports concurrent load testing
- Matches framework's context lifecycle

## Future Enhancements

Potential improvements:
- [ ] Optional cleanup logging (when logger available)
- [ ] Configurable cleanup timeout
- [ ] Connection state statistics
- [ ] Cleanup failure metrics for monitoring
- [ ] Diagnostic mode for troubleshooting connection leaks
