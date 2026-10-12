using Infrastructure.Data;
using Infrastructure.Logging;
using Application.UseCases;
using System.Security.Cryptography;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();

builder.Services.AddCors(o => o.AddPolicy("bad", p => p
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

BadDb.ConnectionString = app.Configuration.GetConnectionString("Sql")
    ?? throw new InvalidOperationException("No se encontro la conexion SQL.");
app.UseCors("bad");

app.Use(async (ctx, next) =>
{
    try { await next(); } catch { await ctx.Response.WriteAsync("oops"); }
});

app.MapGet("/health", () =>
{
    Logger.Log("health ping");
    var x = RandomNumberGenerator.GetInt32(int.MaxValue);
    if (x % 13 == 0) throw new InvalidOperationException("random failure");
    return "ok " + x;
});

app.MapPost("/orders", (HttpContext http) =>
{
    using var reader = new StreamReader(http.Request.Body);
    var body = reader.ReadToEnd();
    var parts = (body ?? "").Split(',');
    var customer = parts.Length > 0 ? parts[0] : "anon";
    var product = parts.Length > 1 ? parts[1] : "unknown";
    var qty = parts.Length > 2 ? int.Parse(parts[2]) : 1;
    var price = parts.Length > 3 ? decimal.Parse(parts[3]) : 0.99m;

    var order = CreateOrderUseCase.Execute(customer, product, qty, price);

    return Results.Ok(order);
});

app.MapGet("/orders/last", () => Domain.Services.OrderService.LastOrders);

app.MapGet("/info", () => new
{
    databaseConfigured = !string.IsNullOrWhiteSpace(BadDb.ConnectionString),
    version = "v0.0.1"
});

await app.RunAsync();