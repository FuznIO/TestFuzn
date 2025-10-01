using Fuzn.TestFuzn.Plugins.Http.Internals;

namespace Fuzn.TestFuzn.Plugins.Http;

public static class TestFuznConfigurationExtensions
{
    public static void UseHttp(this TestFuznConfiguration configuration, Action<PluginConfiguration> configureAction = null)
    {
        var httpConfiguration = new PluginConfiguration();
        if (configureAction != null)
            configureAction(httpConfiguration);

        GlobalState.Configuration = httpConfiguration;
        GlobalState.HasBeenInitialized = true;

        configuration.AddContextPlugin(new HttpPlugin());
    }
}
