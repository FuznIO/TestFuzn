using Microsoft.Extensions.DependencyInjection;
using Fuzn.TestFuzn.Contracts.Plugins;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpPlugin : IContextPlugin
{
    public bool RequireState => false;
    public bool RequireStepExceptionHandling => false;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<TestFuznLoggingHandler>();

        services.AddHttpClient(HttpPluginConstants.DefaultHttpClientName, client =>
        {
            var timeout = HttpGlobalState.Configuration?.DefaultRequestTimeout ?? TimeSpan.FromSeconds(100);
            client.Timeout = timeout;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        })
        .AddHttpMessageHandler<TestFuznLoggingHandler>();
    }

    public Task InitSuite()
    {
        return Task.CompletedTask;
    }

    public Task CleanupSuite()
    {
        return Task.CompletedTask;
    }

    public object InitContext()
    {
        return null!;
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
