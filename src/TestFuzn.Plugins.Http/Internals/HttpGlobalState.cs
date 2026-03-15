namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpGlobalState
{
    public HttpPluginConfiguration Configuration { get; set; } = null!;
    public bool HasBeenInitialized { get; set; } = false;
}
