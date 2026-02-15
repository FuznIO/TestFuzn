using Fuzn.TestFuzn.Plugins.Http.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Extension methods for <see cref="IHttpClientBuilder"/> to add TestFuzn-specific handlers.
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Adds TestFuzn's HTTP handlers to the HTTP client pipeline.
    /// This includes logging, correlation ID injection, and request/response tracking for failed step diagnostics.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/> for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method must be called when registering HTTP clients for use with TestFuzn to enable:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Automatic correlation ID injection via the configured header</description></item>
    /// <item><description>Request/response logging based on verbosity settings</description></item>
    /// <item><description>HTTP details capture for failed step diagnostics</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// configuration.UseHttp(httpConfig =>
    /// {
    ///     httpConfig.Services.AddHttpClient&lt;MyHttpClient&gt;(client =>
    ///     {
    ///         client.BaseAddress = new Uri("https://api.example.com");
    ///         client.Timeout = TimeSpan.FromSeconds(30);
    ///     })
    ///     .AddTestFuznHandlers();
    ///     
    ///     httpConfig.UseDefaultHttpClient&lt;MyHttpClient&gt;();
    /// });
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddTestFuznHandlers(this IHttpClientBuilder builder)
    {
        builder.AddHttpMessageHandler<TestFuznLoggingHandler>();
        return builder;
    }
}
