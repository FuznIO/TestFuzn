namespace TestFusion.Plugins.Http.Internals;

internal class GlobalState
{
    public static PluginConfiguration Configuration { get; set; }
    public static bool HasBeenInitialized { get; set; } = false;
}
