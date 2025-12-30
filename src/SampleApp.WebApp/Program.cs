using Microsoft.AspNetCore.Authentication.Cookies;
using SampleApp.WebApp.Models;
using SampleApp.WebApp.Services;
using SampleApp.WebApp.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Prevent connection rate limits (Kestrel)
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxConcurrentConnections = null;
    o.Limits.MaxConcurrentUpgradedConnections = null; // WebSockets
    o.Limits.MaxRequestBodySize = null;
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
    });

// App Services
builder.Services.AddTransient<ProductService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<WebSocketHandler>();

var app = builder.Build();

// Seed initial products (only if empty to avoid duplicates on hot reload, etc.)
var productService = app.Services.GetRequiredService<ProductService>();
var existing = await productService.GetAllProducts();
if (existing.Count == 0)
{
    var initialProducts = new[]
    {
        new Product { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Name = "Alpha",   Price = 10.99m },
        new Product { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Name = "Bravo",   Price = 15.50m },
        new Product { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), Name = "Charlie", Price = 8.25m },
        new Product { Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Name = "Delta",   Price = 22.10m },
        new Product { Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), Name = "Echo",    Price = 5.75m }
    };

    foreach (var p in initialProducts)
        productService.AddProduct(p);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable WebSockets
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
    ReceiveBufferSize = 4 * 1024 // tune as needed
});

// Generic echo endpoint (already present but ensure it stays minimal)
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
    await handler.HandleWebSocketConnection(context, socket);
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
