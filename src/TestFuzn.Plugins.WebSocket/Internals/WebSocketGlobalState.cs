namespace Fuzn.TestFuzn.Plugins.WebSocket.Internals;

internal class WebSocketGlobalState
{
    public PluginConfiguration Configuration { get; set; } = null!;
    public bool HasBeenInitialized { get; set; } = false;
}
