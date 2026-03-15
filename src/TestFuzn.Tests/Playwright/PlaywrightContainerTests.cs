using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Fuzn.TestFuzn.Plugins.Playwright;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Tests.Playwright;

[TestClass]
public class PlaywrightContainerTests : Test
{
    [Test]
    public async Task Verify_configuration_is_singleton()
    {
        var identities = new ConcurrentBag<int>();

        await Scenario()
            .Step("Resolve configuration across iterations", (context) =>
            {
                var config = context.ServicesProvider.GetRequiredService<PlaywrightPluginConfiguration>();
                identities.Add(RuntimeHelpers.GetHashCode(config));
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Run();

        Assert.AreEqual(1, identities.Distinct().Count());
    }

    [Test]
    public async Task Verify_playwright_instance_is_shared_across_iterations()
    {
        var identities = new ConcurrentBag<int>();

        await Scenario()
            .Step("Get Playwright instance", (context) =>
            {
                var playwright = context.GetPlaywright();
                identities.Add(RuntimeHelpers.GetHashCode(playwright));
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Run();

        Assert.AreEqual(1, identities.Distinct().Count());
    }

    [Test]
    public async Task Verify_browser_context_isolation_across_iterations()
    {
        await Scenario()
            .Step("Set unique cookie and verify no leaks from other iterations", async (context) =>
            {
                var page = await context.CreateBrowserPage();

                var cookiesBefore = await page.Context.CookiesAsync();
                Assert.IsEmpty(cookiesBefore, "Fresh browser context should have no cookies");

                var uniqueValue = Guid.NewGuid().ToString();

                await page.Context.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Name = "iteration-cookie",
                        Value = uniqueValue,
                        Domain = "localhost",
                        Path = "/"
                    }
                });

                var cookiesAfter = await page.Context.CookiesAsync();
                var iterationCookies = cookiesAfter.Where(c => c.Name == "iteration-cookie").ToList();

                Assert.HasCount(1, iterationCookies);
                Assert.AreEqual(uniqueValue, iterationCookies[0].Value);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(5, stats.Ok.RequestCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_multiple_pages_within_iteration_are_independent()
    {
        await Scenario()
            .Step("Create two pages and verify cookie isolation", async (context) =>
            {
                var page1 = await context.CreateBrowserPage();
                var page2 = await context.CreateBrowserPage();

                await page1.Context.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Name = "page1-only",
                        Value = "secret",
                        Domain = "localhost",
                        Path = "/"
                    }
                });

                var page1Cookies = await page1.Context.CookiesAsync();
                Assert.AreEqual(1, page1Cookies.Count(c => c.Name == "page1-only"), "Cookie should exist on page1 context");

                var page2Cookies = await page2.Context.CookiesAsync();
                var leaked = page2Cookies.Where(c => c.Name == "page1-only").ToList();

                Assert.AreEqual(0, leaked.Count);
            })
            .Run();
    }

    [Test]
    public async Task Verify_page_creation_works_after_prior_iteration_cleanup()
    {
        var iterationCount = 0;

        await Scenario()
            .Step("Create page and navigate", async (context) =>
            {
                var page = await context.CreateBrowserPage();
                await page.GotoAsync("https://localhost:7058/");

                var title = await page.TitleAsync();
                Assert.AreEqual("Login - TestWebApp", title);

                Interlocked.Increment(ref iterationCount);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(3))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(3, stats.Ok.RequestCount);
            })
            .Run();

        Assert.AreEqual(3, iterationCount);
    }

    [Test]
    public async Task Verify_playwright_resolves_from_context_plugins_state()
    {
        await Scenario()
            .Step("Verify plugin state is wired correctly", (context) =>
            {
                Assert.IsNotNull(context.Internals);
                Assert.IsNotNull(context.Internals.Plugins);

                var playwright = context.GetPlaywright();
                Assert.IsNotNull(playwright);
            })
            .Run();
    }
}
