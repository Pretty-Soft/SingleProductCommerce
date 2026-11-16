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
            InitialSystemAdminSetupService setup,
            CancellationToken cancellationToken) =>
        {
            if (!HasConfiguredToken(configuration))
                return Results.NotFound();

            return Results.Ok(new { available = await setup.IsAvailableAsync(cancellationToken) });
        }).AllowAnonymous();

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
        InitialSystemAdminSetupService setup,
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

        var result = await setup.CreateAsync(email, request.Password, cancellationToken);
        if (result.AlreadyComplete)
            return Results.Conflict(new { error = "Initial setup is already complete. Sign in through the administrator page." });
        if (!result.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["setup"] = result.Errors.ToArray() });

        return Results.Created("/admin", new { email, role = AdminRoles.SystemAdmin });
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
