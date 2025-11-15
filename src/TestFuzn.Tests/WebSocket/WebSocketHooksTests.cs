using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[FeatureTest]
public class WebSocketHooksTests : BaseFeatureTest
{
    public override string FeatureName => "WebSocket Hooks";

    private const string WebSocketServerUrl = "ws://localhost:5131/ws";

    [ScenarioTest]
    public async Task PreConnect_Hook_Is_Called()
    {
        await Scenario()
            .Step("Verify PreConnect hook is invoked", async (context) =>
            {
                bool preConnectCalled = false;

                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .OnPreConnect((conn) => preConnectCalled = true)
                    .Connect();

                Assert.IsTrue(preConnectCalled, "PreConnect hook should have been called");

                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task PostConnect_Hook_Is_Called()
    {
        await Scenario()
            .Step("Verify PostConnect hook is invoked", async (context) =>
            {
                bool postConnectCalled = false;

                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .OnPostConnect((conn) => postConnectCalled = true)
                    .Connect();

                Assert.IsTrue(postConnectCalled, "PostConnect hook should have been called");

                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task OnMessageReceived_Hook_Is_Called()
    {
        await Scenario()
            .Step("Verify OnMessageReceived hook is invoked", async (context) =>
            {
                bool messageReceivedHookCalled = false;
                string receivedMessageFromHook = null;

                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .OnMessageReceived((conn, msg) =>
                    {
                        messageReceivedHookCalled = true;
                        receivedMessageFromHook = msg;
                    })
                    .Connect();

                var testMessage = "Test message for hook";
                await connection.SendText(testMessage);

                // Wait for echo
                await Task.Delay(TimeSpan.FromSeconds(1));

                Assert.IsTrue(messageReceivedHookCalled, "OnMessageReceived hook should have been called");
                Assert.AreEqual(testMessage, receivedMessageFromHook, "Hook should receive the correct message");

                await connection.Close();
            })
            .Run();
    }

    [ScenarioTest]
    public async Task OnDisconnect_Hook_Is_Called()
    {
        await Scenario()
            .Step("Verify OnDisconnect hook is invoked", async (context) =>
            {
                bool disconnectCalled = false;

                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .OnDisconnect((conn) => disconnectCalled = true)
                    .Connect();

                await connection.Close();

                Assert.IsTrue(disconnectCalled, "OnDisconnect hook should have been called");
            })
            .Run();
    }

    [ScenarioTest]
    public async Task All_Hooks_Work_Together()
    {
        await Scenario()
            .Step("Verify all hooks work in sequence", async (context) =>
            {
                var hooksCalled = new List<string>();

                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .OnPreConnect((conn) => hooksCalled.Add("PreConnect"))
                    .OnPostConnect((conn) => hooksCalled.Add("PostConnect"))
                    .OnMessageReceived((conn, msg) => hooksCalled.Add("OnMessageReceived"))
                    .OnDisconnect((conn) => hooksCalled.Add("OnDisconnect"))
                    .Connect();

                await connection.SendText("Test");
                await Task.Delay(TimeSpan.FromMilliseconds(500));

                await connection.Close();

                Assert.AreEqual(4, hooksCalled.Count, "All 4 hooks should have been called");
                Assert.AreEqual("PreConnect", hooksCalled[0]);
                Assert.AreEqual("PostConnect", hooksCalled[1]);
                Assert.AreEqual("OnMessageReceived", hooksCalled[2]);
                Assert.AreEqual("OnDisconnect", hooksCalled[3]);
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Hooks_Can_Access_Connection_State()
    {
        await Scenario()
            .Step("Verify hooks can access connection state", async (context) =>
            {
                bool isConnectedInPostConnect = false;
                bool isConnectedInDisconnect = true;

                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .OnPostConnect((conn) => isConnectedInPostConnect = conn.IsConnected)
                    .OnDisconnect((conn) => isConnectedInDisconnect = conn.IsConnected)
                    .Connect();

                await connection.Close();

                Assert.IsTrue(isConnectedInPostConnect, "Connection should be open in PostConnect");
                Assert.IsFalse(isConnectedInDisconnect, "Connection should be closed in OnDisconnect");
            })
            .Run();
    }

    [ScenarioTest]
    public async Task Hook_Can_Store_Context_Data()
    {
        await Scenario()
            .Step("Use hooks to track connection lifecycle in context", async (context) =>
            {
                var connection = await context.CreateWebSocketConnection(WebSocketServerUrl)
                    .OnPreConnect((conn) => context.SetSharedData("PreConnectTime", DateTime.UtcNow))
                    .OnPostConnect((conn) => context.SetSharedData("PostConnectTime", DateTime.UtcNow))
                    .OnMessageReceived((conn, msg) => context.SetSharedData("MessageCount", 
                        context.GetSharedData<int>("MessageCount") + 1))
                    .OnDisconnect((conn) => context.SetSharedData("DisconnectTime", DateTime.UtcNow))
                    .Connect();

                context.SetSharedData("MessageCount", 0);

                await connection.SendText("Message 1");
                await connection.SendText("Message 2");
                await Task.Delay(TimeSpan.FromSeconds(1));

                await connection.Close();

                Assert.IsTrue(context.GetSharedData<DateTime>("PreConnectTime") > DateTime.MinValue);
                Assert.IsTrue(context.GetSharedData<DateTime>("PostConnectTime") > DateTime.MinValue);
                Assert.AreEqual(2, context.GetSharedData<int>("MessageCount"));
                Assert.IsTrue(context.GetSharedData<DateTime>("DisconnectTime") > DateTime.MinValue);
            })
            .Run();
    }
}
