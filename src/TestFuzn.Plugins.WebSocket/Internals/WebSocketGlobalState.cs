namespace Fuzn.TestFuzn.Plugins.WebSocket.Internals;

internal class WebSocketGlobalState
{
    private static PluginConfiguration? _configuration;

    public static PluginConfiguration Configuration
    {
        get => _configuration ?? throw new InvalidOperationException(
            "WebSocket plugin has not been initialized. Please call configuration.UseWebSocket() in your test startup.");
        set => _configuration = value;
    }

    public static bool HasBeenInitialized { get; set; } = false;
}
