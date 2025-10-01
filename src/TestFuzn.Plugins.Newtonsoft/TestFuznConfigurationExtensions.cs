using Fuzn.TestFuzn.Plugins.Newtonsoft.Internals;

namespace Fuzn.TestFuzn.Plugins.Newtonsoft;

public static class TestFuznConfigurationExtensions
{
    public static void UseNewtonsoftSerializer(this TestFuznConfiguration configuration, Action<PluginConfiguration>? pluginConfigurationAction = null)
    {
        var pluginConfiguration = new PluginConfiguration();
        pluginConfigurationAction?.Invoke(pluginConfiguration);
        configuration.AddSerializerProvider(new NewtonsoftSerializerProvider(pluginConfiguration));
    }
}
