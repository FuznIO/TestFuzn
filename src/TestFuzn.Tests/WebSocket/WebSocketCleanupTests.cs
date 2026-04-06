using Fuzn.FluentWebSocket;
using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[TestClass]
public class WebSocketCleanupTests : Test
{
    private const string WebSocketServerUrl = "wss://localhost:7058/ws";

    [Test]
    public async Task Connection_Is_Auto_Closed_When_Test_Completes_Without_Explicit_Close()
    {
        FluentWebSocketConnection? connection = null;

        await Scenario()
            .Step("Create connection without closing it", async (context) =>
            {
                connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                Assert.IsTrue(connection.IsConnected, "Connection should be open");
            })
            .Run();

        Assert.IsNotNull(connection, "Connection should have been created");
        Assert.IsFalse(connection.IsConnected, "Connection should have been auto-closed by cleanup");
    }

    [Test]
    public async Task Multiple_Connections_Are_All_Auto_Closed()
    {
        var connections = new List<FluentWebSocketConnection>();

        await Scenario()
            .Step("Create multiple connections without closing them", async (context) =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);
                    Assert.IsTrue(connection.IsConnected);
                    connections.Add(connection);
                }
            })
            .Run();

        foreach (var connection in connections)
        {
            Assert.IsFalse(connection.IsConnected, "Connection should have been auto-closed by cleanup");
        }
    }

    [Test]
    public async Task Already_Closed_Connections_Are_Handled_Gracefully_During_Cleanup()
    {
        await Scenario()
            .Step("Create and explicitly close connection", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                await connection.Close();

                Assert.IsFalse(connection.IsConnected);
                // Cleanup should handle this gracefully without throwing
            })
            .Run();
    }

    [Test]
    public async Task Load_Test_Connections_Are_Cleaned_Up_Per_Iteration()
    {
        var connectionsFromIterations = new System.Collections.Concurrent.ConcurrentBag<FluentWebSocketConnection>();

        await Scenario()
            .Step("Create connection in load test iteration", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                await connection.SendText($"Message from iteration {context.Info.CorrelationId}");
                var response = await connection.Receive(TimeSpan.FromSeconds(5));
                Assert.IsNotNull(response.Text);

                connectionsFromIterations.Add(connection);
                // Not closing - each iteration's cleanup should handle it
            })
            .Load()
            .Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();

        Assert.IsNotEmpty(connectionsFromIterations, "Should have tracked connections from iterations");
        foreach (var connection in connectionsFromIterations)
        {
            Assert.IsFalse(connection.IsConnected, "Connection should have been auto-closed by iteration cleanup");
        }
    }
}
