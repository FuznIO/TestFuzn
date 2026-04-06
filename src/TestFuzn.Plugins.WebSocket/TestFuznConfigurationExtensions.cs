using Fuzn.TestFuzn.Plugins.WebSocket.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Plugins.WebSocket;

/// <summary>
/// Extension methods for configuring the WebSocket plugin in TestFuzn.
/// </summary>
public static class TestFuznConfigurationExtensions
{
    /// <summary>
    /// Enables WebSocket testing support in TestFuzn.
    /// </summary>
    /// <param name="configuration">The TestFuzn configuration instance.</param>
    /// <param name="configureAction">Optional action to configure WebSocket-specific settings.</param>
    /// <example>
    /// <code>
    /// configuration.UseWebSocket(config =>
    /// {
    ///     config.DefaultConnectionTimeout = TimeSpan.FromSeconds(15);
    ///     config.DefaultKeepAliveInterval = TimeSpan.FromSeconds(60);
    ///     config.LogFailedConnectionsToTestConsole = true;
    /// });
    /// </code>
    /// </example>
    public static void UseWebSocket(this TestFuznConfiguration configuration, Action<PluginConfiguration>? configureAction = null)
    {
        var webSocketConfiguration = new PluginConfiguration();
        configureAction?.Invoke(webSocketConfiguration);

        // Validate configuration
        if (webSocketConfiguration.DefaultConnectionTimeout <= TimeSpan.Zero)
            throw new ArgumentException("DefaultConnectionTimeout must be greater than zero.", nameof(configureAction));

        if (webSocketConfiguration.DefaultKeepAliveInterval < TimeSpan.Zero)
            throw new ArgumentException("DefaultKeepAliveInterval must be zero or greater.", nameof(configureAction));

        if (webSocketConfiguration.ReceiveBufferSize <= 0)
            throw new ArgumentException("ReceiveBufferSize must be greater than zero.", nameof(configureAction));

        if (webSocketConfiguration.MaxMessageSize < 0)
            throw new ArgumentException("MaxMessageSize must be zero or greater.", nameof(configureAction));

        var globalState = new WebSocketGlobalState
        {
            Configuration = webSocketConfiguration,
            HasBeenInitialized = true
        };
        configuration.Services.AddSingleton(globalState);

        configuration.AddContextPlugin(new WebSocketPlugin());
    }
}
