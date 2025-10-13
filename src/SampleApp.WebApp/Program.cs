using Microsoft.EntityFrameworkCore;
using SampleApp.WebApp.Data;
using SampleApp.WebApp.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core + SQLite
builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseSqlite("Data Source=App_Data/products.db"));

// App Services
builder.Services.AddTransient<ProductService>();
builder.Services.AddSingleton<WebSocketHandler>();

var app = builder.Build();

// Auto-create DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable WebSockets
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// WebSocket endpoints
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
        await handler.HandleWebSocketConnection(context, webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Map("/ws/echo", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
        await handler.HandleWebSocketConnection(context, webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.UseAuthorization();

app.MapControllers();

app.Run();
