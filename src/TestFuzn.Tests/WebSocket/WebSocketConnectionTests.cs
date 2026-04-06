using System.Net.WebSockets;
using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[TestClass]
public class WebSocketConnectionTests : Test
{
    private const string WebSocketServerUrl = "wss://localhost:7058/ws";

    [Test]
    public async Task Connect_And_Disconnect_Successfully()
    {
        await Scenario()
            .Step("Connect to WebSocket server", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

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
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl, request => request
                    .WithHeader("X-Custom-Header", "TestValue")
                    .WithHeader("X-User-Agent", "TestFuzn-WebSocket-Tests"));

                Assert.IsTrue(connection.IsConnected);

                await connection.Close();
            })
            .Run();
    }

    [Test]
    public async Task Connect_With_Custom_Timeout_And_KeepAlive()
    {
        await Scenario()
            .Step("Connect with custom connection timeout", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl, request => request
                    .WithTimeout(TimeSpan.FromSeconds(15))
                    .WithKeepAliveInterval(TimeSpan.FromSeconds(60)));

                Assert.IsTrue(connection.IsConnected);

                await connection.Close();
            })
            .Run();
    }

    [Test]
    public async Task Dispose_Closes_Connection()
    {
        await Scenario()
            .Step("Verify disposal closes connection", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                Assert.IsTrue(connection.IsConnected);

                await connection.DisposeAsync();

                Assert.IsFalse(connection.IsConnected);
            })
            .Run();
    }
}
