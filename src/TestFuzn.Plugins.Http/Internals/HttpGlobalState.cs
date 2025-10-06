namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpGlobalState
{
    public static PluginConfiguration Configuration { get; set; }
    public static bool HasBeenInitialized { get; set; } = false;
}
