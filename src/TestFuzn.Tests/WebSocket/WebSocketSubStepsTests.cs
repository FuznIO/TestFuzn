using Fuzn.FluentWebSocket;
using Fuzn.TestFuzn.Plugins.WebSocket;

namespace Fuzn.TestFuzn.Tests.WebSocket;

[TestClass]
public class WebSocketSubStepsTests : Test
{
    private const string WebSocketServerUrl = "wss://localhost:7058/ws";

    [Test]
    public async Task WebSocket_With_Nested_Steps()
    {
        await Scenario()
            .Step("WebSocket scenario with sub-steps", async (context) =>
            {
                await context.Step("Connect to server", async (subContext) =>
                {
                    var connection = await subContext.CreateWebSocketConnection(WebSocketServerUrl);

                    Assert.IsTrue(connection.IsConnected);
                    subContext.SetSharedData("Connection", connection);
                });

                await context.Step("Send and receive message", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<FluentWebSocketConnection>("Connection");

                    await connection.SendText("Hello from sub-step");
                    var response = await connection.Receive(TimeSpan.FromSeconds(5));
                    Assert.AreEqual("Hello from sub-step", response.Text);
                });

                await context.Step("Close connection", async (subContext) =>
                {
                    var connection = subContext.GetSharedData<FluentWebSocketConnection>("Connection");
                    await connection.Close();
                    Assert.IsFalse(connection.IsConnected);
                });
            })
            .Run();
    }
}
