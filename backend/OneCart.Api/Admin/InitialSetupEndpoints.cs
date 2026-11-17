using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace OneCart.Api.Admin;

public static class InitialSetupEndpoints
{
    public static IEndpointRouteBuilder MapInitialSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/setup");

        group.MapGet("/status", async (
            IConfiguration configuration,
            InstallationConfigurationStore installation,
            IServiceScopeFactory scopeFactory,
            CancellationToken cancellationToken) =>
        {
            if (!HasConfiguredToken(configuration))
                return Results.NotFound();
            if (installation.Current is null)
                return Results.Ok(new { available = true, requiresDatabase = true });
            await using var scope = scopeFactory.CreateAsyncScope();
            var setup = scope.ServiceProvider.GetRequiredService<InitialSystemAdminSetupService>();
            return Results.Ok(new { available = await setup.IsAvailableAsync(cancellationToken), requiresDatabase = false });
        }).AllowAnonymous();

        group.MapPost("/database/test", TestDatabaseAsync)
            .AllowAnonymous().RequireRateLimiting("InitialSystemAdminSetup");

        group.MapPost("/install", InstallAsync)
            .AllowAnonymous().RequireRateLimiting("InitialSystemAdminSetup");

        group.MapPost("/system-admin", CreateInitialSystemAdminAsync)
            .AllowAnonymous()
            .RequireRateLimiting("InitialSystemAdminSetup");

        return endpoints;
    }

    private static async Task<IResult> CreateInitialSystemAdminAsync(
        InitialSystemAdminRequest request,
        [FromHeader(Name = "X-OneCart-Setup-Token")] string? setupToken,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        HttpRequest httpRequest,
        InstallationConfigurationStore installation,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        if (!HasConfiguredToken(configuration))
            return Results.NotFound();

        if (!InitialSetupSecurity.IsAllowedOrigin(httpRequest, configuration, environment.IsDevelopment())
            || !InitialSetupSecurity.HasValidToken(configuration, setupToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length is 0 or > 254 || !new EmailAddressAttribute().IsValid(email))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Enter a valid email address."] });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 256
            || request.Password != request.ConfirmPassword)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["Enter matching passwords no longer than 256 characters."] });

        if (!installation.IsReady)
            return Results.Conflict(new { error = "Configure the database through the first-run installer." });
        await using var scope = scopeFactory.CreateAsyncScope();
        var setup = scope.ServiceProvider.GetRequiredService<InitialSystemAdminSetupService>();
        var result = await setup.CreateAsync(email, request.Password, cancellationToken);
        if (result.AlreadyComplete)
            return Results.Conflict(new { error = "Initial setup is already complete. Sign in through the administrator page." });
        if (!result.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["setup"] = result.Errors.ToArray() });

        return Results.Created("/admin", new { email, role = AdminRoles.SystemAdmin });
    }

    private static async Task<IResult> TestDatabaseAsync(
        DatabaseSetupRequest request,
        [FromHeader(Name = "X-OneCart-Setup-Token")] string? setupToken,
        IConfiguration configuration, IWebHostEnvironment environment,
        HttpRequest httpRequest, InstallationConfigurationStore installation,
        FirstRunInstallationService setup, CancellationToken cancellationToken)
    {
        if (!HasConfiguredToken(configuration)) return Results.NotFound();
        if (!InitialSetupSecurity.IsAllowedOrigin(httpRequest, configuration, environment.IsDevelopment())
            || !InitialSetupSecurity.HasValidToken(configuration, setupToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (installation.Current is not null) return Results.Conflict(new { error = "Database installation is already configured." });
        if (!FirstRunInstallationService.TryBuildDatabase(request, out var database, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["database"] = [error!] });
        return await setup.TestConnectionAsync(database!, cancellationToken)
            ? Results.Ok(new { connected = true })
            : Results.BadRequest(new { error = "Could not connect. Check the database host, login, server certificate, and server access." });
    }

    private static async Task<IResult> InstallAsync(
        CompleteInstallationRequest request,
        [FromHeader(Name = "X-OneCart-Setup-Token")] string? setupToken,
        IConfiguration configuration, IWebHostEnvironment environment,
        HttpRequest httpRequest, FirstRunInstallationService setup,
        CancellationToken cancellationToken)
    {
        if (!HasConfiguredToken(configuration)) return Results.NotFound();
        if (!InitialSetupSecurity.IsAllowedOrigin(httpRequest, configuration, environment.IsDevelopment())
            || !InitialSetupSecurity.HasValidToken(configuration, setupToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            var result = await setup.InstallAsync(request, cancellationToken);
            if (result.AlreadyComplete) return Results.Conflict(new { error = result.Error });
            if (!result.Succeeded) return Results.ValidationProblem(new Dictionary<string, string[]> { ["setup"] = [result.Error!] });
            return Results.Created("/admin", new { email = request.Email.Trim(), role = AdminRoles.SystemAdmin });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Results.Problem("Installation could not finish. Check database permissions and the server's protected storage, then retry.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool HasConfiguredToken(IConfiguration configuration)
    {
        var tokenFile = configuration["Setup:BootstrapTokenFile"];
        if (!string.IsNullOrWhiteSpace(tokenFile))
        {
            var fullPath = Path.GetFullPath(tokenFile);
            return File.Exists(fullPath) && new FileInfo(fullPath).Length >= 32;
        }

        return EncodingLength(configuration["Setup:BootstrapToken"]) >= 32;
    }

    private static int EncodingLength(string? token) =>
        token is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(token);

    public sealed record InitialSystemAdminRequest(string Email, string Password, string ConfirmPassword);
}
