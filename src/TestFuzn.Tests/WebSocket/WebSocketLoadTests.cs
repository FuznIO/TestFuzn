using Fuzn.TestFuzn.Attributes;
using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[TestClass]
public class WebSocketLoadTests : BaseFeatureTest
{
    public override string FeatureName => "WebSocket Load Testing";

    private const string WebSocketServerUrl = "wss://localhost:44316/ws";

    [Test]
    public async Task Concurrent_Connections_OneTime_Load()
    {
        await Scenario()
            .Step("Create concurrent WebSocket connections", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Connect();

                var message = $"Message from iteration {context.Info.CorrelationId}";
                await connection.SendText(message);

                var response = await connection.WaitForMessage(TimeSpan.FromSeconds(10));

                Assert.AreEqual(message, response, "Echo server should return the same message");

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
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Verbosity(LoggingVerbosity.Minimal)
                    .Connect();

                // Send 5 messages per connection
                for (int i = 1; i <= 5; i++)
                {
                    var message = $"Iteration {context.Info.CorrelationId} - Message {i}";
                    await connection.SendText(message);

                    var response = await connection.WaitForMessage(TimeSpan.FromSeconds(5));
                    Assert.AreEqual(message, response);
                }

                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.FixedLoad(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)))
            .Run();
    }

    [Test]
    public async Task Long_Running_Connection_With_Periodic_Messages()
    {
        await Scenario()
            .Step("Maintain long-running connection with periodic messages", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .KeepAliveInterval(TimeSpan.FromSeconds(30))
                    .Verbosity(LoggingVerbosity.Minimal)
                    .Connect();

                // Send messages every 2 seconds for 10 seconds
                for (int i = 0; i < 5; i++)
                {
                    await connection.SendText($"Periodic message {i + 1}");
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .Run();
    }

    [Test]
    public async Task Load_Test_With_JSON_Messages()
    {
        await Scenario()
            .Step("Send JSON messages in load test", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Verbosity(LoggingVerbosity.Minimal)
                    .Connect();

                var message = new WebSocketMessage
                {
                    Type = "load-test",
                    Content = $"Load test message from {context.Info.CorrelationId}",
                    Timestamp = DateTime.UtcNow
                };

                await connection.SendJson(message);

                var response = await connection.WaitForMessageAs<WebSocketMessage>(TimeSpan.FromSeconds(5));

                Assert.IsNotNull(response);
                Assert.AreEqual(message.Type, response.Type);

                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.OneTimeLoad(20))
            .Run();
    }

    [Test]
    public async Task Load_Test_Verifies_No_Message_Loss()
    {
        await Scenario()
            .Step("Verify all messages are received during load", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Verbosity(LoggingVerbosity.Minimal)
                    .Connect();

                const int messageCount = 10;

                // Send multiple messages quickly
                for (int i = 1; i <= messageCount; i++)
                {
                    await connection.SendText($"Message {i}");
                }

                // Wait for all messages to be received
                await Task.Delay(TimeSpan.FromSeconds(3));

                var receivedMessages = connection.GetReceivedMessages();

                Assert.AreEqual(messageCount, receivedMessages.Count, 
                    $"Should receive all {messageCount} messages, but got {receivedMessages.Count}");

                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Run();
    }

    [Test]
    public async Task Load_Test_With_Shared_Data_Across_Iterations()
    {
        await Scenario()
            .Step("Track messages across iterations", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Verbosity(LoggingVerbosity.Minimal)
                    .Connect();

                await connection.SendText("Test message");
                
                var response = await connection.WaitForMessage(TimeSpan.FromSeconds(5));
                Assert.IsNotNull(response);

                // Note: In load tests, shared data access should be thread-safe
                // This is just for demonstration
                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();
    }

    [Test]
    [Skip] // Remove this attribute when you want to run this intensive test
    public async Task Stress_Test_High_Concurrency()
    {
        await Scenario()
            .Step("Stress test with high concurrency", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Verbosity(LoggingVerbosity.None)
                    .ConnectionTimeout(TimeSpan.FromSeconds(30))
                    .Connect();

                await connection.SendText($"Stress test message {context.Info.CorrelationId}");

                var response = await connection.WaitForMessage(TimeSpan.FromSeconds(10));
                Assert.IsNotNull(response);

                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.FixedLoad(100, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)))
            .Run();
    }

    [Test]
    public async Task Load_Test_Measures_Connection_Duration()
    {
        await Scenario()
            .Step("Measure connection establishment and message roundtrip", async (context) =>
            {
                var startTime = DateTime.UtcNow;

                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .Verbosity(LoggingVerbosity.Minimal)
                    .Connect();

                var connectionTime = DateTime.UtcNow - startTime;
                context.Comment($"Connection took {connectionTime.TotalMilliseconds:F2}ms");

                var messageStart = DateTime.UtcNow;
                await connection.SendText("Performance test");
                var response = await connection.WaitForMessage(TimeSpan.FromSeconds(5));
                var roundTripTime = DateTime.UtcNow - messageStart;

                context.Comment($"Message round-trip took {roundTripTime.TotalMilliseconds:F2}ms");

                Assert.IsNotNull(response);
                Assert.IsTrue(connectionTime.TotalSeconds < 5, "Connection should establish quickly");
                Assert.IsTrue(roundTripTime.TotalSeconds < 3, "Message round-trip should be fast");

                await connection.Close();
            })
            .Load()
            .Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();
    }
}
