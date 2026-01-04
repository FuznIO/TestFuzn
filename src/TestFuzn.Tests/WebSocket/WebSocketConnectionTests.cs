using System.Net.WebSockets;
using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[TestClass]
public class WebSocketConnectionTests : Test
{
    // Use local SampleApp WebSocket server
    private const string WebSocketServerUrl = "wss://localhost:44316/ws";

    [Test]
    public async Task Connect_And_Disconnect_Successfully()
    {
        await Scenario()
            .Step("Connect to WebSocket server", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                Assert.IsTrue(connection.IsConnected, "Connection should be established");
                Assert.AreEqual(WebSocketState.Open, connection.State);

                await connection.Close();

                Assert.IsFalse(connection.IsConnected, "Connection should be closed");
            })
            .Run();
    }

    [Test]
    public async Task Connect_With_Custom_Headers()
    {
        await Scenario()
            .Step("Connect with custom headers", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Header("X-Custom-Header", "TestValue")
                    .Header("X-User-Agent", "TestFuzn-WebSocket-Tests")
                    .Connect();

                Assert.IsTrue(connection.IsConnected);

                await connection.Close();
            })
            .Run();
    }

    [Test]
    public async Task Connect_With_Custom_Timeout()
    {
        await Scenario()
            .Step("Connect with custom connection timeout", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .ConnectionTimeout(TimeSpan.FromSeconds(15))
                    .KeepAliveInterval(TimeSpan.FromSeconds(60))
                    .Connect();

                Assert.IsTrue(connection.IsConnected);

                await connection.Close();
            })
            .Run();
    }

    [Test]
    public async Task Connect_With_Verbosity_Settings()
    {
        await Scenario()
            .Step("Connect with different verbosity levels", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Verbosity(LoggingVerbosity.Full)
                    .Connect();

                Assert.IsTrue(connection.IsConnected);

                await connection.Close();
            })
            .Run();
    }

    [Test]
    public async Task Connection_State_Changes_Correctly()
    {
        await Scenario()
            .Step("Verify connection state transitions", async (context) =>
            {
                var builder = context.CreateWebSocketConnection(WebSocketServerUrl);
                var connection = builder.Build();

                Assert.AreEqual(WebSocketState.None, connection.State, "Initial state should be None");

                await connection.Connect();

                Assert.AreEqual(WebSocketState.Open, connection.State, "State should be Open after connect");
                Assert.IsTrue(connection.IsConnected);

                await connection.Close();

                // State could be Closed, CloseSent, or Aborted depending on server behavior and timing
                Assert.IsTrue(
                    connection.State == WebSocketState.Closed || 
                    connection.State == WebSocketState.CloseSent ||
                    connection.State == WebSocketState.Aborted,
                    $"State should be Closed, CloseSent, or Aborted after close, but was {connection.State}");
                
                Assert.IsFalse(connection.IsConnected, "Connection should not be connected after close");
            })
            .Run();
    }

    [Test]
    public async Task Dispose_Closes_Connection()
    {
        await Scenario()
            .Step("Verify disposal closes connection", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                Assert.IsTrue(connection.IsConnected);

                await connection.DisposeAsync();

                Assert.IsFalse(connection.IsConnected);
            })
            .Run();
    }

    [Test]
    public async Task Build_Without_Connect()
    {
        await Scenario()
            .Step("Build connection without connecting", (context) =>
            {
                var connection = context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Header("X-Test", "Value")
                    .Build();

                Assert.IsNotNull(connection);
                Assert.IsFalse(connection.IsConnected);
                Assert.AreEqual(WebSocketState.None, connection.State);
            })
            .Run();
    }

    [Test]
    public async Task Multiple_Headers_Can_Be_Added()
    {
        await Scenario()
            .Step("Add multiple headers using different methods", async (context) =>
            {
                var headers = new Dictionary<string, string>
                {
                    { "X-Header-1", "Value1" },
                    { "X-Header-2", "Value2" }
                };

                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Header("X-Header-3", "Value3")
                    .Headers(headers)
                    .Connect();

                Assert.IsTrue(connection.IsConnected);

                await connection.Close();
            })
            .Run();
    }
}
