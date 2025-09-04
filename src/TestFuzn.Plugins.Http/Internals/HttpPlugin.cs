using Microsoft.Extensions.DependencyInjection;
using Fuzn.TestFuzn.Contracts.Plugins;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpPlugin : IContextPlugin
{
    private readonly IServiceProvider _serviceProvider;
    public HttpPlugin()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddHttpClient("TestFuzn");
        _serviceProvider = serviceCollection.BuildServiceProvider();
    }
        
    public bool RequireState => true;
    public Task InitGlobal()
    {
        return Task.CompletedTask;
    }

    public async Task CleanupGlobal()
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
}