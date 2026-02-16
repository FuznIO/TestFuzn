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
    /// </summary>
    /// <param name="configuration">The TestFuzn configuration instance.</param>
    /// <param name="configureAction">An optional action to configure the HTTP plugin settings.</param>
    /// <remarks>
    /// <para>
    /// After calling this method, you must register at least one HTTP client using 
    /// <c>services.AddHttpClient&lt;THttpClient&gt;()</c> to enable HTTP testing.
    /// The TestFuzn handlers are automatically added to all HTTP clients.
    /// </para>
    /// <para>
    /// To use the parameterless <c>context.CreateHttpRequest(url)</c> overload, you must also call
    /// <c>httpConfig.DefaultHttpClient&lt;THttpClient&gt;()</c> to specify which client to use by default.
    /// </para>
    /// </remarks>
    public static void UseHttp(this TestFuznConfiguration configuration, Action<HttpPluginConfiguration>? configureAction = null)
    {
        var httpConfiguration = new HttpPluginConfiguration();
        httpConfiguration.Services = configuration.Services;

        // Register the logging handler
        configuration.Services.AddTransient<TestFuznLoggingHandler>();

        // Automatically add the TestFuzn handler to all HTTP clients
        configuration.Services.ConfigureHttpClientDefaults(builder =>
        {
            builder.AddHttpMessageHandler<TestFuznLoggingHandler>();
        });

        // Apply user-provided configuration if available
        configureAction?.Invoke(httpConfiguration);

        HttpGlobalState.Configuration = httpConfiguration;
        HttpGlobalState.HasBeenInitialized = true;

        configuration.AddContextPlugin(new HttpPlugin());
    }
}
