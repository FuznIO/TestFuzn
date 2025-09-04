using Fuzn.TestFuzn.Plugins.Newtonsoft.Internals;

namespace Fuzn.TestFuzn.Plugins.Newtonsoft;

public static class TestFusionConfigurationExtensions
{
    public static void UseNewtonsoftSerializer(this TestFusionConfiguration configuration, Action<PluginConfiguration>? pluginConfigurationAction = null)
    {
        var pluginConfiguration = new PluginConfiguration();
        pluginConfigurationAction?.Invoke(pluginConfiguration);
        configuration.AddSerializerProvider(new NewtonsoftSerializerProvider(pluginConfiguration));
    }
}
