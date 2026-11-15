using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Admin;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using OneCart.Api.Orders;

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
builder.Services.AddScoped<StorefrontSettingsService>();
builder.Services.AddScoped<IAdminEmailSender, SmtpAdminEmailSender>();
builder.Services.AddScoped<AdminSecuritySettingsService>();
builder.Services.AddScoped<ProductMediaService>();
builder.Services.AddScoped<CartQuoteService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<OrderManagementService>();
var dataProtectionKeyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(dataProtectionKeyDirectory))
{
    dataProtectionKeyDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "data-protection-keys");
}

var dataProtection = builder.Services.AddDataProtection();
if (!string.IsNullOrWhiteSpace(dataProtectionKeyDirectory))
{
    var fullKeyDirectory = Path.IsPathRooted(dataProtectionKeyDirectory)
        ? dataProtectionKeyDirectory
        : Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeyDirectory);
    Directory.CreateDirectory(fullKeyDirectory);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(fullKeyDirectory)));
    if (OperatingSystem.IsWindows())
    {
        dataProtection.ProtectKeysWithDpapi();
    }
}

builder.Services.AddSingleton(services => services.GetRequiredService<IDataProtectionProvider>()
    .CreateProtector("OneCart.CartQuote.v1"));
builder.Services.AddSingleton<IProductImageStorage>(services =>
{
    var environment = services.GetRequiredService<IWebHostEnvironment>();
    var configuredPath = builder.Configuration["ProductMedia:RootPath"] ?? Path.Combine("App_Data", "product-images");
    var rootPath = Path.IsPathRooted(configuredPath)
        ? configuredPath
        : Path.Combine(environment.ContentRootPath, configuredPath);
    return new LocalProductImageStorage(Path.GetFullPath(rootPath));
});
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
    options.Events.OnRedirectToLogin = ApiCookieEvents.RedirectToUnauthorized;
    options.Events.OnRedirectToAccessDenied = ApiCookieEvents.RedirectToForbidden;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AdminPasswordReset", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
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

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
    await database.Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseRouting();
app.UseRateLimiter();

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
    string? language,
    StorefrontCatalogService catalog,
    CancellationToken cancellationToken) =>
{
    var selectedLanguage = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
    if (!OneCart.Api.Catalog.CatalogTranslations.Languages.Contains(selectedLanguage, StringComparer.Ordinal))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["language"] = ["Choose en, bn, or es."] });
    var product = await catalog.GetPublishedProductAsync(selectedLanguage, cancellationToken);
    return product is null ? Results.NotFound() : Results.Ok(product);
});
app.MapGet("/api/v1/store/product/images/{imageId:guid}", async (
    Guid imageId,
    ProductMediaService media,
    CancellationToken cancellationToken) =>
{
    var image = await media.OpenPublishedImageAsync(imageId, cancellationToken);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Content, image.ContentType, enableRangeProcessing: false);
});
app.MapAdminEndpoints();
app.MapOrderEndpoints();
app.MapAdminCustomerEndpoints();

if (args.Length == 2 && args[0] == "--bootstrap-system-admin")
{
    Environment.ExitCode = await AdminBootstrapper.CreateFirstSystemAdminAsync(app.Services, args[1]);
    return;
}

app.Run();
