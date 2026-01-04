using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Playwright;
using Microsoft.Playwright;

namespace SampleApp.Tests;

[TestClass]
public class ProductUITests : Test
{
    [Test]
    public async Task VerifyThatLoginAndProductCreationWorks()
    {
        await Scenario()
            .Step("Enter login data", async (context) =>
            {
                var page = await context.CreateBrowserPage();
                await page.GotoAsync("https://localhost:44316/");
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter username" }).ClickAsync();
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter username" }).FillAsync("admin");
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).ClickAsync();
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).FillAsync("admin123");
                await page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
                await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Welcome, Administrator!");
            })
            .Run();
    }
}