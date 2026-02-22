# Web UI Testing with Playwright

TestFuzn integrates with Microsoft Playwright for browser automation.

---

## Configuration

```csharp
public void Configure(TestFuznConfiguration configuration)
{
    configuration.UsePlaywright(c =>
    {
        c.BrowserTypes = new List<string> { "chromium" };
        c.ConfigureBrowserLaunchOptions = (browserType, launchOptions) =>
        {
            launchOptions.Headless = false; // Set to true for CI/CD
        };

        // Optional: configure default context options for all pages
        c.ConfigureBrowserContextOptions = (browserType, options) =>
        {
            options.Locale = "en-US";
        };
    });
}
```

---

## Basic Browser Testing

```csharp
using Fuzn.TestFuzn.Plugins.Playwright;
using Microsoft.Playwright;

[Test]
public async Task Verify_login_flow()
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
```

---

## Device Emulation

Use the `device` parameter to emulate a mobile or tablet device. The device name maps to Playwright's built-in [device descriptors](https://playwright.dev/dotnet/docs/emulation#devices) (e.g. `"iPhone 13"`, `"Pixel 5"`, `"iPad Pro 11"`).

```csharp
[Test]
public async Task Verify_mobile_layout()
{
    await Scenario()
        .Step("Open page as iPhone 13", async (context) =>
        {
            var page = await context.CreateBrowserPage(device: "iPhone 13");
            await page.GotoAsync("https://localhost:44316/");

            // The page now has iPhone 13 viewport, user agent, touch support, etc.
            await Assertions.Expect(page.Locator(".mobile-menu")).ToBeVisibleAsync();
        })
        .Run();
}
```

You can combine device emulation with per-call overrides:

```csharp
var page = await context.CreateBrowserPage(
    device: "iPhone 13",
    configureBrowserContext: options => options.Locale = "fr-FR"
);
```

---

## Custom Context Options

Use the `configureBrowserContext` parameter to customize the browser context for a specific page:

```csharp
[Test]
public async Task Verify_custom_viewport()
{
    await Scenario()
        .Step("Open page with custom viewport", async (context) =>
        {
            var page = await context.CreateBrowserPage(configureBrowserContext: options =>
            {
                options.ViewportSize = new ViewportSize { Width = 1920, Height = 1080 };
                options.Locale = "en-US";
            });
            await page.GotoAsync("https://localhost:44316/");
        })
        .Run();
}
```

### Options layering

When multiple option sources are used, they are applied in this order (later overrides earlier):

1. **Device defaults** — if `device` is specified, its descriptor provides the base options
2. **Global `ConfigureBrowserContextOptions`** — from `PluginConfiguration`, applied to all pages
3. **Per-call `configureBrowserContext`** — the callback passed to `CreateBrowserPage`

---

## CreateBrowserPage Signature

```csharp
Task<IPage> CreateBrowserPage(
    string? browserType = null,        // defaults to first configured browser
    string? device = null,             // Playwright device descriptor name
    Action<BrowserNewContextOptions>? configureBrowserContext = null  // per-call overrides
)
```

All parameters are optional and named. Each call creates a new isolated browser context and page.

---

## UI Load Testing

```csharp
[Test]
public async Task UI_load_test()
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
```

---

[← Back to Table of Contents](README.md)
