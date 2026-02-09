using Fuzn.TestFuzn.Plugins.Http.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Extension methods for <see cref="TestFuznConfiguration"/> to configure the HTTP plugin.
/// </summary>
public static class TestFuznConfigurationExtensions
{
    /// <summary>
    /// Enables the HTTP plugin for TestFuzn with optional configuration.
    /// Adding custom HttpClient must be done within section to ensure that the logging handler is properly registered and applied to all HttpClient instances.
    /// </summary>
    /// <param name="configuration">The TestFuzn configuration instance.</param>
    /// <param name="configureAction">An optional action to configure the HTTP plugin settings.</param>
    public static void UseHttp(this TestFuznConfiguration configuration, Action<PluginConfiguration>? configureAction = null)
    {
        var httpConfiguration = new PluginConfiguration();
        httpConfiguration.Services = configuration.Services;

        // Ensure that the logging handler is added to all HttpClient instances by default
        configuration.Services.AddTransient<TestFuznLoggingHandler>();
        configuration.Services.ConfigureHttpClientDefaults(configure =>
        {
            configure.AddHttpMessageHandler<TestFuznLoggingHandler>();
        });

        // Apply user-provided configuration if available
        configureAction?.Invoke(httpConfiguration);

        if (httpConfiguration.DefaultHttpClient == typeof(TestFuznHttpClient))
        {
            // Register the default HttpClient with the specified configuration
            configuration.Services.AddHttpClient<TestFuznHttpClient>(client =>
                {
                    if (httpConfiguration.DefaultBaseAddress != null)
                        client.BaseAddress = httpConfiguration.DefaultBaseAddress;
                    client.Timeout = httpConfiguration.DefaultRequestTimeout;
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = httpConfiguration.DefaultAllowAutoRedirect
                });
        }

        HttpGlobalState.Configuration = httpConfiguration;
        HttpGlobalState.HasBeenInitialized = true;

        configuration.AddContextPlugin(new HttpPlugin());
    }
}
