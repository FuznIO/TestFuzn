using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Playwright;
using Microsoft.Playwright;

namespace SampleApp.Tests;

[TestClass]
public class LoginUITests : Test
{
    [Test]
    public async Task Verify_that_login_works()
    {
        IPage page = null;

        await Scenario()
            .Step("Enter login data", async (context) =>
            {
                page = await context.CreateBrowserPage();
                await page.GotoAsync("https://localhost:44316/");
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter username" }).ClickAsync();
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter username" }).FillAsync("admin");
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).ClickAsync();
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).FillAsync("admin123");
                await page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            })
            .Step("Verify login success", async (context) =>
            {
                await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Welcome, Administrator!");
            })
            .Run();
    }

    [Test]
    public async Task Verify_that_login_works_load()
    {
        await Scenario()
            .InputData(new LoginInfo("Administrator", "admin", "admin123"),
                        new LoginInfo("Demo User", "demo", "demo"))
            .Step("Enter login data", async (context) =>
            {
                var loginInfo = context.InputData<LoginInfo>();

                var page = await context.CreateBrowserPage();
                await page.GotoAsync("https://localhost:44316/");
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter username" }).ClickAsync();
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter username" }).FillAsync(loginInfo.Username);
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).ClickAsync();
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).FillAsync(loginInfo.Password);
                await page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
                context.SetSharedData("page", page);
            })
            .Step("Verify login success", async (context) =>
            {
                var page = context.GetSharedData<IPage>("page");
                var loginInfo = context.InputData<LoginInfo>();

                await Assertions.Expect(page.Locator("h1")).ToContainTextAsync($"Welcome, {loginInfo.Name}!");
            })
            .Run();
    }

    private record LoginInfo(string Name, string Username, string Password);
}