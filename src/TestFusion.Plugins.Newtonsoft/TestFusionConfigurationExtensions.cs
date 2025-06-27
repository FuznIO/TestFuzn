using TestFusion.Plugins.Newtonsoft.Internals;

namespace TestFusion.Plugins.Newtonsoft;

public static class TestFusionConfigurationExtensions
{
    public static void UseNewtonsoftSerializer(this TestFusionConfiguration configuration, Action<PluginConfiguration>? configureAction = null)
    {
        var pluginConfiguration = new PluginConfiguration();
        configureAction?.Invoke(pluginConfiguration);
        configuration.AddSerializerProvider(new NewtonsoftSerializerProvider(pluginConfiguration.JsonSerializerSettings));
    }
}
