using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[FeatureTest]
public class WebSocketMessagingTests : BaseFeatureTest
{
    public override string FeatureName => "WebSocket Messaging";

    private const string WebSocketServerUrl = "wss://localhost:44316/ws";

    [ScenarioTest]
    public async Task Send_And_Receive_Text_Message()
    {
        await Scenario()
            .Step("Send text message and receive echo", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                var testMessage = "Hello WebSocket!";
                await connection.SendText(testMessage);

                var receivedMessage = await connection.WaitForMessage(TimeSpan.FromSeconds(5));

                Assert.AreEqual(testMessage, receivedMessage, "Echo server should return the same message");

                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Send_Multiple_Messages()
    {
        await Scenario()
            .Step("Send multiple messages in sequence", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                var messages = new[] { "Message 1", "Message 2", "Message 3" };

                foreach (var msg in messages)
                {
                    await connection.SendText(msg);
                }

                // Wait a bit for all messages to be received
                await Task.Delay(TimeSpan.FromSeconds(2));

                var receivedMessages = connection.GetReceivedMessages();

                Assert.AreEqual(messages.Length, receivedMessages.Count, "Should receive all sent messages");

                for (int i = 0; i < messages.Length; i++)
                {
                    Assert.AreEqual(messages[i], receivedMessages[i], $"Message {i + 1} should match");
                }

                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Send_And_Receive_Json_Message()
    {
        await Scenario()
            .Step("Send and receive JSON objects", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                var message = new WebSocketMessage
                {
                    Type = "test",
                    Content = "Hello from TestFuzn",
                    Timestamp = DateTime.UtcNow
                };

                await connection.SendJson(message);

                var receivedMessage = await connection.WaitForMessageAs<WebSocketMessage>(TimeSpan.FromSeconds(5));

                Assert.IsNotNull(receivedMessage);
                Assert.AreEqual(message.Type, receivedMessage.Type);
                Assert.AreEqual(message.Content, receivedMessage.Content);

                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task GetReceivedMessages_Returns_All_Messages()
    {
        await Scenario()
            .Step("Verify all received messages are buffered", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                await connection.SendText("Message 1");
                await connection.SendText("Message 2");

                // Wait for messages to be received
                await Task.Delay(TimeSpan.FromSeconds(1));

                var messages = connection.GetReceivedMessages();

                Assert.IsTrue(messages.Count >= 2, "Should have received at least 2 messages");

                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task ClearReceivedMessages_Empties_Buffer()
    {
        await Scenario()
            .Step("Clear message buffer and verify it's empty", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                await connection.SendText("Test message");
                await Task.Delay(TimeSpan.FromSeconds(1));

                var messagesBefore = connection.GetReceivedMessages();
                Assert.IsTrue(messagesBefore.Count > 0, "Should have messages before clearing");

                connection.ClearReceivedMessages();

                var messagesAfter = connection.GetReceivedMessages();
                Assert.AreEqual(0, messagesAfter.Count, "Message buffer should be empty after clearing");

                // Add delay before closing to ensure connection is stable
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                
                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task WaitForMessage_Times_Out_When_No_Message()
    {
        await Scenario()
            .Step("Verify timeout when waiting for message", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                // Wait a bit for any initial connection messages
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                
                // Clear any initial messages
                connection.ClearReceivedMessages();

                try
                {
                    await connection.WaitForMessage(TimeSpan.FromSeconds(1));
                    Assert.Fail("Should have thrown TimeoutException");
                }
                catch (TimeoutException ex)
                {
                    Assert.IsTrue(ex.Message.Contains("No message received"), "Exception should indicate timeout");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200));
                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task WaitForMessage_Returns_Immediately_If_Message_Available()
    {
        await Scenario()
            .Step("Verify WaitForMessage returns immediately when message is buffered", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                await connection.SendText("Immediate message");
                await Task.Delay(TimeSpan.FromMilliseconds(500)); // Let it arrive

                var startTime = DateTime.UtcNow;
                var message = await connection.WaitForMessage(TimeSpan.FromSeconds(10));
                var elapsed = DateTime.UtcNow - startTime;

                Assert.IsNotNull(message);
                Assert.IsTrue(elapsed.TotalSeconds < 2, "Should return immediately, not wait for timeout");

                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Send_Binary_Data()
    {
        await Scenario()
            .Step("Send binary data through WebSocket", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                var binaryData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
                await connection.SendBinary(binaryData);

                // Note: Echo server behavior with binary data may vary
                // Just verify no exception is thrown
                await Task.Delay(TimeSpan.FromMilliseconds(500));

                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Cannot_Send_When_Not_Connected()
    {
        await Scenario()
            .Step("Verify exception when sending without connection", async (context) =>
            {
                var connection = context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Build();

                try
                {
                    await connection.SendText("This should fail");
                    Assert.Fail("Should have thrown InvalidOperationException");
                }
                catch (InvalidOperationException ex)
                {
                    Assert.IsTrue(ex.Message.Contains("not connected"), "Exception should indicate no connection");
                }
            })
            .Run();
    }
}
