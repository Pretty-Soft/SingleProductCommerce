using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Catalog;
using OneCart.Api.Data;

var builder = WebApplication.CreateBuilder(args);
var storeConnectionString = builder.Configuration.GetConnectionString("Store")
    ?? throw new InvalidOperationException(
        "Configure ConnectionStrings:Store with the PostgreSQL connection string.");

builder.Services.AddDbContext<StoreDbContext>(options =>
    options.UseNpgsql(storeConnectionString));
builder.Services.AddScoped<StorefrontCatalogService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    var knownProxies = builder.Configuration
        .GetSection("ForwardedHeaders:KnownProxies")
        .Get<string[]>() ?? [];

    foreach (var proxy in knownProxies)
    {
        if (!IPAddress.TryParse(proxy, out var address))
        {
            throw new InvalidOperationException(
                $"'{proxy}' is not a valid IP address in ForwardedHeaders:KnownProxies.");
        }

        options.KnownProxies.Add(address);
    }
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "https_required",
            message = "HTTPS is required for all API traffic."
        });
        return;
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "OneCart.Api"
}));

app.MapGet("/api/v1/store/product", async (
    StorefrontCatalogService catalog,
    CancellationToken cancellationToken) =>
{
    var product = await catalog.GetPublishedProductAsync(cancellationToken);
    return product is null ? Results.NotFound() : Results.Ok(product);
});

app.Run();
