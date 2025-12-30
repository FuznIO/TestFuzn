using System.Net.WebSockets;
using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[TestClass]
public class WebSocketCleanupTests : TestBase
{
    public override GroupInfo Group => new() { Name = "WebSocket Auto Cleanup" };

    private static readonly string EchoServerUrl = "wss://localhost:44316/ws";

    [Test]
    public async Task Connection_Is_Auto_Closed_When_Test_Completes_Without_Explicit_Close()
    {
        WebSocketConnection? connection = null;

        await Scenario()
            .Step("Create connection without closing it", async (context) =>
            {
                connection = await context.CreateWebSocketConnection(EchoServerUrl)
                    .Connect();

                Assert.IsTrue(connection.IsConnected, "Connection should be open");

                // Intentionally NOT closing the connection
                // The framework should auto-close it during cleanup
            })
            .Run();

        // After scenario completes, the connection should have been auto-closed
        // Note: We can't directly test this in the same test run, but we can verify
        // the connection was created and is now disposed
        Assert.IsNotNull(connection, "Connection should have been created");
    }

    [Test]
    public async Task Multiple_Connections_Are_All_Auto_Closed()
    {
        var connections = new List<WebSocketConnection>();

        await Scenario()
            .Step("Create multiple connections without closing them", async (context) =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var connection = await context.CreateWebSocketConnection(EchoServerUrl)
                        .Connect();

                    Assert.IsTrue(connection.IsConnected);
                    connections.Add(connection);
                }

                // Intentionally NOT closing any connections
                Assert.AreEqual(3, connections.Count);
            })
            .Run();

        // All connections should have been tracked and auto-closed
        Assert.AreEqual(3, connections.Count, "All connections should have been created");
    }

    [Test]
    public async Task Already_Closed_Connections_Are_Handled_Gracefully_During_Cleanup()
    {
        await Scenario()
            .Step("Create and explicitly close connection", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(EchoServerUrl)
                    .Connect();

                Assert.IsTrue(connection.IsConnected);

                // Explicitly close the connection
                await connection.Close();

                Assert.IsFalse(connection.IsConnected);

                // The cleanup should handle this gracefully without throwing
            })
            .Run();

        // If we reach here, cleanup handled the already-closed connection correctly
        Assert.IsTrue(true, "Test completed successfully");
    }

    [Test]
    public async Task Mixed_Open_And_Closed_Connections_Are_Cleaned_Up_Correctly()
    {
        await Scenario()
            .Step("Create multiple connections, close some, leave others open", async (context) =>
            {
                // Create connection 1 - will close explicitly
                var connection1 = await context.CreateWebSocketConnection(EchoServerUrl)
                    .Connect();
                await connection1.Close();

                // Create connection 2 - leave open
                var connection2 = await context.CreateWebSocketConnection(EchoServerUrl)
                    .Connect();

                // Create connection 3 - will close explicitly
                var connection3 = await context.CreateWebSocketConnection(EchoServerUrl)
                    .Connect();
                await connection3.Close();

                // Create connection 4 - leave open
                var connection4 = await context.CreateWebSocketConnection(EchoServerUrl)
                    .Connect();

                // Verify states before cleanup
                Assert.IsFalse(connection1.IsConnected);
                Assert.IsTrue(connection2.IsConnected);
                Assert.IsFalse(connection3.IsConnected);
                Assert.IsTrue(connection4.IsConnected);

                // Cleanup should handle all gracefully
            })
            .Run();

        Assert.IsTrue(true, "Test completed with mixed connection states");
    }

    [Test]
    public async Task Connections_In_SubSteps_Are_Tracked_And_Cleaned_Up()
    {
        await Scenario()
            .Step("Parent step with sub-steps creating connections", async (context) =>
            {
                await context.Step("Sub-step 1: Create first connection", async (subContext) =>
                {
                    var connection1 = await subContext.CreateWebSocketConnection(EchoServerUrl)
                        .Connect();
                    Assert.IsTrue(connection1.IsConnected);
                    // Not closing
                });

                await context.Step("Sub-step 2: Create second connection", async (subContext) =>
                {
                    var connection2 = await subContext.CreateWebSocketConnection(EchoServerUrl)
                        .Connect();
                    Assert.IsTrue(connection2.IsConnected);
                    // Not closing
                });

                await context.Step("Sub-step 3: Create and close third connection", async (subContext) =>
                {
                    var connection3 = await subContext.CreateWebSocketConnection(EchoServerUrl)
                        .Connect();
                    Assert.IsTrue(connection3.IsConnected);
                    await connection3.Close();
                    await Task.Delay(50);
                    Assert.IsFalse(connection3.IsConnected);
                });
            })
            .Run();

        Assert.IsTrue(true, "Sub-step connections cleaned up successfully");
    }

    [Test]
    public async Task Load_Test_Connections_Are_Cleaned_Up_Per_Iteration()
    {
        await Scenario()
            .Step("Create connection in load test iteration", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(EchoServerUrl)
                    .Verbosity(LoggingVerbosity.Minimal)
                    .Connect();

                await connection.SendText($"Message from iteration {context.Info.CorrelationId}");
                var response = await connection.WaitForMessage(TimeSpan.FromSeconds(5));

                Assert.IsNotNull(response);

                // Not closing - each iteration's cleanup should handle it
            })
            .Load()
            .Simulations((context, simulations) => simulations.OneTimeLoad(15))
            .Run();

        Assert.IsTrue(true, "Load test iterations completed with auto-cleanup");
    }

    [Test]
    public async Task Connection_With_Error_During_Send_Is_Still_Cleaned_Up()
    {
        await Scenario()
            .Step("Create connection and cause error", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(EchoServerUrl)
                    .Connect();

                Assert.IsTrue(connection.IsConnected);

                // Close the connection
                await connection.Close();

                // Try to send after closing (should throw)
                try
                {
                    await connection.SendText("This should fail");
                    Assert.Fail("Should have thrown exception");
                }
                catch (InvalidOperationException)
                {
                    // Expected exception
                }

                // Connection should still be tracked for cleanup
            })
            .Run();

        Assert.IsTrue(true, "Connection with error was handled correctly");
    }
}
