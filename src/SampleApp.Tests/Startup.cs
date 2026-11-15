using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using Fuzn.TestFuzn.Plugins.Playwright;
using Microsoft.Extensions.DependencyInjection;

namespace SampleApp.Tests;

public class Startup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.UseHttp(httpConfig =>
        {
            httpConfig.Services.AddHttpClient<SampleAppHttpClient>();

            httpConfig.DefaultHttpClient<SampleAppHttpClient>();
        });
        configuration.UsePlaywright(c =>
        {
            c.BrowserTypes = new List<string> { "chromium" };
            c.ConfigureBrowserLaunchOptions = (browserType, launchOptions) =>
            {
                launchOptions.Timeout = 5000;
                launchOptions.Headless = true;
            };
        });
    }
}
