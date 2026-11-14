using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Admin;
using OneCart.Api.Catalog;
using OneCart.Api.Data;

var builder = WebApplication.CreateBuilder(args);
var storeConnectionString = builder.Configuration.GetConnectionString("Store")
    ?? throw new InvalidOperationException(
        "Configure ConnectionStrings:Store for the selected Database:Provider.");

builder.Services.AddDbContext<StoreDbContext>(options =>
    StoreDatabaseProvider.Configure(
        options,
        StoreDatabaseProvider.GetProviderName(builder.Configuration["Database:Provider"]),
        storeConnectionString));
builder.Services.AddScoped<StorefrontCatalogService>();
builder.Services.AddScoped<AdminCatalogService>();
builder.Services.AddIdentity<AdminUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<StoreDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-OneCart.Admin";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-OneCart.AntiForgery";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});

var allowedAdminOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
if (builder.Environment.IsDevelopment())
{
    allowedAdminOrigins = allowedAdminOrigins
        .Concat(["http://localhost:5173", "http://127.0.0.1:5173"])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

builder.Services.AddCors(options => options.AddPolicy("AdminUi", policy =>
{
    if (allowedAdminOrigins.Length > 0)
    {
        policy.WithOrigins(allowedAdminOrigins);
    }

    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

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

app.UseCors("AdminUi");
app.UseAuthentication();
app.UseAuthorization();

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
app.MapAdminEndpoints();

if (args.Length == 2 && args[0] == "--bootstrap-system-admin")
{
    Environment.ExitCode = await AdminBootstrapper.CreateFirstSystemAdminAsync(app.Services, args[1]);
    return;
}

app.Run();
