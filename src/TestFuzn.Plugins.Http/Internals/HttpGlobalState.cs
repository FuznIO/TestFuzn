namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpGlobalState
{
    public static HttpPluginConfiguration Configuration { get; set; } = null!;
    public static bool HasBeenInitialized { get; set; } = false;
}
