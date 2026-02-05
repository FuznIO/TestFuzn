namespace Fuzn.TestFuzn.Plugins.Http;

public class HttpPluginConstants
{
    public const string DefaultHttpClientName = "TestFuzn";
    public static readonly HttpRequestOptionsKey<Context> ContextOptionName = new("TestFuznContext");
}
