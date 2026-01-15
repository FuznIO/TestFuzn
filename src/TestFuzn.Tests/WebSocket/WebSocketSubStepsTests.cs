using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[TestClass]
public class WebSocketSubStepsTests : Test
{
    private const string WebSocketServerUrl = "wss://localhost:44316/ws";

    [Test]
    public async Task WebSocket_With_Nested_Steps()
    {
        await Scenario()
            .Step("WebSocket scenario with sub-steps", async (context) =>
            {
                context.Comment("Main step: Setting up WebSocket connection");

                await context.Step("Connect to server", async (subContext) =>
                {
                    var connection = await subContext.CreateWebSocketConnection(WebSocketServerUrl)
                        .Connect();

                    Assert.IsTrue(connection.IsConnected);
                    subContext.SetSharedData("WebSocketConnection", connection);
                    subContext.Comment("Successfully connected to WebSocket server");
                });

                await context.Step("Send messages", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<WebSocketConnection>("WebSocketConnection");

                    await subContext.Step("Send first message", async (subContext2) =>
                    {
                        await connection.SendText("First message");
                        var response = await connection.WaitForMessage(TimeSpan.FromSeconds(5));
                        Assert.AreEqual("First message", response);
                        subContext2.Comment("First message sent and received");
                    });

                    await subContext.Step("Send second message", async (subContext2) =>
                    {
                        await connection.SendText("Second message");
                        var response = await connection.WaitForMessage(TimeSpan.FromSeconds(5));
                        Assert.AreEqual("Second message", response);
                        subContext2.Comment("Second message sent and received");
                    });
                });

                await context.Step("Cleanup connection", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<WebSocketConnection>("WebSocketConnection");
                    await connection.Close();
                    Assert.IsFalse(connection.IsConnected);
                    subContext.Comment("Connection closed successfully");
                });
            })
            .Run();
    }

    [Test]
    public async Task WebSocket_Multiple_Scenarios_With_Nested_Steps()
    {
        await Scenario()
            .Step("First connection scenario", async (context) =>
            {
                await context.Step("Setup connection", async (subContext) =>
                {
                    var connection = await subContext.CreateWebSocketConnection(WebSocketServerUrl)
                        .Header("X-Scenario", "First")
                        .Connect();

                    subContext.SetSharedData("Connection1", connection);
                });

                await context.Step("Test connection", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<WebSocketConnection>("Connection1");
                    await connection.SendText("Test from scenario 1");
                    var response = await connection.WaitForMessage();
                    Assert.IsNotNull(response);
                });

                await context.Step("Close connection", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<WebSocketConnection>("Connection1");
                    await connection.Close();
                });
            })
            .Step("Second connection scenario", async (context) =>
            {
                await context.Step("Setup new connection", async (subContext) =>
                {
                    var connection = await subContext.CreateWebSocketConnection(WebSocketServerUrl)
                        .Header("X-Scenario", "Second")
                        .Connect();

                    subContext.SetSharedData("Connection2", connection);
                });

                await context.Step("Test new connection", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<WebSocketConnection>("Connection2");
                    await connection.SendText("Test from scenario 2");
                    var response = await connection.WaitForMessage();
                    Assert.IsNotNull(response);
                });

                await context.Step("Close new connection", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<WebSocketConnection>("Connection2");
                    await connection.Close();
                });
            })
            .Run();
    }

    [Test]
    public async Task WebSocket_Deep_Nested_Steps_With_JSON()
    {
        await Scenario()
            .Step("WebSocket JSON scenario", async (context) =>
            {
                WebSocketConnection connection;

                await context.Step("Initialize", async (subContext) =>
                {
                    connection = await subContext.CreateWebSocketConnection(WebSocketServerUrl)
                        .Connect();

                    // Set it in parent context so sibling steps can access it
                    context.SetSharedData("Connection", connection);

                    subContext.Step("Verify connection", (subContext2) =>
                    {
                        Assert.IsTrue(connection.IsConnected);
                        subContext2.Comment("Connection verified");
                    });
                });

                await context.Step("Send JSON data", async (subContext) =>
                {
                    // Access from parent context
                    connection = context.GetSharedData<WebSocketConnection>("Connection");

                    subContext.Step("Prepare message", (subContext2) =>
                    {
                        var message = new WebSocketMessage
                        {
                            Type = "nested-step",
                            Content = "Deep nested test",
                            Timestamp = DateTime.UtcNow
                        };
                        // Set in sub-context for its children
                        subContext.SetSharedData("Message", message);
                        subContext2.Comment("Message prepared");
                    });

                    await subContext.Step("Send and verify", async (subContext2) =>
                    {
                        var message = subContext.GetSharedData<WebSocketMessage>("Message");
                        await connection.SendJson(message);

                        var response = await connection.WaitForMessageAs<WebSocketMessage>(TimeSpan.FromSeconds(5));
                        Assert.IsNotNull(response);
                        Assert.AreEqual(message.Type, response.Type);
                        subContext2.Comment($"Message sent and verified: {response.Type}");
                    });
                });

                await context.Step("Cleanup", async (_) =>
                {
                    connection = context.GetSharedData<WebSocketConnection>("Connection");
                    await connection.Close();
                });
            })
            .Run();
    }

    [Test]
    public async Task WebSocket_With_Hooks_In_SubSteps()
    {
        await Scenario()
            .Step("Configure WebSocket with hooks", async (context) =>
            {
                var hooksCalled = new List<string>();
                context.SetSharedData("HooksCalled", hooksCalled);

                await context.Step("Create connection with hooks", async (subContext) =>
                {
                    var hooks = subContext.GetSharedData<List<string>>("HooksCalled");

                    var connection = await subContext.CreateWebSocketConnection(WebSocketServerUrl)
                        .OnPreConnect((_) => hooks.Add("PreConnect"))
                        .OnPostConnect((_) => hooks.Add("PostConnect"))
                        .OnMessageReceived((_, msg) => hooks.Add($"OnMessage: {msg}"))
                        .OnDisconnect((_) => hooks.Add("OnDisconnect"))
                        .Connect();

                    subContext.SetSharedData("Connection", connection);
                });

                await context.Step("Send test message", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<WebSocketConnection>("Connection");
                    await connection.SendText("Hook test");
                    await Task.Delay(TimeSpan.FromSeconds(1));
                });

                await context.Step("Close and verify hooks", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<WebSocketConnection>("Connection");
                    await connection.Close();

                    var hooks = subContext.GetSharedData<List<string>>("HooksCalled");
                    Assert.IsGreaterThanOrEqualTo(hooks.Count, 4, $"Expected at least 4 hooks, got {hooks.Count}");
                    Assert.Contains("PreConnect", hooks);
                    Assert.Contains("PostConnect", hooks);
                    Assert.Contains("OnDisconnect", hooks);
                    
                    subContext.Comment($"All hooks called successfully: {string.Join(", ", hooks)}");
                });
            })
            .Run();
    }
}
