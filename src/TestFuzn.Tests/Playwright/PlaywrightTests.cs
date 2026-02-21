using Fuzn.TestFuzn.Plugins.Playwright;
using Microsoft.Playwright;

namespace Fuzn.TestFuzn.Tests.Playwright;

[TestClass]
public class PlaywrightTests : Test
{
    [Test]
    public async Task Verify_that_playwright_works()
    {
        await Scenario("Test UI using BrowserPage that wraps Playwright")
            .Step("Open Swagger page and verify title", async (context) =>
            {
                var page = await context.CreateBrowserPage();
                await page.GotoAsync("https://localhost:7058/");

                var title = await page.TitleAsync();

                Assert.AreEqual("Login - TestWebApp", title);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(10, stats.Ok.RequestCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_device_emulation()
    {
        await Scenario("Test UI with device emulation")
            .Step("Open page as iPhone 13", async (context) =>
            {
                var page = await context.CreateBrowserPage(device: "iPhone 13");
                await page.GotoAsync("https://localhost:7058/");

                var title = await page.TitleAsync();
                Assert.AreEqual("Login - TestWebApp", title);
            })
            .Run();
    }

    [Test]
    public async Task Verify_per_call_context_options()
    {
        await Scenario("Test UI with per-call context options")
            .Step("Open page with custom viewport", async (context) =>
            {
                var page = await context.CreateBrowserPage(configureContext: options =>
                {
                    options.ViewportSize = new ViewportSize { Width = 1920, Height = 1080 };
                    options.Locale = "en-US";
                });
                await page.GotoAsync("https://localhost:7058/");

                var title = await page.TitleAsync();
                Assert.AreEqual("Login - TestWebApp", title);
            })
            .Run();
    }

    [Test]
    public async Task Verify_browser_context_access()
    {
        await Scenario("Test UI with browser context access")
            .Step("Open page and access browser context for cookies", async (context) =>
            {
                var page = await context.CreateBrowserPage();
                await page.Context.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Name = "test-cookie",
                        Value = "test-value",
                        Domain = "localhost",
                        Path = "/"
                    }
                });
                await page.GotoAsync("https://localhost:7058/");

                var title = await page.TitleAsync();
                Assert.AreEqual("Login - TestWebApp", title);
            })
            .Run();
    }

    [Test]
    public async Task Verify_load_test_with_different_devices()
    {
        var devices = new[] { "iPhone 13", "Pixel 5", "iPad Pro 11" };

        await Scenario("Load test with device emulation")
            .InputData(devices)
            .Step("Open page on device", async (context) =>
            {
                var page = await context.CreateBrowserPage(device: context.InputData<string>());
                await page.GotoAsync("https://localhost:7058/");

                var title = await page.TitleAsync();
                Assert.AreEqual("Login - TestWebApp", title);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(9))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(9, stats.Ok.RequestCount);
            })
            .Run();
    }
}
