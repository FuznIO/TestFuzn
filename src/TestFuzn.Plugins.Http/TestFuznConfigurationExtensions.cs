using Fuzn.TestFuzn.Plugins.Http.Internals;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Extension methods for <see cref="TestFuznConfiguration"/> to configure the HTTP plugin.
/// </summary>
public static class TestFuznConfigurationExtensions
{
    /// <summary>
    /// Enables the HTTP plugin for TestFuzn with optional configuration.
    /// </summary>
    /// <param name="configuration">The TestFuzn configuration instance.</param>
    /// <param name="configureAction">An optional action to configure the HTTP plugin settings.</param>
    public static void UseHttp(this TestFuznConfiguration configuration, Action<PluginConfiguration>? configureAction = null)
    {
        var httpConfiguration = new PluginConfiguration();
        if (configureAction != null)
            configureAction(httpConfiguration);

        HttpGlobalState.Configuration = httpConfiguration;
        HttpGlobalState.HasBeenInitialized = true;

        configuration.AddContextPlugin(new HttpPlugin());
    }
}
