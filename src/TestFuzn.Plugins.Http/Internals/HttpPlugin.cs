using Microsoft.Extensions.DependencyInjection;
using Fuzn.TestFuzn.Contracts.Plugins;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpPlugin : IContextPlugin
{
    private readonly IServiceProvider _serviceProvider;

    public HttpPlugin()
    {
        var serviceCollection = new ServiceCollection();
        
        serviceCollection.AddTransient<TestFuznLoggingHandler>();
        
        serviceCollection.AddHttpClient(HttpClientNames.TestFuzn, client =>
        {
            var timeout = HttpGlobalState.Configuration?.DefaultRequestTimeout ?? TimeSpan.FromSeconds(100);
            client.Timeout = timeout;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        })
        .AddHttpMessageHandler<TestFuznLoggingHandler>();

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    public bool RequireState => true;
    public bool RequireStepExceptionHandling => false;

    public Task InitSuite()
    {
        return Task.CompletedTask;
    }

    public async Task CleanupSuite()
    {
        await Task.CompletedTask;
    }

    public object InitContext()
    {
        return _serviceProvider.GetRequiredService<IHttpClientFactory>();
    }

    public Task CleanupContext(object state)
    {
        return Task.CompletedTask;
    }

    public Task HandleStepException(object state, IterationContext context, Exception exception)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Named HttpClient constants.
/// </summary>
internal static class HttpClientNames
{
    public const string TestFuzn = "TestFuzn";
}