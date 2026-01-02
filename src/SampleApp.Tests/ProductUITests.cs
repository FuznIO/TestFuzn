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
                await Task.Delay(5000);
            })
            .Run();
    }
}


/*
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter username" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter username" }).FillAsync("admin");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).FillAsync("admin12");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).PressAsync("Shift+Home");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).FillAsync("admin123");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter password" }).PressAsync("Enter");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Welcome, Administrator!" }).ClickAsync();
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Welcome, Administrator!" }).ClickAsync();
        await Expect(Page.Locator("h1")).ToContainTextAsync("Welcome, Administrator!");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Manage Products" }).ClickAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Add New Product" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter product name" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter product name" }).FillAsync("Product123");
        await Page.GetByPlaceholder("0.00").ClickAsync();
        await Page.GetByPlaceholder("0.00").FillAsync("60");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save Product" }).ClickAsync();
        await Expect(Page.Locator("#success-message")).ToContainTextAsync("Product 'Product123' created successfully!");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Search products by name..." }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Search products by name..." }).FillAsync("Product123");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        await Expect(Page.Locator("#search-results-info")).ToContainTextAsync("Showing results for: \"Product123\" (1 product found)");
        await Page.GetByRole(AriaRole.Cell, new() { Name = "Product123" }).ClickAsync();
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Product123");
        await Expect(Page.Locator("tbody")).ToContainTextAsync("$60,00");


 */