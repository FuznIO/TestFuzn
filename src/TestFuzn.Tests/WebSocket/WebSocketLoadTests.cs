using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[TestClass]
public class WebSocketLoadTests : Test
{
    private const string WebSocketServerUrl = "wss://localhost:7058/ws";

    [Test]
    public async Task Concurrent_Connections_OneTime_Load()
    {
        await Scenario()
            .Step("Create concurrent WebSocket connections", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                var message = $"Message from iteration {context.Info.CorrelationId}";
                await connection.SendText(message);

                var response = await connection.Receive(TimeSpan.FromSeconds(10));

                Assert.AreEqual(message, response.Text, "Echo server should return the same message");

                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();
    }

    [Test]
    public async Task Fixed_Load_Multiple_Messages_Per_Connection()
    {
        await Scenario()
            .Step("Send multiple messages in load test", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                for (int i = 1; i <= 3; i++)
                {
                    var message = $"Iteration {context.Info.CorrelationId} - Message {i}";
                    await connection.SendText(message);

                    var response = await connection.Receive(TimeSpan.FromSeconds(5));
                    Assert.AreEqual(message, response.Text);
                }

                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.FixedLoad(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)))
            .Run();
    }

    [Test]
    public async Task Load_Test_With_JSON_Messages()
    {
        await Scenario()
            .Step("Send JSON messages in load test", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                var message = new WebSocketMessage
                {
                    Type = "load-test",
                    Content = $"Load test message from {context.Info.CorrelationId}",
                    Timestamp = DateTime.UtcNow
                };

                await connection.Send(message);

                var response = await connection.Receive<WebSocketMessage>(TimeSpan.FromSeconds(5));

                Assert.IsNotNull(response.Data);
                Assert.AreEqual(message.Type, response.Data.Type);

                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();
    }
}
