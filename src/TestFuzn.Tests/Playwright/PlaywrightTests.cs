using Fuzn.TestFuzn.Plugins.Playwright;

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
                await page.GotoAsync("https://localhost:44316/swagger/index.html");

                var title = await page.TitleAsync();

                Assert.AreEqual("Swagger UI", title);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(10, stats.Ok.RequestCount);
            })
            .Run();
    }
}
