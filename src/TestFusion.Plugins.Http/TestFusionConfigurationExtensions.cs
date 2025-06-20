using TestFusion.Plugins.Http.Internals;

namespace TestFusion.Plugins.Http;

public static class TestFusionConfigurationExtensions
{
    public static void UseHttp(this TestFusionConfiguration configuration, Action<PluginConfiguration> configureAction = null)
    {
        var httpConfiguration = new PluginConfiguration();
        if (configureAction != null)
            configureAction(httpConfiguration);

        GlobalState.Configuration = httpConfiguration;
        GlobalState.HasBeenInitialized = true;

        configuration.AddContextPlugin(new HttpPlugin());
    }
}
