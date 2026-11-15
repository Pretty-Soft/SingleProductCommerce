using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OneCart.Api.Catalog;
using OneCart.Api.Data;

namespace OneCart.Api.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin");

        group.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext context) =>
            Results.Ok(new { requestToken = antiforgery.GetAndStoreTokens(context).RequestToken }));

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .AddEndpointFilter(ValidateAntiforgeryAsync);

        group.MapPost("/two-factor/login", LoginWithAuthenticatorAsync)
            .AllowAnonymous()
            .AddEndpointFilter(ValidateAntiforgeryAsync);

        group.MapPost("/password/forgot", RequestPasswordResetAsync)
            .AllowAnonymous()
            .RequireRateLimiting("AdminPasswordReset");

        group.MapPost("/password/reset", ResetPasswordAsync)
            .AllowAnonymous()
            .AddEndpointFilter(ValidateAntiforgeryAsync);

        group.MapPost("/logout", async (SignInManager<AdminUser> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return Results.NoContent();
            })
            .RequireAuthorization()
            .AddEndpointFilter(ValidateAntiforgeryAsync);

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization();

        group.MapPost("/password", ChangePasswordAsync)
            .RequireAuthorization()
            .AddEndpointFilter(ValidateAntiforgeryAsync);

        group.MapGet("/mfa/setup", GetAuthenticatorSetupAsync)
            .RequireAuthorization()
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPost("/mfa/setup", ConfirmAuthenticatorSetupAsync)
            .RequireAuthorization()
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPost("/mfa/disable", DisableAuthenticatorAsync)
            .RequireAuthorization()
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/roles", GetRolesAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/users", GetUsersAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/audit", GetAuditEventsAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPost("/users", CreateUserAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPut("/users/{userId:guid}/roles", UpdateUserRolesAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPost("/users/{userId:guid}/mfa/reset", ResetUserAuthenticatorAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/catalog/product", GetAdminProductAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.CatalogAccess))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPut("/catalog/product", SaveAdminProductAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.CatalogAccess))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPost("/catalog/product/images", UploadProductImageAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.MediaAccess))
            .WithMetadata(new RequestSizeLimitAttribute(ProductMediaService.MaximumImageBytes + 128 * 1024))
            .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = ProductMediaService.MaximumImageBytes + 128 * 1024 })
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/catalog/product/images/{imageId:guid}", OpenAdminProductImageAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.MediaAccess))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPut("/catalog/product/images/{imageId:guid}", UpdateProductImageAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.MediaAccess))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapDelete("/catalog/product/images/{imageId:guid}", DeleteProductImageAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.MediaAccess))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/storefront/settings", async (StorefrontSettingsService settings, CancellationToken cancellationToken) =>
                await settings.GetAsync(cancellationToken) is { } value ? Results.Ok(value) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin, AdminRoles.StoreManager))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPut("/storefront/settings", async (
                StorefrontSettingsRequest request,
                StorefrontSettingsService settings,
                UserManager<AdminUser> userManager,
                HttpContext context) =>
            {
                if (!Guid.TryParse(userManager.GetUserId(context.User), out var actorId)) return Results.Unauthorized();
                var result = await settings.SaveAsync(
                    request,
                    actorId,
                    userManager.GetUserName(context.User) ?? string.Empty,
                    context.RequestAborted);
                if (result.IsNotFound) return Results.NotFound();
                return result.Errors is not null ? Results.ValidationProblem(result.Errors) : Results.Ok(result.Settings);
            })
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin, AdminRoles.StoreManager))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/security/mfa-policy", async (AdminSecuritySettingsService settings, CancellationToken cancellationToken) =>
                Results.Ok(new { required = await settings.GetMfaRequiredAsync(cancellationToken) }))
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPut("/security/mfa-policy", async (
                MfaPolicyRequest request,
                AdminSecuritySettingsService settings,
                UserManager<AdminUser> userManager,
                HttpContext context) =>
            {
                if (!Guid.TryParse(userManager.GetUserId(context.User), out var actorId)) return Results.Unauthorized();
                var updated = await settings.SetMfaRequiredAsync(request.Required, actorId,
                    userManager.GetUserName(context.User) ?? string.Empty, context.RequestAborted);
                return updated ? Results.NoContent() : Results.NotFound();
            })
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        AdminLoginRequest request,
        SignInManager<AdminUser> signInManager)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || request.Email.Length > 256
            || string.IsNullOrEmpty(request.Password)
            || request.Password.Length > 1024)
        {
            return Results.BadRequest(new { error = "Email and password are required." });
        }

        var result = await signInManager.PasswordSignInAsync(
            request.Email.Trim(), request.Password, isPersistent: false, lockoutOnFailure: true);

        return result.Succeeded
            ? Results.Ok(new { signedIn = true, requiresTwoFactor = false })
            : result.RequiresTwoFactor
                ? Results.Ok(new { signedIn = false, requiresTwoFactor = true })
                : Results.Unauthorized();
    }

    private static async Task<IResult> LoginWithAuthenticatorAsync(
        AuthenticatorLoginRequest request,
        SignInManager<AdminUser> signInManager)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 32) return Results.Unauthorized();
        var code = request.Code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);
        return result.Succeeded ? Results.Ok(new { signedIn = true }) : Results.Unauthorized();
    }

    private static async Task<IResult> RequestPasswordResetAsync(
        PasswordResetRequest request,
        UserManager<AdminUser> userManager,
        IAdminEmailSender emailSender,
        IConfiguration configuration,
        ILogger<AdminEndpointsMarker> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || !new EmailAddressAttribute().IsValid(request.Email))
            return Results.BadRequest(new { error = "Enter a valid email address." });

        var baseUrl = configuration["Email:PasswordResetUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var resetBase)
            || (resetBase.Scheme != Uri.UriSchemeHttps && !string.Equals(resetBase.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
            return Results.Problem("Password reset email is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(resetBase.ToString(), new Dictionary<string, string?>
            {
                ["email"] = user.Email,
                ["token"] = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token))
            });
            try
            {
                await emailSender.SendPasswordResetAsync(user.Email!, new Uri(query), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Administrator password-reset email delivery failed.");
            }
        }

        return Results.Accepted(value: new { message = "If that administrator account exists, a reset link will be sent." });
    }

    private static async Task<IResult> ResetPasswordAsync(
        CompletePasswordResetRequest request,
        UserManager<AdminUser> userManager,
        StoreDbContext database)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256
            || string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 4096
            || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length > 256)
            return Results.BadRequest(new { error = "Enter a valid reset link and a new password." });

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null) return Results.BadRequest(new { error = "The reset link is invalid or expired." });
        string decodedToken;
        try { decodedToken = System.Text.Encoding.UTF8.GetString(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(request.Token)); }
        catch (FormatException) { return Results.BadRequest(new { error = "The reset link is invalid or expired." }); }

        var result = await userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
        if (!result.Succeeded) return IdentityValidationResult(result);
        user.MustChangePassword = false;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return IdentityValidationResult(update);
        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(), ActorUserId = user.Id, ActorEmail = user.Email ?? string.Empty,
            Action = "admin_user.password.reset", TargetId = user.Id, Target = user.Email ?? string.Empty,
            Details = "Administrator password reset using a one-time email token.",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetAuthenticatorSetupAsync(
        UserManager<AdminUser> userManager,
        HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        if (user.TwoFactorEnabled) return Results.Conflict(new { error = "Authenticator MFA is already enabled." });

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            var reset = await userManager.ResetAuthenticatorKeyAsync(user);
            if (!reset.Succeeded) return IdentityValidationResult(reset);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }
        return Results.Ok(new { authenticatorKey = FormatAuthenticatorKey(key ?? string.Empty), account = user.Email });
    }

    private static async Task<IResult> ConfirmAuthenticatorSetupAsync(
        AuthenticatorCodeRequest request,
        UserManager<AdminUser> userManager,
        SignInManager<AdminUser> signInManager,
        HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        var code = request.Code?.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(code) || !await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
            return Results.BadRequest(new { error = "The authenticator code is not valid. Check your device time and try again." });

        var enabled = await userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enabled.Succeeded) return IdentityValidationResult(enabled);
        await signInManager.RefreshSignInAsync(user);
        return Results.Ok(new { enabled = true });
    }

    private static async Task<IResult> DisableAuthenticatorAsync(
        AuthenticatorCodeRequest request,
        UserManager<AdminUser> userManager,
        SignInManager<AdminUser> signInManager,
        AdminSecuritySettingsService securitySettings,
        StoreDbContext database,
        HttpContext context)
    {
        if (await securitySettings.GetMfaRequiredAsync(context.RequestAborted))
            return Results.Conflict(new { error = "Authenticator MFA is required by the System Admin and cannot be disabled." });
        var user = await userManager.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        var code = request.Code?.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (!user.TwoFactorEnabled || string.IsNullOrWhiteSpace(code)
            || !await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
            return Results.BadRequest(new { error = "Enter a valid code from your authenticator app." });

        await using var transaction = await database.Database.BeginTransactionAsync(context.RequestAborted);
        var resetKey = await userManager.ResetAuthenticatorKeyAsync(user);
        if (!resetKey.Succeeded) return IdentityValidationResult(resetKey);
        var disabled = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disabled.Succeeded) return IdentityValidationResult(disabled);
        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(), ActorUserId = user.Id, ActorEmail = user.Email ?? string.Empty,
            Action = "admin_user.mfa.disable", TargetId = user.Id, Target = user.Email ?? string.Empty,
            Details = "Administrator disabled optional authenticator MFA after verifying a current code.",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(context.RequestAborted);
        await transaction.CommitAsync(context.RequestAborted);
        await signInManager.RefreshSignInAsync(user);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetUserAuthenticatorAsync(
        Guid userId,
        UserManager<AdminUser> userManager,
        StoreDbContext database,
        HttpContext context)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Results.NotFound();
        await using var transaction = await database.Database.BeginTransactionAsync(context.RequestAborted);
        var disabled = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disabled.Succeeded) return IdentityValidationResult(disabled);
        var result = await userManager.ResetAuthenticatorKeyAsync(user);
        if (!result.Succeeded) return IdentityValidationResult(result);
        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(), ActorUserId = Guid.Parse(userManager.GetUserId(context.User)!),
            ActorEmail = userManager.GetUserName(context.User) ?? string.Empty,
            Action = "admin_user.mfa.reset", TargetId = user.Id, Target = user.Email ?? string.Empty,
            Details = "Authenticator enrollment reset; user must enroll again before accessing admin functions.",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(context.RequestAborted);
        await transaction.CommitAsync(context.RequestAborted);
        return Results.NoContent();
    }

    private static string FormatAuthenticatorKey(string key) =>
        string.Join(" ", Enumerable.Range(0, (key.Length + 3) / 4).Select(index => key.Substring(index * 4, Math.Min(4, key.Length - index * 4))));

    private static async Task<IResult> GetCurrentUserAsync(
        UserManager<AdminUser> userManager,
        AdminSecuritySettingsService securitySettings,
        CancellationToken cancellationToken,
        HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new CurrentAdminUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            roles.ToArray(),
            user.MustChangePassword,
            user.TwoFactorEnabled,
            user.LockoutEnd,
            await securitySettings.GetMfaRequiredAsync(cancellationToken)));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        UserManager<AdminUser> userManager,
        SignInManager<AdminUser> signInManager,
        HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrEmpty(request.CurrentPassword)
            || string.IsNullOrEmpty(request.NewPassword)
            || request.CurrentPassword.Length > 1024
            || request.NewPassword.Length > 256)
        {
            return Results.BadRequest(new { error = "Enter a valid current password and a new password up to 256 characters." });
        }

        var mustChangePassword = user.MustChangePassword;
        user.MustChangePassword = false;
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            user.MustChangePassword = mustChangePassword;
            return Results.ValidationProblem(result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
        }

        await signInManager.RefreshSignInAsync(user);
        return Results.NoContent();
    }

    private static async Task<IResult> GetRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new AdminRoleResponse(role.Id, role.Name ?? string.Empty))
            .ToListAsync();

        return Results.Ok(roles);
    }

    private static async Task<IResult> GetUsersAsync(UserManager<AdminUser> userManager)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync();
        var result = new List<AdminUserResponse>(users.Count);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new AdminUserResponse(
                user.Id,
                user.Email ?? string.Empty,
                roles.ToArray(),
                user.MustChangePassword,
                user.TwoFactorEnabled,
                user.LockoutEnd));
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAuditEventsAsync(StoreDbContext database)
    {
        var events = await database.AdminAuditEvents
            .AsNoTracking()
            .OrderByDescending(auditEvent => auditEvent.CreatedAtUtc)
            .Take(100)
            .Select(auditEvent => new AdminAuditResponse(
                auditEvent.Id,
                auditEvent.ActorEmail,
                auditEvent.Action,
                auditEvent.Target,
                auditEvent.Details,
                auditEvent.CreatedAtUtc))
            .ToListAsync();

        return Results.Ok(events);
    }

    private static async Task<IResult> GetAdminProductAsync(
        AdminCatalogService catalog,
        CancellationToken cancellationToken)
    {
        var product = await catalog.GetProductAsync(cancellationToken);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    private static async Task<IResult> SaveAdminProductAsync(
        AdminProductRequest request,
        AdminCatalogService catalog,
        UserManager<AdminUser> userManager,
        HttpContext context)
    {
        if (!context.User.IsInRole(AdminRoles.SystemAdmin))
        {
            var current = await catalog.GetProductAsync(context.RequestAborted);
            if (current is null || IsStoreSettingsChanged(request, current)) return Results.Forbid();
        }

        var userId = userManager.GetUserId(context.User);
        if (!Guid.TryParse(userId, out var actorId))
        {
            return Results.Unauthorized();
        }

        var result = await catalog.SaveProductAsync(
            request,
            actorId,
            userManager.GetUserName(context.User) ?? string.Empty,
            context.RequestAborted);
        return result.Errors is not null
            ? Results.ValidationProblem(result.Errors)
            : Results.Ok(result.Product);
    }

    private static bool IsStoreSettingsChanged(AdminProductRequest request, AdminProductResponse current)
    {
        if (!string.Equals(request.BrandName?.Trim(), current.BrandName, StringComparison.Ordinal)
            || !string.Equals(request.StorefrontHeadline?.Trim() ?? string.Empty, current.StorefrontHeadline, StringComparison.Ordinal)
            || !string.Equals(request.StorefrontSubtitle?.Trim() ?? string.Empty, current.StorefrontSubtitle, StringComparison.Ordinal)
            || request.FlashSale != current.FlashSale
            || request.SocialSharingEnabled != current.SocialSharingEnabled
            || request.WhatsAppEnabled != current.WhatsAppEnabled
            || !string.Equals(request.WhatsAppPhoneNumber?.Trim() ?? string.Empty, current.WhatsAppPhoneNumber, StringComparison.Ordinal)
            || !string.Equals(request.WhatsAppPrefillMessage?.Trim() ?? string.Empty, current.WhatsAppPrefillMessage, StringComparison.Ordinal))
        {
            return true;
        }

        var requestedItems = request.TrustItems ?? [];
        return requestedItems.Count != current.TrustItems.Count || requestedItems.Any(requested =>
            !current.TrustItems.Any(saved => saved.Id == requested.Id
                && saved.Title == requested.Title
                && saved.Description == requested.Description
                && saved.IsActive == requested.IsActive
                && saved.DisplayOrder == requested.DisplayOrder));
    }

    private static async Task<IResult> UploadProductImageAsync(
        HttpContext context,
        ProductMediaService media,
        UserManager<AdminUser> userManager)
    {
        if (context.Request.ContentLength is > ProductMediaService.MaximumImageBytes + 128 * 1024)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(context.RequestAborted);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { error = "Upload a valid multipart form." });
        }

        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return Results.BadRequest(new { error = "Choose an image to upload." });
        }
        var mediaKind = form["mediaKind"].ToString();
        if (!CanManageMedia(context.User, mediaKind)) return Results.Forbid();
        if (!Guid.TryParse(userManager.GetUserId(context.User), out var actorId))
        {
            return Results.Unauthorized();
        }

        await using var stream = file.OpenReadStream();
        var result = await media.UploadAsync(
            stream,
            file.Length,
            mediaKind,
            form["altText"].ToString(),
            actorId,
            userManager.GetUserName(context.User) ?? string.Empty,
            context.RequestAborted);

        if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
        return result.Image is null
            ? Results.BadRequest(new { error = "Create a product draft before uploading media." })
            : Results.Created(result.Image.Url, result.Image);
    }

    private static async Task<IResult> OpenAdminProductImageAsync(
        Guid imageId,
        ProductMediaService media,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var kind = await media.GetMediaKindAsync(imageId, cancellationToken);
        if (kind is null) return Results.NotFound();
        if (!CanManageMedia(context.User, kind)) return Results.Forbid();
        var image = await media.OpenAdminImageAsync(imageId, cancellationToken);
        return image is null
            ? Results.NotFound()
            : Results.File(image.Content, image.ContentType, enableRangeProcessing: false);
    }

    private static async Task<IResult> UpdateProductImageAsync(
        Guid imageId,
        AdminProductImageUpdateRequest request,
        ProductMediaService media,
        UserManager<AdminUser> userManager,
        HttpContext context)
    {
        var kind = await media.GetMediaKindAsync(imageId, context.RequestAborted);
        if (kind is null) return Results.NotFound();
        if (!CanManageMedia(context.User, kind)) return Results.Forbid();
        if (!Guid.TryParse(userManager.GetUserId(context.User), out var actorId)) return Results.Unauthorized();
        var result = await media.UpdateAsync(
            imageId,
            request.AltText,
            request.DisplayOrder,
            actorId,
            userManager.GetUserName(context.User) ?? string.Empty,
            context.RequestAborted);
        if (result.IsNotFound) return Results.NotFound();
        return result.Error is not null ? Results.BadRequest(new { error = result.Error }) : Results.Ok(result.Image);
    }

    private static async Task<IResult> DeleteProductImageAsync(
        Guid imageId,
        ProductMediaService media,
        UserManager<AdminUser> userManager,
        HttpContext context)
    {
        var kind = await media.GetMediaKindAsync(imageId, context.RequestAborted);
        if (kind is null) return Results.NotFound();
        if (!CanManageMedia(context.User, kind)) return Results.Forbid();
        if (!Guid.TryParse(userManager.GetUserId(context.User), out var actorId)) return Results.Unauthorized();
        var deleted = await media.DeleteAsync(
            imageId,
            actorId,
            userManager.GetUserName(context.User) ?? string.Empty,
            context.RequestAborted);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static bool CanManageMedia(System.Security.Claims.ClaimsPrincipal principal, string mediaKind) =>
        principal.IsInRole(AdminRoles.SystemAdmin)
        || (mediaKind == "product" && principal.IsInRole(AdminRoles.CatalogManager))
        || (mediaKind == "logo" && principal.IsInRole(AdminRoles.StoreManager));

    private static async Task<IResult> CreateUserAsync(
        CreateAdminUserRequest request,
        UserManager<AdminUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        StoreDbContext database,
        HttpContext context)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
        {
            return Results.BadRequest(new { error = "Enter a valid email address." });
        }

        if (string.IsNullOrEmpty(request.TemporaryPassword) || request.TemporaryPassword.Length > 256)
        {
            return Results.BadRequest(new { error = "Enter a temporary password up to 256 characters long." });
        }

        var roleNames = NormalizeRoleNames(request.RoleNames);
        if (roleNames.Length == 0 || roleNames.Any(role => !AdminRoles.BuiltIn.Contains(role, StringComparer.Ordinal)))
        {
            return Results.BadRequest(new { error = "Select at least one valid role." });
        }

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                return Results.BadRequest(new { error = "One or more selected roles do not exist." });
            }
        }

        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            MustChangePassword = true
        };

        await using var transaction = await database.Database.BeginTransactionAsync(context.RequestAborted);
        var createResult = await userManager.CreateAsync(user, request.TemporaryPassword ?? string.Empty);
        if (!createResult.Succeeded)
        {
            return IdentityValidationResult(createResult);
        }

        var roleResult = await userManager.AddToRolesAsync(user, roleNames);
        if (!roleResult.Succeeded)
        {
            return IdentityValidationResult(roleResult);
        }

        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = Guid.Parse(userManager.GetUserId(context.User)!),
            ActorEmail = userManager.GetUserName(context.User) ?? string.Empty,
            Action = "admin_user.create",
            TargetId = user.Id,
            Target = email,
            Details = JsonSerializer.Serialize(new { roles = roleNames }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(context.RequestAborted);
        await transaction.CommitAsync(context.RequestAborted);

        return Results.Created($"/api/v1/admin/users/{user.Id}", new AdminUserResponse(
            user.Id, email, roleNames, user.MustChangePassword, user.TwoFactorEnabled, user.LockoutEnd));
    }

    private static async Task<IResult> UpdateUserRolesAsync(
        Guid userId,
        UpdateAdminUserRolesRequest request,
        UserManager<AdminUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        StoreDbContext database,
        SignInManager<AdminUser> signInManager,
        HttpContext context)
    {
        var target = await userManager.FindByIdAsync(userId.ToString());
        if (target is null)
        {
            return Results.NotFound();
        }

        var roleNames = NormalizeRoleNames(request.RoleNames);
        if (roleNames.Length == 0 || roleNames.Any(role => !AdminRoles.BuiltIn.Contains(role, StringComparer.Ordinal)))
        {
            return Results.BadRequest(new { error = "Select at least one valid role." });
        }

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                return Results.BadRequest(new { error = "One or more selected roles do not exist." });
            }
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, context.RequestAborted);
        var currentRoles = await userManager.GetRolesAsync(target);
        var isRemovingSystemAdmin = currentRoles.Contains(AdminRoles.SystemAdmin, StringComparer.Ordinal)
            && !roleNames.Contains(AdminRoles.SystemAdmin, StringComparer.Ordinal);
        if (isRemovingSystemAdmin)
        {
            var systemAdmins = await userManager.GetUsersInRoleAsync(AdminRoles.SystemAdmin);
            if (systemAdmins.Count <= 1)
            {
                return Results.Conflict(new { error = "The last SystemAdmin role cannot be removed." });
            }
        }

        var removeResult = await userManager.RemoveFromRolesAsync(target, currentRoles);
        if (!removeResult.Succeeded)
        {
            return IdentityValidationResult(removeResult);
        }

        var addResult = await userManager.AddToRolesAsync(target, roleNames);
        if (!addResult.Succeeded)
        {
            return IdentityValidationResult(addResult);
        }

        await userManager.UpdateSecurityStampAsync(target);
        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = Guid.Parse(userManager.GetUserId(context.User)!),
            ActorEmail = userManager.GetUserName(context.User) ?? string.Empty,
            Action = "admin_user.roles.update",
            TargetId = target.Id,
            Target = target.Email ?? string.Empty,
            Details = JsonSerializer.Serialize(new { previousRoles = currentRoles, roles = roleNames }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(context.RequestAborted);
        await transaction.CommitAsync(context.RequestAborted);

        if (string.Equals(target.Id.ToString(), userManager.GetUserId(context.User), StringComparison.Ordinal))
        {
            if (roleNames.Contains(AdminRoles.SystemAdmin, StringComparer.Ordinal))
            {
                await signInManager.RefreshSignInAsync(target);
            }
            else
            {
                await signInManager.SignOutAsync();
            }
        }

        return Results.NoContent();
    }

    private static string[] NormalizeRoleNames(IEnumerable<string>? roleNames) =>
        roleNames?
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

    private static IResult IdentityValidationResult(IdentityResult result) =>
        Results.ValidationProblem(result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));

    internal static async ValueTask<object?> ValidateAntiforgeryAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new { error = "A valid anti-forgery token is required." });
        }

        return await next(context);
    }

    internal static async ValueTask<object?> RequireChangedPasswordAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AdminUser>>();
        var securitySettings = context.HttpContext.RequestServices.GetRequiredService<AdminSecuritySettingsService>();
        var user = await userManager.GetUserAsync(context.HttpContext.User);
        var mfaRequired = await securitySettings.GetMfaRequiredAsync(context.HttpContext.RequestAborted);
        return user is null
            ? Results.Unauthorized()
            : user.MustChangePassword
                ? Results.StatusCode(StatusCodes.Status403Forbidden)
                : mfaRequired && !user.TwoFactorEnabled && !context.HttpContext.Request.Path.StartsWithSegments("/api/v1/admin/mfa")
                    ? Results.StatusCode(StatusCodes.Status403Forbidden)
                    : await next(context);
    }

    public sealed record AdminLoginRequest(string Email, string Password);
    public sealed record AuthenticatorLoginRequest(string Code);
    public sealed record AuthenticatorCodeRequest(string Code);
    public sealed record MfaPolicyRequest(bool Required);
    public sealed record PasswordResetRequest(string Email);
    public sealed record CompletePasswordResetRequest(string Email, string Token, string NewPassword);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public sealed record CreateAdminUserRequest(string Email, string TemporaryPassword, string[] RoleNames);
    public sealed record UpdateAdminUserRolesRequest(string[] RoleNames);
    public sealed record AdminProductImageUpdateRequest(string AltText, int DisplayOrder);
    public sealed record AdminRoleResponse(Guid Id, string Name);
    public sealed record AdminAuditResponse(
        Guid Id,
        string ActorEmail,
        string Action,
        string Target,
        string Details,
        DateTimeOffset CreatedAtUtc);
    public sealed record AdminUserResponse(
        Guid Id,
        string Email,
        string[] Roles,
        bool MustChangePassword,
        bool TwoFactorEnabled,
        DateTimeOffset? LockoutEnd);
    public sealed record CurrentAdminUserResponse(
        Guid Id,
        string Email,
        string[] Roles,
        bool MustChangePassword,
        bool TwoFactorEnabled,
        DateTimeOffset? LockoutEnd,
        bool MfaRequired);
}

public sealed class AdminEndpointsMarker { }
