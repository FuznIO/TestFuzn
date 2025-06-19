namespace TestFusion.HttpTesting.Internals;

internal class GlobalState
{
    public static HttpTestingConfiguration Configuration { get; set; }
    public static bool HasBeenInitialized { get; set; } = false;
}
