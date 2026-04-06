using Fuzn.FluentWebSocket;
using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[TestClass]
public class WebSocketMessagingTests : Test
{
    private const string WebSocketServerUrl = "wss://localhost:7058/ws";

    [Test]
    public async Task Send_And_Receive_Text_Message()
    {
        await Scenario()
            .Step("Send text message and receive echo", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                var testMessage = "Hello WebSocket!";
                await connection.SendText(testMessage);

                var receivedMessage = await connection.Receive(TimeSpan.FromSeconds(5));

                Assert.AreEqual(testMessage, receivedMessage.Text, "Echo server should return the same message");

                await connection.Close();
            })
            .Run();
    }

    [Test]
    public async Task Send_And_Receive_Json_Message()
    {
        await Scenario()
            .Step("Send and receive JSON objects", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                var message = new WebSocketMessage
                {
                    Type = "test",
                    Content = "Hello from TestFuzn",
                    Timestamp = DateTime.UtcNow
                };

                await connection.Send(message);

                var receivedMessage = await connection.Receive<WebSocketMessage>(TimeSpan.FromSeconds(5));

                Assert.IsNotNull(receivedMessage.Data);
                Assert.AreEqual(message.Type, receivedMessage.Data.Type);
                Assert.AreEqual(message.Content, receivedMessage.Data.Content);

                await connection.Close();
            })
            .Run();
    }

    [Test]
    public async Task Send_And_Receive_Multiple_Messages()
    {
        await Scenario()
            .Step("Send multiple messages in sequence", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl);

                var messages = new[] { "Message 1", "Message 2", "Message 3" };

                foreach (var msg in messages)
                {
                    await connection.SendText(msg);
                }

                for (int i = 0; i < messages.Length; i++)
                {
                    var received = await connection.Receive(TimeSpan.FromSeconds(5));
                    Assert.AreEqual(messages[i], received.Text, $"Message {i + 1} should match");
                }

                await connection.Close();
            })
            .Run();
    }
}
